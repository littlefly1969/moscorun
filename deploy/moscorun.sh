#!/usr/bin/env bash
# Gestione di MoscoRun sul server con un solo comando.
#
#   ./moscorun.sh update [versione]   backup -> pull -> migrazioni -> riavvio -> verifica salute (rollback automatico)
#   ./moscorun.sh rollback            torna all'immagine in uso prima dell'ultimo update
#   ./moscorun.sh backup              copia del database (pg_dump, formato custom)
#   ./moscorun.sh restore <file>      ripristina un backup (chiede conferma)
#   ./moscorun.sh status | logs
#
# Pensato per essere eseguito anche dal timer systemd (aggiornamento automatico notturno).
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
  : "${POSTGRES_HOST:?POSTGRES_HOST mancante in .env}"
  : "${POSTGRES_PASSWORD:?POSTGRES_PASSWORD mancante in .env}"
  mkdir -p "$STATE_DIR"
  MOSCORUN_ENV_LOADED=1
}

current_image_id() { docker inspect --format '{{.Image}}' "$(compose ps -q app 2>/dev/null)" 2>/dev/null || true; }

wait_healthy() {
  local deadline=$((SECONDS + HEALTH_TIMEOUT_SECONDS)) status cid
  cid="$(compose ps -q app)"
  while ((SECONDS < deadline)); do
    status="$(docker inspect --format '{{.State.Health.Status}}' "$cid" 2>/dev/null || echo unknown)"
    [[ "$status" == healthy ]] && return 0
    sleep 3
  done
  return 1
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

  local next
  next="$(docker image inspect --format '{{.Id}}' "$(compose config --images | grep -m1 moscorun)")"
  if [[ -n "$previous" && "$previous" == "$next" ]]; then
    log "già aggiornato: nessuna azione"
    return 0
  fi

  if [[ "${COMPOSE_PROFILES:-}" == *bundled-db* ]]; then compose up -d --wait db; fi
  if [[ -n "$previous" ]]; then cmd_backup; fi

  log "migrazioni del database"
  compose run --rm --no-deps app migrate || die "migrazioni fallite: resta in esecuzione la versione precedente"

  if [[ -n "$previous" ]]; then echo "$previous" >"$STATE_DIR/previous-image"; fi
  compose up -d --no-deps app

  if wait_healthy; then
    log "MoscoRun in salute: $(curl -fsS "http://127.0.0.1:${MOSCORUN_PORT:-8085}/api/system/info" 2>/dev/null || echo 'versione non leggibile')"
    docker image prune -f --filter "label=org.opencontainers.image.title=MoscoRun" --filter "until=720h" >/dev/null || true
  else
    log "la nuova versione non risponde entro ${HEALTH_TIMEOUT_SECONDS}s"
    if [[ -n "$previous" ]]; then cmd_rollback; fi
    die "aggiornamento annullato; controllare i log con: $0 logs"
  fi
}

cmd_rollback() {
  load_env
  [[ -f "$STATE_DIR/previous-image" ]] || die "nessuna versione precedente registrata"
  local previous
  previous="$(<"$STATE_DIR/previous-image")"
  log "ritorno all'immagine $previous"
  log "nota: le migrazioni già applicate restano; se incompatibili ripristinare l'ultimo backup con '$0 restore'"
  docker tag "$previous" moscorun:rollback
  MOSCORUN_IMAGE=moscorun MOSCORUN_VERSION=rollback compose up -d --no-deps app
  if wait_healthy; then log "rollback completato"; else die "anche la versione precedente non risponde"; fi
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
  if wait_healthy; then log "ripristino completato"; else die "l'applicazione non risponde dopo il ripristino"; fi
}

cmd_status() {
  load_env
  compose ps
  curl -fsS "http://127.0.0.1:${MOSCORUN_PORT:-8085}/api/system/info" && echo
}

main() {
  command -v docker >/dev/null || die "docker non installato"
  local cmd="${1:-}"
  shift || true
  case "$cmd" in
    update) cmd_update "$@" ;;
    rollback) cmd_rollback ;;
    backup) cmd_backup ;;
    restore) cmd_restore "$@" ;;
    status) cmd_status ;;
    logs)
      load_env
      compose logs --tail=200 -f app
      ;;
    *) sed -n '2,10p' "$0" | sed 's/^# \{0,1\}//'; exit 2 ;;
  esac
}

main "$@"
