#!/usr/bin/env bash
# Test di release-plan.sh su repository git temporanei (eseguiti in CI, job "Segreti e script").
set -Eeuo pipefail

PLAN="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)/release-plan.sh"
readonly PLAN
failures=0
REPOS=()
trap 'rm -rf "${REPOS[@]}"' EXIT

new_repo() {
  REPO="$(mktemp -d)"
  REPOS+=("$REPO")
  git -C "$REPO" init -q -b main
  git -C "$REPO" config user.email test@example.invalid
  git -C "$REPO" config user.name test
  commit "chore: inizio"
}
commit() { git -C "$REPO" commit -q --allow-empty -m "$1" ${2:+-m "$2"}; }
plan() { (cd "$REPO" && "$PLAN" HEAD "$REPO/notes.md"); }
expect() {
  local name="$1" expected="$2" actual
  actual="$(plan | tr '\n' ' ')"
  if [[ "$actual" == "$expected"* ]]; then
    printf '✔ %s\n' "$name"
  else
    printf '✘ %s\n  atteso: %s\n  ottenuto: %s\n' "$name" "$expected" "$actual"
    failures=$((failures + 1))
  fi
}
notes_contain() {
  if grep -qF -- "$2" "$REPO/notes.md"; then printf '✔ %s\n' "$1"; else
    printf '✘ %s: manca "%s" in\n%s\n' "$1" "$2" "$(cat "$REPO/notes.md")"; failures=$((failures + 1)); fi
}
notes_lack() {
  if ! grep -qF -- "$2" "$REPO/notes.md"; then printf '✔ %s\n' "$1"; else
    printf '✘ %s: non doveva esserci "%s"\n' "$1" "$2"; failures=$((failures + 1)); fi
}

new_repo
commit "docs: guida"
commit "ci: pipeline"
expect "solo commit non rilasciabili: nessuna release" "release=false version= tag= previous="

new_repo
commit "fix: errore"
expect "prima release con fix: patch da 0.1.0" "release=true version=0.1.1 tag=v0.1.1 previous="

new_repo
commit "fix: errore"
commit "feat(api): nuova funzione"
commit "deps: aggiornamenti"
expect "feat prevale su fix: minor" "release=true version=0.2.0 tag=v0.2.0"
notes_contain "note: sezione Novità" "### Novità"
notes_contain "note: testo senza tipo né ambito" "- nuova funzione ("
notes_contain "note: sezione Correzioni" "### Correzioni"
notes_contain "note: sezione Dipendenze" "### Dipendenze"
notes_lack "note: chore nascosto" "inizio"

new_repo
commit "feat: prima"
git -C "$REPO" tag v0.2.0
commit "fix: dopo il tag"
expect "si parte dall'ultimo tag" "release=true version=0.2.1 tag=v0.2.1 previous=v0.2.0"
notes_lack "note: solo i commit dopo il tag" "prima"

new_repo
git -C "$REPO" tag v0.4.2
commit "feat!: rimozione API"
expect "breaking prima della 1.0: minor" "release=true version=0.5.0"
notes_contain "note: sezione incompatibili" "### Modifiche incompatibili"

new_repo
git -C "$REPO" tag v1.4.2
commit "fix: cambio" "BREAKING CHANGE: formato diverso"
expect "breaking nel corpo dopo la 1.0: major" "release=true version=2.0.0 tag=v2.0.0 previous=v1.4.2"

new_repo
git -C "$REPO" tag v0.2.0
git -C "$REPO" checkout -q -b ramo
commit "feat: dal ramo"
git -C "$REPO" checkout -q main
git -C "$REPO" merge -q --no-ff ramo -m "Merge pull request #9 from ramo"
expect "i commit dentro un merge contano, il merge no" "release=true version=0.3.0"
notes_lack "note: messaggio di merge escluso" "Merge pull request"

new_repo
commit "Aggiornamento senza formato"
commit "feature: tipo sconosciuto"
expect "commit non convenzionali ignorati" "release=false"

if ((failures > 0)); then echo "$failures test falliti" >&2; exit 1; fi
echo "Tutti i test di release-plan superati."
