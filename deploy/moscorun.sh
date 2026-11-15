#!/usr/bin/env bash
# Gestione di MoscoRun sul server con un solo comando.
#
#   ./moscorun.sh update [versione]   pull -> backup -> migrazioni -> riavvio -> verifica (rollback automatico)
#   ./moscorun.sh rollback            torna all'immagine in uso prima dell'ultimo update
#   ./moscorun.sh backup              copia del database (pg_dump, formato custom)
#   ./moscorun.sh restore <file>      ripristina un backup (chiede conferma)
#   ./moscorun.sh status | logs       stato (esce con errore se l'app non è pronta) e log
#   ./moscorun.sh auto-update         usato dal timer systemd: aggiorna solo se MOSCORUN_AUTO_UPDATE=1
#   ./moscorun.sh proxy-config nginx|apache   stampa la configurazione del reverse proxy generata da .env
#   ./moscorun.sh check               verifica la coerenza di .env senza toccare container o database
#
# Il gate di ogni aggiornamento è /health/ready (database raggiungibile), non la sola liveness del processo.
set -Eeuo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
readonly SCRIPT_DIR
readonly STATE_DIR="$SCRIPT_DIR/.state"
readonly HEALTH_TIMEOUT_SECONDS="${MOSCORUN_HEALTH_TIMEOUT:-90}"
readonly PG_CLIENT_IMAGE="postgres:17-alpine"

log() { printf '%s [moscorun] %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$*"; }
die() { log "ERRORE: $*" >&2; exit 1; }

compose() { docker compose --project-directory "$SCRIPT_DIR" -f "$SCRIPT_DIR/compose.yaml" "$@"; }

load_env() {
  # Una sola lettura: una versione passata da riga di comando non deve essere sovrascritta da .env.
  if [[ -n "${MOSCORUN_ENV_LOADED:-}" ]]; then return 0; fi
  [[ -f "$SCRIPT_DIR/.env" ]] || die "manca $SCRIPT_DIR/.env (copiare .env.example e compilare)"
  local perms
  perms="$(stat -c '%a' "$SCRIPT_DIR/.env")"
  [[ "$perms" =~ ^[0-7]00$ ]] || die ".env deve essere leggibile solo dal proprietario: chmod 600 $SCRIPT_DIR/.env"
  set -a
  # shellcheck disable=SC1091
  source "$SCRIPT_DIR/.env"
  set +a
  validate_env
  mkdir -p "$STATE_DIR"
  MOSCORUN_ENV_LOADED=1
}

has_profile() { [[ ",${COMPOSE_PROFILES:-}," == *",$1,"* ]]; }

# Errori di configurazione fermati prima di toccare immagini, container o database, con l'indicazione del rimedio.
validate_env() {
  [[ -n "${POSTGRES_HOST:-}" ]] || die "POSTGRES_HOST mancante in .env (database incluso: POSTGRES_HOST=db e COMPOSE_PROFILES=bundled-db)"
  [[ -n "${POSTGRES_PASSWORD:-}" ]] || die "POSTGRES_PASSWORD mancante in .env (generarla con: openssl rand -base64 32)"
  if has_profile bundled-db; then
    [[ "$POSTGRES_HOST" == db ]] ||
      die "COMPOSE_PROFILES=bundled-db avvia il database incluso, raggiungibile solo come POSTGRES_HOST=db (ora: $POSTGRES_HOST)"
  elif [[ "$POSTGRES_HOST" == db ]]; then
    die "POSTGRES_HOST=db indica il database incluso, che parte solo con COMPOSE_PROFILES=bundled-db in .env"
  fi
  case "$POSTGRES_HOST" in
    localhost | 127.* | ::1 | '[::1]')
      die "POSTGRES_HOST=$POSTGRES_HOST punta al container dell'app, non al server: per un PostgreSQL sull'host usare POSTGRES_HOST=host.docker.internal" ;;
  esac
  local name value
  for name in POSTGRES_PORT MOSCORUN_PORT; do
    value="${!name:-}"
    [[ -z "$value" || ( "$value" =~ ^[0-9]{1,5}$ && "$value" -ge 1 && "$value" -le 65535 ) ]] ||
      die "$name non è una porta valida: $value"
  done
}

