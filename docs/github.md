# GitHub nel piano gratuito

Il repository resta **privato** (decisione del 12/11/2026): la release `samsung-sdk-local` e un oggetto Git orfano contengono l'SDK Samsung, che non può essere ridistribuito. Le funzioni di sicurezza a pagamento per i repository privati sono sostituite da controlli equivalenti in CI.

## Cosa fa GitHub

| Funzione | Come | Costo |
| --- | --- | --- |
| CI su ogni PR e su main | `ci.yml`: build, formattazione, analisi statica, unit + integrazione con PostgreSQL reale, test e copertura del cruscotto, E2E Playwright sull'immagine reale | Inclusa (2000 min/mese) |
| Ricerca di segreti | gitleaks su tutta la storia Git in ogni CI | Gratis |
| Vulnerabilità delle dipendenze | `dotnet restore` con NuGet audit, `npm audit`, Dependabot alerts | Gratis |
| Vulnerabilità dell'immagine | Trivy blocca la CI su HIGH/CRITICAL risolvibili | Gratis |
| Aggiornamenti dipendenze | Dependabot settimanale, raggruppato (NuGet, npm, Actions, Docker) | Gratis |
| Versioni e CHANGELOG | release-please da Conventional Commits | Gratis |
| Immagini container | GHCR, multi-arch, con SBOM e provenance; pulizia settimanale delle vecchie versioni | Gratis |
| Azioni di terze parti | Fissate per SHA del commit, aggiornate da Dependabot | — |
| CodeQL | `codeql.yml`, attivo automaticamente solo se il repository diventa pubblico | Gratis solo se pubblico |

Non disponibili su repository privati nel piano Free: branch protection/rulesets applicati, secret scanning e push protection di GitHub, CodeQL, GitHub Pages. Le alternative sopra coprono i rischi principali; la disciplina "solo PR verso main" resta una regola di lavoro.

## Impostazioni da attivare una volta (Settings del repository)

1. **Actions → General → Workflow permissions:** spuntare *Allow GitHub Actions to create and approve pull requests* (serve a release-please).
2. **Code security:** attivare *Dependency graph*, *Dependabot alerts* e *Dependabot security updates*.
3. **General → Pull Requests:** consentire solo *Squash merging* con titolo della PR come messaggio; attivare *Automatically delete head branches*.
4. Facoltativo: secret `RELEASE_PLEASE_TOKEN` (token fine-grained con permessi Contents e Pull requests in scrittura su questo repository) per far partire la CI anche sulle PR di release.

## Flusso di rilascio

1. Le PR vengono unite in main con titolo Conventional Commits.
2. release-please apre/aggiorna la PR "chore: release x.y.z" con il CHANGELOG.
3. Unendo quella PR nascono tag e GitHub Release, e l'immagine `ghcr.io/littlefly1969/moscorun:x.y.z` (+ `x.y`, `x`, `latest`).
4. Il server la installa con `./moscorun.sh update` o con il timer notturno.
