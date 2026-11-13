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
trap 'rm -rf "$WORK"' EXIT

set -a
# shellcheck disable=SC1091
source "$DEPLOY_DIR/.env"
set +a
readonly BASE="http://127.0.0.1:${MOSCORUN_PORT:-8085}"
readonly IMAGE="${MOSCORUN_IMAGE}:${MOSCORUN_VERSION}"
: "${E2E_ADMIN_EMAIL:?}" "${E2E_ADMIN_PASSWORD:?}"

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
wait_until_ready() {
  for _ in $(seq 1 40); do "$SCRIPT" status >/dev/null 2>&1 && return 0; sleep 3; done
  return 1
}

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
pass "versione non pronta scartata, precedente ripristinata"

step "Backup e ripristino con dati reali"
"$SCRIPT" backup
backup="$(<"$DEPLOY_DIR/.state/last-backup")"
weighings="$(sql "select count(*) from moscorun.weighings")"
users="$(sql "select count(*) from moscorun.\"AspNetUsers\"")"
((weighings > 0)) || fail "servono pesate (create dagli E2E) per verificare il ripristino"
sql "delete from moscorun.weighings" >/dev/null
[[ "$(sql "select count(*) from moscorun.weighings")" == 0 ]] || fail "simulazione della perdita non riuscita"
echo RIPRISTINA | "$SCRIPT" restore "$backup"
[[ "$(sql "select count(*) from moscorun.weighings")" == "$weighings" ]] || fail "pesate non ripristinate"
[[ "$(sql "select count(*) from moscorun.\"AspNetUsers\"")" == "$users" ]] || fail "utenti non ripristinati"
session_valid || fail "sessione non valida dopo il ripristino"
pass "ripristinate $weighings pesate e $users utenti"

printf '\nTutti gli scenari superati.\n'