cmd_check() {
  load_env
  local db="PostgreSQL esterno su $POSTGRES_HOST:${POSTGRES_PORT:-5432}"
  has_profile bundled-db && db="PostgreSQL incluso (container db)"
  log "configurazione valida: $db, app su 127.0.0.1:${MOSCORUN_PORT:-8085}"
}

current_image_id() { docker inspect --format '{{.Image}}' "$(compose ps -q app 2>/dev/null)" 2>/dev/null || true; }

# Readiness: l'app risponde e raggiunge il database. La liveness (HEALTHCHECK Docker) da sola non basta.
is_ready() { compose exec -T app dotnet /app/MoscoRun.Api.dll healthcheck ready >/dev/null 2>&1; }

wait_ready() {
  local deadline=$((SECONDS + HEALTH_TIMEOUT_SECONDS)) status cid
  cid="$(compose ps -q app)"
  while ((SECONDS < deadline)); do
    status="$(docker inspect --format '{{.State.Health.Status}}' "$cid" 2>/dev/null || echo unknown)"
    if [[ "$status" == healthy ]] && is_ready; then return 0; fi
    sleep 3
  done
  log "stato container: ${status:-sconosciuto}; readiness (database) non raggiunta entro ${HEALTH_TIMEOUT_SECONDS}s"
  return 1
}

# Certificato che cifra a riposo le chiavi dei cookie (Data Protection). Generato una volta dall'immagine
# stessa e conservato solo in .env (permessi 600): va copiato insieme a .env, non nel backup del database.
ensure_data_protection_certificate() {
  if [[ -n "${MOSCORUN_DATA_PROTECTION_CERT:-}" ]]; then return 0; fi
  log "generazione del certificato Data Protection (salvato in .env)"
  local cert
  cert="$(compose run --rm --no-deps -T app generate-data-protection-certificate | tr -d '\r\n')"
  [[ "$cert" =~ ^[A-Za-z0-9+/=]{1000,}$ ]] || die "generazione del certificato Data Protection fallita"
  printf '\n# Generato da moscorun.sh il %s: cifra le chiavi dei cookie. Non condividere.\nMOSCORUN_DATA_PROTECTION_CERT=%s\n' \
    "$(date -u +%Y-%m-%d)" "$cert" >>"$SCRIPT_DIR/.env"
  export MOSCORUN_DATA_PROTECTION_CERT="$cert"
}

# pg_dump/psql eseguiti in un container usa e getta sulla rete del progetto:
# raggiungono sia il database incluso sia quello sull'host.
pg_run() {
  local network
  network="$(docker network ls --filter "name=^moscorun_default$" --format '{{.Name}}' | head -n1)"
  docker run --rm -i \
    ${network:+--network "$network"} \
    --add-host host.docker.internal:host-gateway \
    -e PGPASSWORD="$POSTGRES_PASSWORD" \
    -e PGSSLMODE="$(tr '[:upper:]' '[:lower:]' <<<"${POSTGRES_SSLMODE:-prefer}")" \
    "$PG_CLIENT_IMAGE" "$@"
}

cmd_backup() {
  load_env
  local dir="${MOSCORUN_BACKUP_DIR:-/var/backups/moscorun}" keep="${MOSCORUN_BACKUP_KEEP:-14}" file
  mkdir -p "$dir"
  chmod 700 "$dir"
  file="$dir/moscorun-$(date -u +%Y%m%dT%H%M%SZ).dump"
  log "backup del database in $file"
  pg_run pg_dump --format=custom --no-owner \
    -h "$POSTGRES_HOST" -p "${POSTGRES_PORT:-5432}" -U "${POSTGRES_USER:-moscorun}" "${POSTGRES_DB:-moscorun}" >"$file.partial"
  [[ -s "$file.partial" ]] || die "backup vuoto: aggiornamento interrotto"
  mv "$file.partial" "$file"
  chmod 600 "$file"
  echo "$file" >"$STATE_DIR/last-backup"
  # Rotazione: tiene solo gli ultimi $keep backup.
  find "$dir" -maxdepth 1 -name 'moscorun-*.dump' -printf '%T@ %p\n' | sort -rn | tail -n "+$((keep + 1))" |
    cut -d' ' -f2- | xargs -r rm -f --
  log "backup completato ($(du -h "$file" | cut -f1))"
}

