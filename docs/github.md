# GitHub nel piano gratuito

Il repository resta **privato** (decisione del 12/11/2026): la release `samsung-sdk-local` e un oggetto Git orfano contengono l'SDK Samsung, che non può essere ridistribuito. Le funzioni di sicurezza a pagamento per i repository privati sono sostituite da controlli equivalenti in CI.

## Cosa fa GitHub

| Funzione | Come | Costo |
| --- | --- | --- |
| CI su ogni PR e su main | `ci.yml`: build, formattazione, analisi statica, unit + integrazione con PostgreSQL reale, test e copertura del cruscotto, E2E Playwright sull'immagine reale, scenari operativi dello script di deploy | Inclusa (2000 min/mese) |
| Analisi statica di sicurezza | Semgrep OSS nella CI (regole fissate a un commit), al posto di CodeQL | Gratis |
| Ricerca di segreti | gitleaks su tutta la storia Git in ogni CI | Gratis |
| Vulnerabilità delle dipendenze | `dotnet restore` con NuGet audit, `npm audit`, Dependabot alerts | Gratis |
| Vulnerabilità dell'immagine | Trivy blocca la CI su HIGH/CRITICAL risolvibili | Gratis |
| Aggiornamenti dipendenze | Dependabot settimanale, raggruppato (NuGet, npm, Actions, Docker) | Gratis |
| Versioni e CHANGELOG | release-please da Conventional Commits | Gratis |
| Gate di rilascio | `release.yml` parte solo dopo una CI riuscita su main e verifica la CI verde sul commit esatto del tag prima di pubblicare | Gratis |
| Immagini container | GHCR, multi-arch, con SBOM e provenance; pulizia settimanale delle vecchie versioni | Gratis |
| Azioni di terze parti | Fissate per SHA del commit, aggiornate da Dependabot | — |
| CodeQL | `codeql.yml`, attivo automaticamente solo se il repository diventa pubblico | Gratis solo se pubblico |

Non disponibili su repository privati nel piano Free: branch protection/rulesets applicati (il ramo `main` risulta `protected=false`), secret scanning e push protection di GitHub, CodeQL, GitHub Pages.

Cosa li sostituisce:

- **Nessuna pubblicazione non verificata:** anche se un commit arrivasse su `main` con la CI rossa, `release.yml` non pubblica: parte solo da una CI riuscita e ricontrolla lo SHA del tag. Il server installa solo da GHCR.
- **CodeQL → Semgrep** in CI, più analizzatori .NET/ESLint con avvisi come errori, gitleaks, Trivy.
- **Procedura di merge** (regola di lavoro, non imposta da GitHub):
  1. Ogni modifica passa da una PR verso `main`, mai push diretti.
  2. Si unisce solo con tutti i check verdi sull'ultimo commit della PR e dopo la review.
  3. Solo *Squash merge*, titolo in formato Conventional Commits.
  4. Se la CI su `main` dopo l'unione fallisce, si corregge con una nuova PR: nessuna release parte finché non torna verde.

Se il repository diventasse pubblico o passasse a un piano a pagamento: attivare un ruleset su `main` con PR obbligatoria e check richiesti (`Backend (.NET)`, `Web (React)`, `Segreti e script`, `Immagine, scansione ed E2E`), e CodeQL si attiva da solo.

## Impostazioni da attivare una volta (Settings del repository)

1. **Actions → General → Workflow permissions:** spuntare *Allow GitHub Actions to create and approve pull requests* (serve a release-please).
2. **Code security:** attivare *Dependency graph*, *Dependabot alerts* e *Dependabot security updates*.
3. **General → Pull Requests:** consentire solo *Squash merging* con titolo della PR come messaggio; attivare *Automatically delete head branches*.
4. Facoltativo: secret `RELEASE_PLEASE_TOKEN` (token fine-grained con permessi Contents e Pull requests in scrittura su questo repository) per far partire la CI anche sulle PR di release.

## Flusso di rilascio

1. Le PR vengono unite in main con titolo Conventional Commits.
2. Terminata con successo la CI su main, release-please apre/aggiorna la PR "chore: release x.y.z" con il CHANGELOG.
3. Unendo quella PR e dopo la CI verde su quel commit nascono tag e GitHub Release, e l'immagine `ghcr.io/littlefly1969/moscorun:x.y.z` (+ `x.y`, `x`, `latest`).
4. Se la pubblicazione è stata bloccata dal gate (CI non ancora verde sul commit del tag), si rilancia a mano *Actions → Release → Run workflow* indicando il tag: il gate viene ripetuto.
5. Il server la installa con `./moscorun.sh update` (il timer notturno resta spento fino alla validazione sul server reale).
