#!/usr/bin/env bash
# Scenari operativi di moscorun.sh su uno stack reale (CI, dopo l'installazione e gli E2E).
# Richiede: deploy/.env con COMPOSE_PROFILES=bundled-db, immagine $MOSCORUN_IMAGE:$MOSCORUN_VERSION in esecuzione,
# E2E_ADMIN_EMAIL ed E2E_ADMIN_PASSWORD. Ogni scenario lascia lo stack pronto per il successivo.
set -Eeuo pipefail

DEPLOY_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
readonly DEPLOY_DIR
readonly SCRIPT="$DEPLOY_DIR/moscorun.sh"
WORK="$(mktemp -d)"
readonly WORK
readonly NGINX_TEST=moscorun-nginx-test APACHE_TEST=moscorun-apache-test
trap 'docker rm -f "$NGINX_TEST" "$APACHE_TEST" >/dev/null 2>&1 || true; rm -rf "$WORK"' EXIT

set -a
# shellcheck disable=SC1091
source "$DEPLOY_DIR/.env"
set +a
readonly BASE="http://127.0.0.1:${MOSCORUN_PORT:-8085}"
readonly IMAGE="${MOSCORUN_IMAGE}:${MOSCORUN_VERSION}"
: "${E2E_ADMIN_EMAIL:?}" "${E2E_ADMIN_PASSWORD:?}"
readonly NGINX_IMAGE="${NGINX_IMAGE:-nginx:1.28.0-alpine}" HTTPD_IMAGE="${HTTPD_IMAGE:-httpd:2.4.63-alpine}"

compose() { docker compose --project-directory "$DEPLOY_DIR" -f "$DEPLOY_DIR/compose.yaml" "$@"; }
step() { printf '\n::group::%s\n' "$*"; }
pass() { printf '✔ %s\n::endgroup::\n' "$*"; }
fail() { printf '::error::%s\n' "$*"; exit 1; }
sql() { compose exec -T db psql -U "${POSTGRES_USER:-moscorun}" -d "${POSTGRES_DB:-moscorun}" -tAc "$1"; }
app_image() { docker inspect --format '{{.Image}}' "$(compose ps -q app)"; }
login_body() { printf '{"email":"%s","password":"%s"}' "$E2E_ADMIN_EMAIL" "$E2E_ADMIN_PASSWORD"; }

login() {
  curl -fsS -o /dev/null -c "$WORK/cookies" -H 'Content-Type: application/json' --data "$(login_body)" "$BASE/api/auth/login"
}
session_valid() { [[ "$(curl -s -o /dev/null -w '%{http_code}' -b "$WORK/cookies" "$BASE/api/auth/me")" == 200 ]]; }
# Smoke funzionale: le query di business principali rispondono, non solo la connessione al database.
smoke() {
  local path
  login
  for path in /api/auth/me /api/v1/dashboard/summary /api/v1/weighings /api/v1/devices; do
    [[ "$(curl -s -o /dev/null -w '%{http_code}' -b "$WORK/cookies" "$BASE$path")" == 200 ]] || fail "smoke: $path non risponde 200"
  done
}
wait_until_ready() {
  for _ in $(seq 1 40); do "$SCRIPT" status >/dev/null 2>&1 && return 0; sleep 3; done
  return 1
}

step "Smoke funzionale sulla versione installata"
smoke
pass "API principali operative"

step "Aggiornamento senza nuova immagine: nessuna azione"
"$SCRIPT" update | tee "$WORK/out"
grep -q "già aggiornato" "$WORK/out" || fail "atteso 'già aggiornato'"
pass "update idempotente"

step "Timer notturno: inerte finché MOSCORUN_AUTO_UPDATE non vale 1"
"$SCRIPT" auto-update | tee "$WORK/out"
grep -q "aggiornamento automatico disabilitato" "$WORK/out" || fail "auto-update non dovrebbe agire"
pass "auto-update disabilitato per default"

step "Riavvio: le sessioni restano valide (chiavi Data Protection persistite e cifrate)"
login
compose restart app
wait_until_ready || fail "app non pronta dopo il riavvio"
session_valid || fail "sessione persa dopo il riavvio"
key_xml="$(sql "select xml from moscorun.data_protection_keys order by id desc limit 1")"
[[ "$key_xml" == *encryptedSecret* && "$key_xml" != *"<masterKey"* ]] || fail "chiave Data Protection non cifrata a riposo"
pass "sessione valida, chiave cifrata"

