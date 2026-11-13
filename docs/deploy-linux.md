# Installazione e aggiornamento sul server Linux

Procedura verificata automaticamente in CI su uno stack reale (installazione con questo script, E2E, scenari operativi: riavvio, reverse proxy, perdita del database, rollback su readiness, backup e ripristino con dati). **Non è ancora stata eseguita sul server reale**: restano da rilevare i dati dell'inventario in fondo e da ripetere le verifiche elencate in "Validazione sul server reale". Fino ad allora l'aggiornamento automatico resta spento.

## Come funziona

- Un'unica immagine `ghcr.io/littlefly1969/moscorun` contiene API e cruscotto React, gira come utente non root su filesystem in sola lettura ed espone la porta 8080 solo su `127.0.0.1` dell'host.
- Il reverse proxy già presente (Apache) pubblica l'HTTPS: esempio in [deploy/apache-moscorun.conf.example](../deploy/apache-moscorun.conf.example).
- Topologia: Apache sull'host → `127.0.0.1:MOSCORUN_PORT` → container. Docker inoltra la connessione dal gateway della rete del progetto (`MOSCORUN_GATEWAY`, predefinito `172.30.85.1` sulla subnet `172.30.85.0/24`): è l'**unico** indirizzo, oltre al loopback, da cui l'app accetta `X-Forwarded-For/Proto/Host`. Nessuna intera rete privata è considerata fidata. Se la subnet è già usata sul server, cambiare `MOSCORUN_SUBNET` e `MOSCORUN_GATEWAY` in `.env`.
- Il database è l'istanza PostgreSQL esistente (database e utente dedicati) oppure, in alternativa, il PostgreSQL incluso nel compose (`COMPOSE_PROFILES=bundled-db`).
- Ogni release crea l'immagine per amd64 e arm64: va bene qualunque CPU del server.

## Prima installazione

```bash
sudo mkdir -p /opt/moscorun && cd /opt/moscorun
# Copiare da deploy/: compose.yaml, moscorun.sh, .env.example, systemd/
cp .env.example .env && chmod 600 .env && nano .env

# Repository privato: login a GHCR con un token "classic" con il solo permesso read:packages
echo '<token>' | docker login ghcr.io -u littlefly1969 --password-stdin

# Database dedicato nell'istanza esistente (una volta sola)
#   CREATE ROLE moscorun LOGIN PASSWORD '<password>';  CREATE DATABASE moscorun OWNER moscorun;

./moscorun.sh update      # scarica, genera il certificato Data Protection, migra, crea l'amministratore, avvia e verifica
```

