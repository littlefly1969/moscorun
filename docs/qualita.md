# Qualità e test

Ogni modifica passa dagli stessi controlli in locale e in CI. Una PR non va unita con la CI rossa.

## Livelli di test

| Livello | Strumenti | Cosa garantisce |
| --- | --- | --- |
| Unità (dominio) | xUnit v3, AwesomeAssertions | Validazione pesate, arrotondamenti, idempotenza, tendenze, token, freschezza dei dispositivi |
| Integrazione | WebApplicationFactory + Testcontainers PostgreSQL 17 | API reale su database reale: autenticazione, isolamento fra utenti, concorrenza, migrazioni (regola expand/contract), avvio con segreti errati, chiavi Data Protection cifrate, proxy fidati, registro accessi con IP reale e senza email, readiness con database irraggiungibile, ultimo peso oltre la finestra del trend |
| Componenti web | Vitest, Testing Library, MSW | Pagine del cruscotto con risposte HTTP simulate, errori, stati vuoti; soglia di copertura 80% |
| End-to-end | Playwright (desktop e mobile) + axe-core | Immagine container reale installata con lo script di produzione: accesso, registrazione dispositivo, invio pesata, conferma "È mia", cruscotto, revoca; nessuna violazione WCAG 2.1 AA, nessuno scorrimento orizzontale |
| Distribuzione | `moscorun.sh` + `deploy/tests/scenarios.sh` in CI | Installazione da zero, migrazioni, readiness, smoke delle API principali; ultima release pubblicata sullo schema nuovo (N-1); riavvio con sessioni conservate, solo il gateway come proxy fidato, reverse proxy generati da `.env` provati con nginx e Apache reali (TLS con HTTP/2, redirect, ACME, HSTS, catena frontiera → nginx, IP e schema non falsificabili da Internet né dalla LAN), perdita e ritorno del database, versione non pronta scartata con rollback automatico, backup → perdita dati → ripristino |

## Analisi statica

- .NET: `TreatWarningsAsErrors`, analizzatori `latest-recommended`, Meziantou.Analyzer, stile applicato in build, `dotnet format`.
- TypeScript: `strict` con `noUncheckedIndexedAccess` ed `exactOptionalPropertyTypes`; ESLint `strictTypeChecked` + accessibilità (jsx-a11y) + regole degli hook React; Prettier.
- Shell: ShellCheck. Workflow: actionlint in locale.
- Sicurezza: Semgrep OSS (C#, TypeScript/React, Dockerfile, GitHub Actions, Bash) con regole fissate a un commit, al posto di CodeQL non disponibile sui repository privati del piano Free; gitleaks; Trivy sull'immagine; audit NuGet e npm.

## Scelte che prevengono errori

- **Idempotenza delle pesate:** indice unico `(dispositivo, sourceEventId)`; un reinvio identico restituisce 200, valori diversi 409. Testata anche con 12 invii concorrenti.
- **Precisione temporale:** gli istanti sono normalizzati al microsecondo, la precisione di PostgreSQL; senza questa regola un reinvio identico risultava un conflitto (difetto trovato dai test di integrazione).
- **Contratto unico:** OpenAPI generato dal backend, tipi TypeScript generati dal contratto, entrambi verificati in CI.
- **Proprietà dei dati:** l'ambito utente deriva solo dalle credenziali; i test verificano che un altro utente riceva 404.
- **Token dei dispositivi:** 256 bit casuali, salvati solo come SHA-256 e confrontati in tempo costante, mostrati una sola volta, revocabili.
- **Accesso:** cookie HttpOnly e SameSite=Strict, blocco dopo 5 tentativi, limite di richieste per IP.
- **Freschezza:** "ultimo contatto" (`lastSeenAt`, qualunque richiesta autenticata) e "ultima pesata ricevuta" (`lastIngestedAt`, solo acquisizioni accettate) sono grandezze distinte; il cruscotto colora la freschezza sulla seconda.
- **Attribuzione:** una pesata "da confermare" offre subito "È mia" e "Non è mia".

## Cosa non è coperto da questi test

- Agente Bluetooth della bilancia e sincronizzazione Android → server: **non ancora implementati** (vedi [README](../README.md)).
- Server reale (convivenza con i siti e i servizi già presenti, Bluetooth): vedi "Validazione sul server reale" in [deploy-linux.md](deploy-linux.md).