step "Reverse proxy: X-Forwarded-Proto accettato solo dal gateway della rete del progetto"
cookie_from_host="$(curl -fsS -D - -o /dev/null -H 'Content-Type: application/json' -H 'X-Forwarded-Proto: https' \
  --data "$(login_body)" "$BASE/api/auth/login" | grep -i '^set-cookie: .MoscoRun.Session' || true)"
[[ "${cookie_from_host,,}" == *secure* ]] || fail "dal proxy fidato il cookie dovrebbe essere secure: $cookie_from_host"
network="$(docker inspect --format '{{range $k, $v := .NetworkSettings.Networks}}{{$k}}{{end}}' "$(compose ps -q app)")"
cookie_from_peer="$(docker run --rm --network "$network" postgres:17-alpine \
  wget -S -O /dev/null --header 'Content-Type: application/json' --header 'X-Forwarded-Proto: https' \
  --post-data "$(login_body)" http://app:8080/api/auth/login 2>&1 | grep -i 'set-cookie: .MoscoRun.Session' || true)"
[[ -n "$cookie_from_peer" ]] || fail "login dal container vicino non riuscito"
[[ "${cookie_from_peer,,}" != *secure* ]] || fail "un container qualsiasi non deve poter dichiarare HTTPS: $cookie_from_peer"
pass "solo il gateway è un proxy fidato"

# --- Reverse proxy generati da "moscorun.sh proxy-config" e verificati con nginx/Apache reali ---
# Il runner fa da host del proxy; i client "esterni" sono container sulla rete bridge (IP diverso da 127.0.0.1).
readonly PUBLIC_HOST=moscorun.example.it
mkdir -p "$WORK/proxy/acme/.well-known/acme-challenge"
openssl req -x509 -newkey rsa:2048 -nodes -days 1 -subj "/CN=$PUBLIC_HOST" -addext "subjectAltName=DNS:$PUBLIC_HOST" \
  -keyout "$WORK/proxy/key.pem" -out "$WORK/proxy/cert.pem" 2>/dev/null
chmod 644 "$WORK/proxy/key.pem"
echo token-di-prova >"$WORK/proxy/acme/.well-known/acme-challenge/prova"

proxy_config() { env MOSCORUN_PUBLIC_HOST="$PUBLIC_HOST" MOSCORUN_PROXY_LISTEN=8081 "$@" "$SCRIPT" proxy-config "${PROXY_KIND:-nginx}"; }
start_nginx() {
  docker rm -f "$NGINX_TEST" >/dev/null 2>&1 || true
  local args=(--network host -v "$WORK/proxy/nginx.conf:/etc/nginx/conf.d/default.conf:ro"
    -v "$WORK/proxy:/etc/moscorun-test:ro" "$NGINX_IMAGE")
  docker run --rm "${args[@]}" nginx -t || fail "configurazione nginx generata non valida"
  docker run -d --name "$NGINX_TEST" "${args[@]}" >/dev/null
  for _ in $(seq 1 20); do curl -s -o /dev/null http://127.0.0.1:8081/ && return 0; sleep 1; done
  fail "nginx non risponde"
}
# Client esterno: stampa il proprio IP, poi esegue lo script curl ricevuto.
external_client() {
  docker run --rm --add-host "$PUBLIC_HOST:host-gateway" -v "$WORK/proxy/cert.pem:/ca.pem:ro" \
    -e LOGIN="$(login_body)" "$NGINX_IMAGE" sh -c 'echo "CLIENT_IP=$(hostname -i | cut -d" " -f1)"; '"$1"
}
# Login via proxy con IP e schema falsificati, poi una pagina protetta con la sessione ottenuta.
# shellcheck disable=SC2016 # espanso dentro il container client
readonly LOGIN_THEN_DASHBOARD='curl -sS -D - -o /dev/null -c /tmp/c --cacert /ca.pem -w "HTTP_VERSION=%{http_version}\n" \
    -H "Content-Type: application/json" -H "X-Forwarded-For: 198.51.100.66" -H "X-Forwarded-Proto: http" \
    --data "$LOGIN" "$URL/api/auth/login"
  echo "DASHBOARD=$(curl -s -o /dev/null -w "%{http_code}" -b /tmp/c --cacert /ca.pem "$URL/api/v1/dashboard/summary")"'
