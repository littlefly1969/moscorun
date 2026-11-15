#!/usr/bin/env bash
# Test di "moscorun.sh check": la validazione di .env ferma le configurazioni incoerenti prima di toccare
# immagini, container o database. Non richiede Docker.
set -Eeuo pipefail

DEPLOY_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
cp "$DEPLOY_DIR/moscorun.sh" "$WORK/"
failures=0

# run_check <contenuto .env>: stampa l'output, restituisce il codice d'uscita.
run_check() {
  printf '%s\n' "$1" >"$WORK/.env"
  chmod 600 "$WORK/.env"
  env -i PATH="$PATH" HOME="$WORK" bash "$WORK/moscorun.sh" check 2>&1
}

expect_ok() {
  local name="$1" out
  if out="$(run_check "$2")"; then echo "✔ $name"; else echo "✘ $name: atteso successo, ottenuto: $out"; failures=$((failures + 1)); fi
}

expect_error() {
  local name="$1" env="$2" message="$3" out
  if out="$(run_check "$env")"; then
    echo "✘ $name: atteso errore, ottenuto successo: $out"; failures=$((failures + 1))
  elif [[ "$out" != *"$message"* ]]; then
    echo "✘ $name: messaggio atteso '$message', ottenuto: $out"; failures=$((failures + 1))
  else
    echo "✔ $name"
  fi
}

readonly PW='POSTGRES_PASSWORD=segreto-di-prova'

expect_ok "esempio compilato: database incluso" "$(cat "$DEPLOY_DIR/.env.example")
$PW"
expect_ok "PostgreSQL sull'host" "POSTGRES_HOST=host.docker.internal
$PW"
expect_ok "profilo in lista con altri" "COMPOSE_PROFILES=altro,bundled-db
POSTGRES_HOST=db
$PW"
expect_error "db senza profilo (caso reale del primo update)" "POSTGRES_HOST=db
$PW" "parte solo con COMPOSE_PROFILES=bundled-db"
expect_error "profilo con un altro host" "COMPOSE_PROFILES=bundled-db
POSTGRES_HOST=host.docker.internal
$PW" "raggiungibile solo come POSTGRES_HOST=db"
expect_error "profilo simile ma diverso" "COMPOSE_PROFILES=bundled-db-old
POSTGRES_HOST=db
$PW" "parte solo con COMPOSE_PROFILES=bundled-db"
expect_error "localhost dentro il container" "POSTGRES_HOST=localhost
$PW" "host.docker.internal"
expect_error "loopback IPv4" "POSTGRES_HOST=127.0.0.1
$PW" "host.docker.internal"
expect_error "host mancante" "$PW" "POSTGRES_HOST mancante"
expect_error "password mancante" "COMPOSE_PROFILES=bundled-db
POSTGRES_HOST=db" "POSTGRES_PASSWORD mancante"
expect_error "porta non numerica" "POSTGRES_HOST=host.docker.internal
POSTGRES_PORT=abc
$PW" "POSTGRES_PORT non è una porta valida"
expect_error "porta fuori intervallo" "POSTGRES_HOST=host.docker.internal
MOSCORUN_PORT=70000
$PW" "MOSCORUN_PORT non è una porta valida"

readonly DB='COMPOSE_PROFILES=bundled-db
POSTGRES_HOST=db'
# admin <password>: righe dell'amministratore da creare (valori di prova, nessun segreto reale).
admin() { printf 'MOSCORUN_ADMIN_EMAIL=admin@example.it\nMOSCORUN_ADMIN_%s=%s' PASSWORD "$1"; }
expect_ok "amministratore con password conforme" "$DB
$PW
$(admin Conforme-2026x)"
expect_ok "amministratore già creato: credenziali svuotate" "$DB
$PW
MOSCORUN_ADMIN_EMAIL=
MOSCORUN_ADMIN_PASSWORD="
expect_error "password senza maiuscola (caso reale del primo update)" "$DB
$PW
$(admin senza-maiuscola-9)" "deve contenere: una lettera maiuscola"
expect_error "password corta e senza cifra" "$DB
$PW
$(admin Corta)" "almeno 12 caratteri, una cifra"
expect_error "email senza password" "$DB
$PW
$(admin '')" "almeno 12 caratteri"

printf '%s\n' "POSTGRES_HOST=db" "COMPOSE_PROFILES=bundled-db" "$PW" >"$WORK/.env"
chmod 644 "$WORK/.env"
if out="$(env -i PATH="$PATH" HOME="$WORK" bash "$WORK/moscorun.sh" check 2>&1)" || [[ "$out" != *"chmod 600"* ]]; then
  echo "✘ .env leggibile da altri: atteso errore chmod 600, ottenuto: $out"; failures=$((failures + 1))
else
  echo "✔ .env leggibile da altri rifiutato"
fi

if ((failures > 0)); then echo "::error::$failures verifiche di moscorun.sh check fallite"; exit 1; fi
echo "Tutte le verifiche di moscorun.sh check superate."
