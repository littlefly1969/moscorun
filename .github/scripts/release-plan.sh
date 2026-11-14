#!/usr/bin/env bash
# Piano di rilascio da Conventional Commits, senza PR di release.
#
#   release-plan.sh <commit> <file-note>
#
# Confronta <commit> con l'ultimo tag vX.Y.Z suo antenato e stampa su stdout, in formato GITHUB_OUTPUT:
#   release=true|false   version=X.Y.Z   tag=vX.Y.Z   previous=<tag precedente o vuoto>
# Scrive le note di rilascio (sezioni in italiano) in <file-note>.
#
# Regole (come release-please con bump-minor-pre-major):
#   - breaking change ("tipo!:" o "BREAKING CHANGE:" nel corpo): major, oppure minor finché la major è 0
#   - feat: minor        - fix, perf, security, deps, revert: patch
#   - docs, refactor, test, ci, chore, build, style e commit non convenzionali: nessun rilascio
# Senza tag precedenti si parte da INITIAL_VERSION (predefinita 0.1.0). I commit di merge sono ignorati:
# contano i commit che contengono le modifiche.
set -Eeuo pipefail

readonly TARGET="${1:?commit di destinazione}"
readonly NOTES="${2:?file delle note}"
readonly INITIAL_VERSION="${INITIAL_VERSION:-0.1.0}"
readonly SEMVER_RE='^([0-9]+)\.([0-9]+)\.([0-9]+)$'
readonly COMMIT_RE='^([a-z]+)(\([^)]*\))?(!)?: (.+)$'

sha="$(git rev-parse --verify "$TARGET^{commit}")"
previous="$(git describe --tags --abbrev=0 --match 'v[0-9]*.[0-9]*.[0-9]*' "$sha" 2>/dev/null || true)"
base="${previous#v}"
[[ -n "$base" ]] || base="$INITIAL_VERSION"
[[ "$base" =~ $SEMVER_RE ]] || { echo "versione di partenza non valida: $base" >&2; exit 2; }
major="${BASH_REMATCH[1]}" minor="${BASH_REMATCH[2]}" patch="${BASH_REMATCH[3]}"

declare -A sections=()
bump=0 # 0 nessuno, 1 patch, 2 minor, 3 major
range="${previous:+$previous..}$sha"
while IFS=$'\x1f' read -r -d $'\x1e' hash subject body; do
  hash="${hash#$'\n'}"
  [[ "$subject" =~ $COMMIT_RE ]] || continue
  type="${BASH_REMATCH[1]}" breaking="${BASH_REMATCH[3]}" text="${BASH_REMATCH[4]}"
  [[ "$body" == *"BREAKING CHANGE:"* || "$body" == *"BREAKING-CHANGE:"* ]] && breaking="!"
  level=0
  case "$type" in
    feat) level=2 ;;
    fix | perf | security | deps | revert) level=1 ;;
  esac
  if [[ -n "$breaking" ]]; then level=3; fi
  ((level > bump)) && bump=$level
  case "$type" in
    feat) section="Novità" ;;
    fix) section="Correzioni" ;;
    perf) section="Prestazioni" ;;
    security) section="Sicurezza" ;;
    deps) section="Dipendenze" ;;
    docs) section="Documentazione" ;;
    revert) section="Annullamenti" ;;
    *) section="" ;;
  esac
  [[ -n "$breaking" ]] && section="Modifiche incompatibili"
  [[ -n "$section" ]] && sections["$section"]+="- ${text} (${hash:0:7})"$'\n'
done < <(git log --no-merges --reverse --format='%H%x1f%s%x1f%b%x1e' "$range")

case "$bump" in
  0) printf 'release=false\nversion=\ntag=\nprevious=%s\n' "$previous"; : >"$NOTES"; exit 0 ;;
  1) patch=$((patch + 1)) ;;
  2) minor=$((minor + 1)) patch=0 ;;
  3) if ((major == 0)); then minor=$((minor + 1)) patch=0; else major=$((major + 1)) minor=0 patch=0; fi ;;
esac
version="$major.$minor.$patch"

{
  for section in "Modifiche incompatibili" "Novità" "Correzioni" "Sicurezza" "Prestazioni" "Dipendenze" "Annullamenti" "Documentazione"; do
    [[ -n "${sections[$section]:-}" ]] || continue
    printf '### %s\n\n%s\n' "$section" "${sections[$section]}"
  done
} >"$NOTES"
printf 'release=true\nversion=%s\ntag=v%s\nprevious=%s\n' "$version" "$version" "$previous"