app_logs_since() { docker logs --since "$1" "$(compose ps -q app)" 2>&1; }

step "nginx termina il TLS (configurazione consigliata): HTTP/2, redirect, ACME, HSTS, IP reale"
since="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
proxy_config MOSCORUN_PROXY_TLS=1 MOSCORUN_PROXY_HTTP3=1 MOSCORUN_PROXY_TLS_LISTEN=8443 \
  MOSCORUN_TLS_CERT=/etc/moscorun-test/cert.pem MOSCORUN_TLS_KEY=/etc/moscorun-test/key.pem \
  MOSCORUN_ACME_ROOT=/etc/moscorun-test/acme >"$WORK/proxy/nginx.conf"
start_nginx
redirect="$(curl -s -o /dev/null -w '%{http_code} %{redirect_url}' -H "Host: $PUBLIC_HOST" http://127.0.0.1:8081/health/live)"
[[ "$redirect" == "301 https://$PUBLIC_HOST/health/live" ]] || fail "HTTP deve reindirizzare a HTTPS: $redirect"
[[ "$(curl -s -H "Host: $PUBLIC_HOST" http://127.0.0.1:8081/.well-known/acme-challenge/prova)" == token-di-prova ]] \
  || fail "la validazione ACME deve restare raggiungibile in HTTP"
out="$(external_client "URL=https://$PUBLIC_HOST:8443; $LOGIN_THEN_DASHBOARD")"
client_ip="$(sed -n 's/^CLIENT_IP=//p' <<<"$out")"
grep -q '^HTTP_VERSION=2' <<<"$out" || fail "atteso HTTP/2: $out"
grep -i '^set-cookie: .MoscoRun.Session' <<<"$out" | grep -qi secure || fail "cookie di sessione non Secure: $out"
grep -qi '^strict-transport-security: max-age=31536000' <<<"$out" || fail "manca HSTS: $out"
grep -qi '^alt-svc: h3=' <<<"$out" || fail "HTTP/3 non annunciato: $out"
grep -q '^DASHBOARD=200' <<<"$out" || fail "sessione non valida via HTTPS: $out"
logs="$(app_logs_since "$since")"
grep -qF "Accesso riuscito da $client_ip" <<<"$logs" || fail "IP reale del client ($client_ip) assente dal registro accessi"
! grep -qF 198.51.100 <<<"$logs" || fail "un X-Forwarded-For del client è finito nell'app"
pass "TLS su nginx: HTTP/2, HTTP/3 annunciato, cookie Secure, HSTS, IP reale $client_ip"

step "nginx: limite di dimensione ampio solo per l'invio degli allenamenti"
head -c 2000000 /dev/zero | tr '\0' ' ' >"$WORK/big.json"
post_big() {
  curl -s -o /dev/null -w '%{http_code}' --cacert "$WORK/proxy/cert.pem" --resolve "$PUBLIC_HOST:8443:127.0.0.1" \
    -H 'Content-Type: application/json' --data-binary @"$WORK/big.json" "https://$PUBLIC_HOST:8443$1"
}
# Senza token l'app risponde 401: la richiesta da 2 MB ha superato nginx.
[[ "$(post_big /api/v1/ingest/workouts)" == 401 ]] || fail "un allenamento da 2 MB deve arrivare all'app"
[[ "$(post_big /api/auth/login)" == 413 ]] || fail "le altre API devono restare limitate a 1 MB"
pass "2 MB accettati per gli allenamenti, rifiutati (413) per le altre API"

step "Frontiera Apache -> nginx in HTTP -> app: la catena X-Forwarded è fidata solo dalla frontiera"
since="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
proxy_config MOSCORUN_EDGE_PROXIES=127.0.0.1 >"$WORK/proxy/nginx.conf"
start_nginx
PROXY_KIND=apache proxy_config MOSCORUN_PROXY_UPSTREAM=http://127.0.0.1:8081 \
  MOSCORUN_TLS_CERT=/etc/moscorun-test/cert.pem MOSCORUN_TLS_KEY=/etc/moscorun-test/key.pem \
  | sed -e 's/^<VirtualHost \*:80>/<VirtualHost *:8082>/' -e 's/^<VirtualHost \*:443>/<VirtualHost *:9443>/' \
    -e "s#https://$PUBLIC_HOST/#https://$PUBLIC_HOST:9443/#" >"$WORK/proxy/apache.conf"
