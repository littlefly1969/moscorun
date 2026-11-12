# Sviluppo locale

## Requisiti

- .NET SDK 10 (versione fissata in `backend/global.json`)
- Node.js 22 (`web/.nvmrc`)
- Docker (PostgreSQL di sviluppo e test di integrazione)

## Struttura

```
backend/            API ASP.NET Core (.NET 10) e test
  src/MoscoRun.Domain           regole di dominio pure (pesate, dispositivi, tendenze)
  src/MoscoRun.Infrastructure   EF Core, PostgreSQL, Identity, migrazioni
  src/MoscoRun.Api              endpoint, autenticazione, comandi migrate/healthcheck
  tests/MoscoRun.UnitTests      xUnit v3
  tests/MoscoRun.IntegrationTests  WebApplicationFactory + Testcontainers (PostgreSQL reale)
web/                cruscotto React + test Vitest + E2E Playwright
contracts/          contratto OpenAPI generato dal backend (fonte dei tipi del cruscotto)
deploy/             compose, script di aggiornamento, unità systemd
Dockerfile          immagine unica API + cruscotto
```

## Avvio

```bash
# 1. Database di sviluppo (password solo locale, scelta da te)
docker run -d --name moscorun-dev-db -p 5432:5432 \
  -e POSTGRES_DB=moscorun -e POSTGRES_USER=moscorun -e POSTGRES_PASSWORD='<password-locale>' postgres:17-alpine

# 2. Segreti di sviluppo fuori dal repository (dotnet user-secrets)
cd backend/src/MoscoRun.Api
dotnet user-secrets set "ConnectionStrings:MoscoRun" "Host=localhost;Database=moscorun;Username=moscorun;Password=<password-locale>"
dotnet user-secrets set "Bootstrap:AdminEmail" "tu@example.it"
dotnet user-secrets set "Bootstrap:AdminPassword" "<almeno-12-caratteri>"

# 3. Migrazioni + utente amministratore, poi API su http://localhost:5080
dotnet run -- migrate
dotnet run

# 4. Cruscotto su http://localhost:5173 (proxy verso l'API)
cd ../../../web && npm ci && npm run dev
```

## Comandi di qualità

| Cosa | Backend | Web |
| --- | --- | --- |
| Formattazione | `dotnet format` | `npm run format` |
| Analisi statica | `dotnet build` (avvisi = errori) | `npm run lint`, `npm run typecheck` |
| Test | `dotnet test` | `npm test`, `npm run test:coverage` |
| E2E | — | `E2E_ADMIN_PASSWORD=… npm run e2e` contro uno stack avviato |

## Modificare l'API

1. Cambiare endpoint o contratti in `backend/src/MoscoRun.Api`.
2. `dotnet build` rigenera `contracts/moscorun-api.json`.
3. `cd web && npm run api:generate` rigenera i tipi TypeScript.
4. La CI fallisce se contratto o tipi non sono committati.

## Modificare il database

```bash
cd backend
dotnet tool restore
dotnet ef migrations add NomeMigrazione -p src/MoscoRun.Infrastructure -s src/MoscoRun.Api -o Persistence/Migrations
```

Le migrazioni devono restare compatibili con la versione precedente dell'applicazione (aggiungere prima, rimuovere in una release successiva), perché il rollback automatico riporta l'immagine precedente ma non lo schema.

## Commit e rilasci

Messaggi in formato [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, `docs:`, `deps:`…). release-please calcola versione e CHANGELOG; l'unione della PR di release pubblica l'immagine.