cmd_update() {
  load_env
  if [[ $# -gt 0 ]]; then export MOSCORUN_VERSION="$1"; fi
  log "aggiornamento a ${MOSCORUN_IMAGE:-ghcr.io/littlefly1969/moscorun}:${MOSCORUN_VERSION:-latest}"

  local previous
  previous="$(current_image_id)"
  if [[ "${MOSCORUN_SKIP_PULL:-0}" != 1 ]]; then compose pull app; fi
  ensure_data_protection_certificate

  local next
  next="$(docker image inspect --format '{{.Id}}' "$(compose config --images | grep -m1 moscorun)")"
  if [[ -n "$previous" && "$previous" == "$next" ]]; then
    log "già aggiornato: nessuna azione"
    return 0
  fi

  if [[ "${COMPOSE_PROFILES:-}" == *bundled-db* ]]; then compose up -d --wait db; fi
  if [[ -n "$previous" ]]; then cmd_backup; fi

  # Le migrazioni "expand" lasciano funzionante la versione in uso; quelle "contract" (rimozioni) vengono
  # rifiutate dal comando migrate se non autorizzate esplicitamente, prima di toccare lo schema.
  log "migrazioni del database"
  local migrate_args=(migrate) status=0
  if [[ "${MOSCORUN_ALLOW_CONTRACT_MIGRATIONS:-0}" == 1 ]]; then migrate_args+=(--allow-contract); fi
  compose run --rm --no-deps -T app "${migrate_args[@]}" || status=$?
  case "$status" in
    0) ;;
    3) die "la release contiene migrazioni che rimuovono struttura del database: nulla è stato modificato.
   Dopo aver letto le note di rilascio: MOSCORUN_ALLOW_CONTRACT_MIGRATIONS=1 $0 update ${MOSCORUN_VERSION:-}" ;;
    *) die "migrazioni fallite (codice $status): resta in esecuzione la versione precedente" ;;
  esac

  if [[ -n "$previous" ]]; then echo "$previous" >"$STATE_DIR/previous-image"; fi
  compose up -d --no-deps app

  if wait_ready; then
    log "MoscoRun pronto: $(curl -fsS "http://127.0.0.1:${MOSCORUN_PORT:-8085}/api/system/info" 2>/dev/null || echo 'versione non leggibile')"
    docker image prune -f --filter "label=org.opencontainers.image.title=MoscoRun" --filter "until=720h" >/dev/null || true
  else
    log "la nuova versione non è pronta"
    if [[ -n "$previous" ]]; then cmd_rollback; fi
    die "aggiornamento annullato; controllare i log con: $0 logs"
  fi
}

cmd_rollback() {
  load_env
  [[ -f "$STATE_DIR/previous-image" ]] || die "nessuna versione precedente registrata"
  local previous
  previous="$(<"$STATE_DIR/previous-image")"
  log "ritorno all'immagine $previous (lo schema resta quello migrato: le migrazioni automatiche sono solo expand)"
  docker tag "$previous" moscorun:rollback
  MOSCORUN_IMAGE=moscorun MOSCORUN_VERSION=rollback compose up -d --no-deps app
  if wait_ready; then
    log "rollback completato"
  else
    local backup="(nessun backup registrato)"
    [[ -f "$STATE_DIR/last-backup" ]] && backup="$(<"$STATE_DIR/last-backup")"
    die "anche la versione precedente non è pronta. Verificare il database ($0 status, $0 logs);
   se lo schema è la causa, ripristinare il backup fatto prima dell'aggiornamento: $0 restore $backup"
  fi
}

cmd_restore() {
  load_env
  local file="${1:?indicare il file di backup}"
  [[ -f "$file" ]] || die "file non trovato: $file"
  read -r -p "Il database ${POSTGRES_DB:-moscorun} verrà sovrascritto con $file. Scrivere RIPRISTINA per continuare: " answer
  [[ "$answer" == RIPRISTINA ]] || die "annullato"
  compose stop app
  pg_run pg_restore --clean --if-exists --no-owner \
    -h "$POSTGRES_HOST" -p "${POSTGRES_PORT:-5432}" -U "${POSTGRES_USER:-moscorun}" -d "${POSTGRES_DB:-moscorun}" <"$file"
  compose start app
  if wait_ready; then log "ripristino completato"; else die "l'applicazione non è pronta dopo il ripristino"; fi
}