docker run -d --name "$APACHE_TEST" --network host -v "$WORK/proxy:/etc/moscorun-test:ro" "$HTTPD_IMAGE" sh -c '
  conf=/usr/local/apache2/conf/httpd.conf
  sed -i -E -e "s/^#(LoadModule (ssl|socache_shmcb|proxy|proxy_http|headers)_module)/\1/" \
    -e "s/^Listen 80$/Listen 8082\nListen 9443/" "$conf"
  echo "Include /etc/moscorun-test/apache.conf" >>"$conf"
  httpd -t && exec httpd-foreground' >/dev/null
for _ in $(seq 1 20); do curl -sk -o /dev/null https://127.0.0.1:9443/ && break; sleep 1; done
redirect="$(curl -s -o /dev/null -w '%{http_code} %{redirect_url}' -H "Host: $PUBLIC_HOST" http://127.0.0.1:8082/x)"
[[ "$redirect" == "301 https://$PUBLIC_HOST:9443/x" ]] || fail "Apache deve reindirizzare a HTTPS: $redirect $(docker logs "$APACHE_TEST" 2>&1 | tail -5)"
out="$(external_client "URL=https://$PUBLIC_HOST:9443; $LOGIN_THEN_DASHBOARD")"
client_ip="$(sed -n 's/^CLIENT_IP=//p' <<<"$out")"
grep -i '^set-cookie: .MoscoRun.Session' <<<"$out" | grep -qi secure || fail "dietro Apache il cookie deve essere Secure: $out"
grep -qi '^strict-transport-security: max-age=31536000' <<<"$out" || fail "manca HSTS dietro Apache: $out"
grep -q '^DASHBOARD=200' <<<"$out" || fail "sessione non valida attraverso i due proxy: $out"
# Un host della LAN che parla direttamente con nginx non può dichiararsi frontiera.
# shellcheck disable=SC2016 # espanso dentro il container client
lan="$(external_client 'curl -sS -D - -o /dev/null -H "Content-Type: application/json" -H "X-Forwarded-For: 198.51.100.67" \
  -H "X-Forwarded-Proto: https" --data "$LOGIN" http://'"$PUBLIC_HOST"':8081/api/auth/login')"
lan_ip="$(sed -n 's/^CLIENT_IP=//p' <<<"$lan")"
grep -qi '^set-cookie: .MoscoRun.Session' <<<"$lan" || fail "login diretto dalla LAN non riuscito: $lan"
! grep -i '^set-cookie' <<<"$lan" | grep -qi secure || fail "un host della LAN non deve poter dichiarare HTTPS: $lan"
! grep -qi '^strict-transport-security' <<<"$lan" || fail "HSTS non deve comparire in HTTP: $lan"
logs="$(app_logs_since "$since")"
grep -qF "Accesso riuscito da $client_ip" <<<"$logs" || fail "IP reale del client dietro Apache ($client_ip) assente dal registro"
grep -qF "Accesso riuscito da $lan_ip" <<<"$logs" || fail "IP dell'host LAN ($lan_ip) assente dal registro"
! grep -qF 198.51.100 <<<"$logs" || fail "un X-Forwarded-For falsificato è finito nell'app"
docker rm -f "$NGINX_TEST" "$APACHE_TEST" >/dev/null
pass "doppio proxy: HTTPS e IP reale ($client_ip) solo dalla frontiera, LAN ($lan_ip) senza privilegi"

step "Perdita del database: status segnala 'non pronto', poi recupero"
compose stop db
if "$SCRIPT" status; then fail "status dovrebbe fallire con il database fermo"; fi
compose start db
wait_until_ready || fail "app non tornata pronta dopo il riavvio del database"
pass "readiness segue il database"

step "Nuova versione che non raggiunge il database: gate di readiness e rollback automatico"
previous="$(app_image)"
mkdir -p "$WORK/dbless"
cat >"$WORK/dbless/Dockerfile" <<DOCKERFILE
FROM $IMAGE
LABEL org.moscorun.test=dbless
CMD ["--ConnectionStrings:MoscoRun=Host=127.0.0.1;Port=1;Database=x;Username=x;Password=unreachable-db;Timeout=2"]
DOCKERFILE
docker build -q -t moscorun:dbless "$WORK/dbless" >/dev/null
if MOSCORUN_HEALTH_TIMEOUT=30 "$SCRIPT" update dbless; then fail "l'aggiornamento doveva fallire"; fi
[[ "$(app_image)" == "$previous" ]] || fail "dopo il rollback deve girare l'immagine precedente"
"$SCRIPT" status >/dev/null || fail "dopo il rollback l'app deve essere pronta"
session_valid || fail "sessione persa dopo il rollback"
smoke
pass "versione non pronta scartata, precedente ripristinata"