Al primo `update` lo script genera (con l'immagine stessa) il certificato che cifra a riposo le chiavi dei cookie di sessione e lo aggiunge a `.env` come `MOSCORUN_DATA_PROTECTION_CERT`. In produzione l'app non parte senza un certificato valido.

## Aggiornamento con un comando

```bash
./moscorun.sh update            # versione del canale scelto in .env (es. latest)
./moscorun.sh update 0.3.1      # versione esatta
```

Passi eseguiti, nell'ordine:

1. Scarica l'immagine; se è identica a quella in uso non fa nulla.
2. Backup del database (`pg_dump`, formato custom) in `MOSCORUN_BACKUP_DIR`, con rotazione; il percorso resta in `.state/last-backup`.
3. **Preflight delle migrazioni:** il comando `migrate` dell'immagine elenca le migrazioni in attesa e rifiuta quelle *contract* (che rimuovono o restringono struttura) senza consenso esplicito, prima di toccare lo schema. Se le migrazioni falliscono o vengono rifiutate, la versione in uso resta attiva.
4. Riavvia l'applicazione e attende la **readiness**: container sano (*liveness*, `/health/live`) **e** `/health/ready`, che verifica la connessione al database, l'assenza di migrazioni non applicate e la corrispondenza dello schema al modello di questa versione (legge una riga da ogni tabella con tutte le colonne mappate).
5. Se la nuova versione non è pronta entro 90 secondi, torna automaticamente all'immagine precedente e ne verifica di nuovo la readiness.

Altri comandi: `rollback`, `backup`, `restore <file>` (chiede conferma), `status` (esce con errore se l'app non è pronta), `logs`.

### Migrazioni e rollback (regola expand/contract)

Il rollback automatico riporta l'immagine precedente ma **non** lo schema. Per questo:

- Le migrazioni applicate in automatico sono solo **expand**: aggiungono tabelle, colonne nullable o con valore predefinito, indici non univoci. La classificazione è a **elenco chiuso**: qualunque operazione non riconosciuta come additiva (indice UNIQUE, FK o check su tabella esistente, default rimosso, SQL libero, tipi futuri…) è contract. `MigrationSafetyTests` fa fallire la build se una migrazione contract non è dichiarata.
- La regola è un'euristica, non una prova: per questo la CI avvia anche l'**ultima release pubblicata sullo schema migrato dal nuovo commit** e ne verifica readiness e API principali (scenario "Compatibilità N-1"). La review manuale delle migrazioni resta obbligatoria.
- Le rimozioni (**contract**: drop, rename, colonne rese obbligatorie, SQL libero) si pubblicano solo in una release successiva a quella che ha smesso di usare la struttura, sono annotate con `[ContractMigration("motivo")]` e richiedono il consenso esplicito:

  ```bash
  ./moscorun.sh backup                                    # backup verificato appena prima
  MOSCORUN_ALLOW_CONTRACT_MIGRATIONS=1 ./moscorun.sh update
  ```

  Il timer notturno non applica mai migrazioni contract.
- Nessun rollback distruttivo dello schema viene eseguito alla cieca. Se dopo un aggiornamento anche la versione precedente non è pronta per colpa dello schema, lo script indica il backup fatto prima dell'aggiornamento:

  ```bash
  ./moscorun.sh rollback                    # immagine precedente
  ./moscorun.sh restore /var/backups/moscorun/moscorun-AAAAMMGGTHHMMSSZ.dump   # schema e dati di prima
  ```

  Il ripristino sovrascrive anche i dati arrivati dopo il backup: le pesate ancora presenti negli agenti vengono reinviate (l'acquisizione è idempotente).

## Aggiornamento automatico

**Da lasciare spento** finché aggiornamento, readiness, backup, ripristino e rollback non sono stati verificati sul server reale. Il timer può essere installato in anticipo: finché `MOSCORUN_AUTO_UPDATE` non vale `1` in `.env` non fa nulla (`./moscorun.sh auto-update` registra solo "aggiornamento automatico disabilitato").

```bash
sudo cp systemd/moscorun-update.* /etc/systemd/system/
sudo systemctl daemon-reload && sudo systemctl enable --now moscorun-update.timer
systemctl list-timers moscorun-update.timer   # prossima esecuzione (ogni notte alle 04:30 circa)
journalctl -u moscorun-update                 # esito
# Solo dopo la validazione:  MOSCORUN_AUTO_UPDATE=1 in .env
```

Con `MOSCORUN_VERSION=latest` arrivano tutte le release; con `MOSCORUN_VERSION=0` solo quelle della major 0 (nessuna modifica incompatibile). Su GHCR arrivano solo commit con CI completa riuscita (vedi [github.md](github.md)).

## Segreti

| Dove | Cosa |
| --- | --- |
| `.env` sul server (permessi 600, controllati dallo script) | Password PostgreSQL, amministratore iniziale, porta, certificato Data Protection (generato) |
| GitHub Secrets | Solo ciò che serve alle pipeline (es. firma APK, eventuale `RELEASE_PLEASE_TOKEN`) |
| Repository | Nessun segreto. `.env.example` contiene solo nomi e spiegazioni |

L'applicazione si rifiuta di partire se manca la stringa di connessione, se la password del database è vuota o un segnaposto (`change-me`, `password`…), se la password dell'amministratore è più corta di 12 caratteri o, in produzione, se manca un certificato Data Protection valido.

### Protezione dei dati a riposo

| Cosa | Protezione | Recupero |
| --- | --- | --- |
| Database | Utente PostgreSQL dedicato, porta non esposta (rete Docker o host), password solo in `.env` | `restore` da backup |
| Chiavi dei cookie (Data Protection) | Nel database, cifrate con il certificato in `.env`: un dump sottratto non basta a falsificare sessioni | Stesso `.env`: le sessioni sopravvivono a riavvii e ripristini (verificato in CI). Senza il certificato basta rifare l'accesso: i dati non dipendono da questa chiave |
| Backup | Cartella `700`, file `600`, rotazione | Conservare **anche una copia di `.env`** in un luogo sicuro separato |
| Token dei dispositivi | Solo SHA-256 nel database | Non recuperabili: si revoca e se ne genera uno nuovo |

## Validazione sul server reale (da fare)

La CI verifica lo script su un runner Ubuntu amd64 con PostgreSQL incluso. Sul server vanno ripetuti, prima di attivare il timer:

1. Installazione da zero con l'istanza PostgreSQL esistente (`POSTGRES_HOST`) e con Apache davanti (cookie `Secure`, IP reale nei log).
2. `./moscorun.sh backup` e `restore` su un database di prova con dati.
3. Riavvio del server: container e sessioni tornano da soli.
4. Arresto di PostgreSQL: `status` segnala "non pronto"; alla ripartenza l'app torna pronta.
5. Aggiornamento a una versione successiva e `rollback`.
6. Aggiornamento senza rete verso GHCR: lo script si ferma senza cambiare nulla.
7. Architettura: l'immagine multi-arch scelta (`amd64`/`arm64`) parte e risponde.

## Inventario ancora da rilevare

| Informazione | Perché serve |
| --- | --- |
| Distribuzione Linux e versione | BlueZ per l'agente bilancia e systemd |
| Architettura CPU, RAM e spazio | Limite di memoria del container e spazio per i backup |
| Bluetooth LE e posizione fisica | Ricezione effettiva della bilancia |
| Container e reti già presenti, versione PostgreSQL | Come raggiungere l'istanza esistente (`POSTGRES_HOST`) |
| Proxy, domini e porte | Virtual host HTTPS e porta locale libera (`MOSCORUN_PORT`) |

Non includere password o token nell'inventario: bastano le informazioni descrittive.
