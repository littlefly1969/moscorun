# Qualità e test

Ogni modifica passa dagli stessi controlli in locale e in CI. Una PR non va unita con la CI rossa.

## Livelli di test

| Livello | Strumenti | Cosa garantisce |
| --- | --- | --- |
| Unità (dominio) | xUnit v3, AwesomeAssertions | Validazione pesate, arrotondamenti, idempotenza, tendenze, token |
| Integrazione | WebApplicationFactory + Testcontainers PostgreSQL 17 | API reale su database reale: autenticazione, isolamento fra utenti, concorrenza, migrazioni, avvio con segreti errati |
| Componenti web | Vitest, Testing Library, MSW | Pagine del cruscotto con risposte HTTP simulate, errori, stati vuoti; soglia di copertura 80% |
| End-to-end | Playwright (desktop e mobile) + axe-core | Immagine container reale installata con lo script di produzione: accesso, registrazione dispositivo, invio pesata, cruscotto, revoca; nessuna violazione WCAG 2.1 AA |
| Distribuzione | Script `moscorun.sh` eseguito in CI | Installazione da zero, migrazioni, healthcheck |

## Analisi statica

- .NET: `TreatWarningsAsErrors`, analizzatori `latest-recommended`, Meziantou.Analyzer, stile applicato in build, `dotnet format`.
- TypeScript: `strict` con `noUncheckedIndexedAccess` ed `exactOptionalPropertyTypes`; ESLint `strictTypeChecked` + accessibilità (jsx-a11y) + regole degli hook React; Prettier.
- Shell: ShellCheck. Workflow: actionlint in locale.

## Scelte che prevengono errori

- **Idempotenza delle pesate:** indice unico `(dispositivo, sourceEventId)`; un reinvio identico restituisce 200, valori diversi 409. Testata anche con 12 invii concorrenti.
- **Precisione temporale:** gli istanti sono normalizzati al microsecondo, la precisione di PostgreSQL; senza questa regola un reinvio identico risultava un conflitto (difetto trovato dai test di integrazione).
- **Contratto unico:** OpenAPI generato dal backend, tipi TypeScript generati dal contratto, entrambi verificati in CI.
- **Proprietà dei dati:** l'ambito utente deriva solo dalle credenziali; i test verificano che un altro utente riceva 404.
- **Token dei dispositivi:** 256 bit casuali, salvati solo come SHA-256 e confrontati in tempo costante, mostrati una sola volta, revocabili.
- **Accesso:** cookie HttpOnly e SameSite=Strict, blocco dopo 5 tentativi, limite di richieste per IP.