# Compatibilità all'indietro reale: l'ultima release pubblicata deve funzionare sullo schema migrato da questa
# versione, perché è l'immagine a cui tornerebbe un rollback. Saltato finché non esiste una release.
step "Compatibilità N-1: ultima release pubblicata sullo schema di questa versione"
if [[ -n "${PREVIOUS_IMAGE:-}" ]] && { docker image inspect "$PREVIOUS_IMAGE" >/dev/null 2>&1 || docker pull -q "$PREVIOUS_IMAGE" >/dev/null 2>&1; }; then
  docker tag "$PREVIOUS_IMAGE" moscorun:previous
  MOSCORUN_IMAGE=moscorun MOSCORUN_VERSION=previous compose up -d --no-deps app
  wait_until_ready || fail "la release precedente non è pronta sullo schema nuovo: la migrazione non è expand"
  smoke
  compose up -d --no-deps app
  wait_until_ready || fail "la versione corrente non torna pronta"
  pass "$PREVIOUS_IMAGE compatibile con lo schema nuovo"
else
  printf '::notice::Nessuna release precedente disponibile (%s): verifica N-1 saltata\n' "${PREVIOUS_IMAGE:-non indicata}"
  pass "saltato"
fi

step "Backup e ripristino con dati reali"
# Fixture scientifiche sintetiche, solo sullo stack isolato di CI.
sql "insert into moscorun.scientific_observations (owner_id,id,metric,unit,value,time,available_at,source,method,input_ids)
     select id,'00000000-0000-4000-8000-000000000001','heartRate','bpm',145,now(),now(),'ci-backup','Measured','{}'
     from moscorun.\"AspNetUsers\" order by id limit 1" >/dev/null
sql "insert into moscorun.scientific_observations (owner_id,id,metric,unit,value,time,available_at,source,method,derivation_version,input_ids)
     select owner_id,'00000000-0000-4000-8000-000000000002','ciDerived','native',null,time,available_at,'ci-backup','Derived','v1',array[id]
     from moscorun.scientific_observations where source='ci-backup' and id='00000000-0000-4000-8000-000000000001'" >/dev/null
scientific_count="$(sql "select count(*) from moscorun.scientific_observations where source='ci-backup'")"
[[ "$scientific_count" == 2 ]] || fail "fixture scientifiche mancanti"
science_before="$(sql "select md5(string_agg(to_jsonb(o)::text, ',' order by owner_id,id)) from moscorun.scientific_observations o")"
"$SCRIPT" backup
backup="$(<"$DEPLOY_DIR/.state/last-backup")"
weighings="$(sql "select count(*) from moscorun.weighings")"
users="$(sql "select count(*) from moscorun.\"AspNetUsers\"")"
((weighings > 0)) || fail "servono pesate (create dagli E2E) per verificare il ripristino"
sql "delete from moscorun.weighings" >/dev/null
sql "delete from moscorun.scientific_observations" >/dev/null
[[ "$(sql "select count(*) from moscorun.scientific_observations")" == 0 ]] || fail "simulazione perdita scientifica non riuscita"
[[ "$(sql "select count(*) from moscorun.weighings")" == 0 ]] || fail "simulazione della perdita non riuscita"
echo RIPRISTINA | "$SCRIPT" restore "$backup"
[[ "$(sql "select count(*) from moscorun.weighings")" == "$weighings" ]] || fail "pesate non ripristinate"
[[ "$(sql "select count(*) from moscorun.\"AspNetUsers\"")" == "$users" ]] || fail "utenti non ripristinati"
[[ "$(sql "select md5(string_agg(to_jsonb(o)::text, ',' order by owner_id,id)) from moscorun.scientific_observations o")" == "$science_before" ]] || fail "osservazioni scientifiche o lineage non ripristinati"
session_valid || fail "sessione non valida dopo il ripristino"
smoke
pass "ripristinate $weighings pesate e $users utenti"

printf '\nTutti gli scenari superati.\n'