cmd_status() {
  load_env
  compose ps
  curl -fsS "http://127.0.0.1:${MOSCORUN_PORT:-8085}/api/system/info" && echo
  if is_ready; then log "pronto: database raggiungibile"; else die "NON pronto: database irraggiungibile o applicazione ferma"; fi
}

# Il timer notturno resta inerte finché l'aggiornamento automatico non è abilitato esplicitamente in .env,
# dopo aver verificato sul server reale aggiornamento, rollback, backup e ripristino.
cmd_auto_update() {
  load_env
  if [[ "${MOSCORUN_AUTO_UPDATE:-0}" != 1 ]]; then
    log "aggiornamento automatico disabilitato (MOSCORUN_AUTO_UPDATE=1 in .env per abilitarlo)"
    return 0
  fi
  # Mai migrazioni contract senza una persona presente.
  MOSCORUN_ALLOW_CONTRACT_MIGRATIONS=0 cmd_update
}

# Configurazioni dei reverse proxy generate dai parametri di .env (validati: finiscono in file di sistema).
readonly HOST_RE='^[A-Za-z0-9]([A-Za-z0-9-]*[A-Za-z0-9])?(\.[A-Za-z0-9]([A-Za-z0-9-]*[A-Za-z0-9])?)*$'
readonly ADDR_RE='^[0-9A-Fa-f:.]+(/[0-9]{1,3})?$'
readonly LISTEN_RE='^(([0-9.]+|\[[0-9A-Fa-f:]+\]):)?[0-9]{1,5}$'
readonly UPSTREAM_RE='^https?://([A-Za-z0-9.-]+|\[[0-9A-Fa-f:]+\])(:[0-9]{1,5})?/$'
readonly PATH_RE='^/[A-Za-z0-9._/-]+$'

# fill <testo> CHIAVE valore ...: sostituisce i segnaposto @@CHIAVE@@.
fill() {
  local out="$1" key
  shift
  while (($#)); do
    key="$1"
    out="${out//"@@${key}@@"/"$2"}"
    shift 2
  done
  printf '%s' "$out"
}
part() { [[ -f "$SCRIPT_DIR/proxy/$1" ]] || die "modello mancante: proxy/$1"; cat "$SCRIPT_DIR/proxy/$1"; }
require() { [[ "$2" =~ $3 ]] || die "$1 non valido o mancante in .env: '$2'"; }
done_render() { [[ "$1" != *@@* ]] || die "segnaposto non sostituito nella configurazione generata"; printf '%s\n' "$1"; }

proxy_nginx() {
  local host="$1" mode="${MOSCORUN_PROXY_TLS:-0}" listen="${MOSCORUN_PROXY_LISTEN:-80}"
  local tls_listen="${MOSCORUN_PROXY_TLS_LISTEN:-443}" port="${MOSCORUN_PORT:-8085}"
  local acme="${MOSCORUN_ACME_ROOT:-/var/www/letsencrypt}" geo="" realip="" addr site servers headers="" h3=""
  require MOSCORUN_PROXY_LISTEN "$listen" "$LISTEN_RE"
  require MOSCORUN_PORT "$port" '^[0-9]{1,5}$'
  require MOSCORUN_ACME_ROOT "$acme" "$PATH_RE"
  local edges="${MOSCORUN_EDGE_PROXIES:-}"
  for addr in ${edges//,/ }; do
    require MOSCORUN_EDGE_PROXIES "$addr" "$ADDR_RE"
    geo+=$'\n'"    $addr 1;"
    realip+="    set_real_ip_from $addr;"$'\n'
  done
  if [[ -n "$realip" ]]; then
    realip="    # IP reale del client: l'ultimo indirizzo aggiunto dalla frontiera, accettato solo dalla frontiera."$'\n'"${realip}    real_ip_header X-Forwarded-For;"$'\n'
  fi
  if [[ "${MOSCORUN_PROXY_HTTP3:-0}" == 1 ]]; then
    [[ "$mode" == 1 ]] || die "MOSCORUN_PROXY_HTTP3=1 richiede MOSCORUN_PROXY_TLS=1"
    h3="    listen $tls_listen quic;"
    headers="    add_header Alt-Svc 'h3=\":${tls_listen##*:}\"; ma=86400' always;"$'\n'
  fi
  site="$(fill "$(part nginx-site.part)" PUBLIC_HOST "$host" EDGE_REALIP "$realip" EXTRA_HEADERS "$headers" ACME_ROOT "$acme" APP_PORT "$port")"
  case "$mode" in
    0)
      servers="$(fill "$(part nginx-server-http.part)" LISTEN "$listen" PUBLIC_HOST "$host" SITE "$site")"
      mode="HTTP su $listen${edges:+, HTTPS terminato dalla frontiera}"
      ;;
    1)
      local cert="${MOSCORUN_TLS_CERT:-/etc/letsencrypt/live/$host/fullchain.pem}"
      local key="${MOSCORUN_TLS_KEY:-/etc/letsencrypt/live/$host/privkey.pem}"
      require MOSCORUN_PROXY_TLS_LISTEN "$tls_listen" "$LISTEN_RE"
      require MOSCORUN_TLS_CERT "$cert" "$PATH_RE"
      require MOSCORUN_TLS_KEY "$key" "$PATH_RE"
      servers="$(fill "$(part nginx-server-tls.part)" LISTEN "$listen" TLS_LISTEN "$tls_listen" HTTP3_LISTEN "$h3" \
        PUBLIC_HOST "$host" TLS_CERT "$cert" TLS_KEY "$key" ACME_ROOT "$acme" SITE "$site")"
      mode="HTTPS terminato da nginx su $tls_listen${h3:+ (HTTP/2 e HTTP/3)}, redirect da $listen"
      ;;
    *) die "MOSCORUN_PROXY_TLS deve valere 0 o 1" ;;
  esac
  done_render "$(fill "$(part nginx.conf.template)" PUBLIC_HOST "$host" APP_PORT "$port" MODE "$mode" \
    EDGE_GEO "$geo" SERVERS "$servers")"
}

proxy_apache() {
  local host="$1" upstream="${MOSCORUN_PROXY_UPSTREAM:-}" ssl_proxy=""
  [[ -z "$upstream" || "$upstream" == */ ]] || upstream+=/
  require MOSCORUN_PROXY_UPSTREAM "$upstream" "$UPSTREAM_RE"
  local cert="${MOSCORUN_TLS_CERT:-/etc/letsencrypt/live/$host/fullchain.pem}"
  local key="${MOSCORUN_TLS_KEY:-/etc/letsencrypt/live/$host/privkey.pem}"
  require MOSCORUN_TLS_CERT "$cert" "$PATH_RE"
  require MOSCORUN_TLS_KEY "$key" "$PATH_RE"
  [[ "$upstream" != https://* ]] || ssl_proxy="    SSLProxyEngine on"$'\n'
  done_render "$(fill "$(part apache-edge.conf.template)" PUBLIC_HOST "$host" UPSTREAM "$upstream" \
    TLS_CERT "$cert" TLS_KEY "$key" SSL_PROXY "$ssl_proxy")"
}

cmd_proxy_config() {
  load_env
  local host="${MOSCORUN_PUBLIC_HOST:-}"
  require MOSCORUN_PUBLIC_HOST "$host" "$HOST_RE"
  case "${1:-}" in
    nginx) proxy_nginx "$host" ;;
    apache) proxy_apache "$host" ;;
    *) die "uso: moscorun.sh proxy-config nginx|apache" ;;
  esac
}

main() {
  local cmd="${1:-}"
  shift || true
  [[ "$cmd" == check ]] || command -v docker >/dev/null || die "docker non installato"
  case "$cmd" in
    check) cmd_check ;;
    update) cmd_update "$@" ;;
    rollback) cmd_rollback ;;
    backup) cmd_backup ;;
    restore) cmd_restore "$@" ;;
    status) cmd_status ;;
    auto-update) cmd_auto_update ;;
    proxy-config) cmd_proxy_config "$@" ;;
    logs)
      load_env
      compose logs --tail=200 -f app
      ;;
    *) sed -n '2,13p' "$0" | sed 's/^# \{0,1\}//'; exit 2 ;;
  esac
}

main "$@"
