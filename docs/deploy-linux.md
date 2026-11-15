# Installazione e aggiornamento sul server Linux

Procedura verificata automaticamente in CI su uno stack reale (installazione con questo script, E2E, scenari operativi: riavvio, reverse proxy, perdita del database, rollback su readiness, backup e ripristino con dati). **Non è ancora stata eseguita sul server reale**: restano da rilevare i dati dell'inventario in fondo e da ripetere le verifiche elencate in "Validazione sul server reale". Fino ad allora l'aggiornamento automatico resta spento.

## Come funziona

- Un'unica immagine `ghcr.io/littlefly1969/moscorun` contiene API e cruscotto React, gira come utente non root su filesystem in sola lettura ed espone la porta 8080 solo su `127.0.0.1` dell'host.
- L'HTTPS è pubblicato da un reverse proxy sull'host (nginx), con una configurazione **generata da `.env`** (`./moscorun.sh proxy-config nginx`): vedi [Reverse proxy](#reverse-proxy).
- Dal proxy al container: `127.0.0.1:MOSCORUN_PORT` → Docker inoltra la connessione dal gateway della rete del progetto (`MOSCORUN_GATEWAY`, predefinito `172.30.85.1` sulla subnet `172.30.85.0/24`). È l'**unico** indirizzo, oltre al loopback, da cui l'app accetta `X-Forwarded-For/Proto/Host`; nessuna intera rete privata è considerata fidata. Se la subnet è già usata sul server, cambiare `MOSCORUN_SUBNET` e `MOSCORUN_GATEWAY` in `.env` (Docker rifiuta l'avvio in caso di sovrapposizione, senza toccare le altre reti).
- Database: vedi [Database](#database).
- Ogni release crea l'immagine per amd64 e arm64: va bene qualunque CPU del server.

## Prima installazione

```bash
sudo mkdir -p /opt/moscorun && cd /opt/moscorun
# Copiare da deploy/: compose.yaml, moscorun.sh, .env.example, proxy/, systemd/
cp .env.example .env && chmod 600 .env && nano .env

# Repository privato: login a GHCR con un token "classic" con il solo permesso read:packages,
# incollato quando viene chiesta la password (non resta nella cronologia della shell)
docker login ghcr.io -u littlefly1969

# Solo con un PostgreSQL esistente: database e ruolo dedicati (vedi "Database")

./moscorun.sh check       # coerenza di .env (database, porte, permessi), senza toccare nulla
./moscorun.sh update      # stessa verifica, poi scarica, genera il certificato Data Protection, migra, crea l'amministratore, avvia e verifica
```

Al primo `update` lo script genera (con l'immagine stessa) il certificato che cifra a riposo le chiavi dei cookie di sessione e lo aggiunge a `.env` come `MOSCORUN_DATA_PROTECTION_CERT`. In produzione l'app non parte senza un certificato valido.

## Reverse proxy

La configurazione del proxy non si scrive a mano: `moscorun.sh proxy-config` la genera dai parametri di `.env`, validati (nomi host, indirizzi, porte e percorsi finiscono in file di sistema). Si rigenera dopo ogni modifica di `.env`.

| Parametro | Significato | Predefinito |
| --- | --- | --- |
| `MOSCORUN_PUBLIC_HOST` | Nome pubblico del sito | obbligatorio |
| `MOSCORUN_PROXY_TLS` | `1`: nginx termina l'HTTPS; `0`: nginx in HTTP (TLS terminato a monte) | `0` |
| `MOSCORUN_PROXY_HTTP3` | `1`: anche HTTP/3 (QUIC, porta UDP 443 aperta) | `0` |
| `MOSCORUN_EDGE_PROXIES` | Proxy a monte fidati (IP o CIDR, separati da virgola o spazio) | nessuno |
| `MOSCORUN_TLS_CERT`, `MOSCORUN_TLS_KEY` | Certificato e chiave | `/etc/letsencrypt/live/<host>/…` |
| `MOSCORUN_ACME_ROOT` | Webroot per `certbot --webroot` | `/var/www/letsencrypt` |
| `MOSCORUN_PROXY_LISTEN`, `MOSCORUN_PROXY_TLS_LISTEN` | Porte di ascolto HTTP e HTTPS | `80`, `443` |
| `MOSCORUN_PROXY_UPSTREAM` | Solo per `proxy-config apache`: indirizzo di nginx visto dalla frontiera | — |

**Regola di fiducia.** nginx è l'unico punto che decide di chi fidarsi:
- accetta `X-Forwarded-For/Proto` solo dai proxy in `MOSCORUN_EDGE_PROXIES`, e solo l'ultimo indirizzo aggiunto da loro;
- verso l'app inoltra sempre valori propri (`X-Forwarded-For $remote_addr`).

Un client non può quindi falsificare IP o schema. Questo vale per i cookie `Secure`, per il limite dei tentativi di accesso per IP e per il registro accessi (`MoscoRun.Security`, con l'IP reale e senza l'email).

**Convivenza con altri siti.** La configurazione generata si integra con quelli già presenti sullo stesso nginx:
- un blocco `server` dedicato, scelto solo da `server_name`, senza `default_server`;
- variabili e cache TLS con prefisso `moscorun_`;
- nessuna modifica a `nginx.conf` o agli altri siti.

### Configurazione consigliata: nginx termina il TLS

```bash
sudo mkdir -p /var/www/letsencrypt
# 1) Prima emissione: sito in HTTP (serve solo la validazione ACME)
MOSCORUN_PROXY_TLS=0 ./moscorun.sh proxy-config nginx | sudo tee /etc/nginx/sites-available/moscorun >/dev/null
sudo ln -s /etc/nginx/sites-available/moscorun /etc/nginx/sites-enabled/moscorun
sudo nginx -t && sudo systemctl reload nginx
sudo certbot certonly --webroot -w /var/www/letsencrypt -d <MOSCORUN_PUBLIC_HOST> \
  --deploy-hook 'systemctl reload nginx'
# 2) HTTPS: in .env MOSCORUN_PROXY_TLS=1 (e facoltativamente MOSCORUN_PROXY_HTTP3=1)
./moscorun.sh proxy-config nginx | sudo tee /etc/nginx/sites-available/moscorun >/dev/null
sudo nginx -t && sudo systemctl reload nginx
```

Il risultato:
- HTTP risponde solo alla validazione ACME e reindirizza (301) a HTTPS;
- HTTPS usa TLS 1.2/1.3, HTTP/2, HSTS e, se attivato, HTTP/3 annunciato con `Alt-Svc`;
- il rinnovo dei certificati passa dal webroot e non modifica il file generato.

Il comando `proxy-config` lo produce da `.env` e basta reinstallarlo dopo una modifica. Il comando `certbot --nginx`, che riscrive i file di nginx, non serve.

### Con un proxy di frontiera (es. Apache su un'altra macchina)

Internet → frontiera (HTTPS) → nginx sul server (HTTP in LAN) → app.

- In `.env` del server indicare l'IP LAN della frontiera in `MOSCORUN_EDGE_PROXIES` e lasciare `MOSCORUN_PROXY_TLS=0`.
- Per la frontiera, `MOSCORUN_PROXY_UPSTREAM=http://<IP del server> ./moscorun.sh proxy-config apache` genera il virtual host Apache, con redirect, `ProxyPreserveHost` e `X-Forwarded-Proto: https`. Apache aggiunge da solo l'IP del client a `X-Forwarded-For`.
- Un host della LAN che si collega direttamente a nginx non ha privilegi: per lui valgono IP e schema reali.

Il passaggio successivo, cioè eliminare la frontiera e terminare il TLS su nginx, richiede solo due modifiche a `.env` (`MOSCORUN_EDGE_PROXIES` vuoto, `MOSCORUN_PROXY_TLS=1`), la rigenerazione e il port forwarding di 80/443 (più UDP 443 per HTTP/3) verso il server.

**Verifica in CI.** Entrambe le topologie sono provate su nginx e Apache reali, con l'app reale (`deploy/tests/scenarios.sh`):
- TLS con HTTP/2, redirect, ACME e HSTS;
- catena frontiera → nginx;
- tentativi di falsificare IP e schema, sia da Internet sia dalla LAN, controllati sul registro accessi dell'app.

## Database

| Situazione | Configurazione |
| --- | --- |
| Nessun PostgreSQL di sistema, oppure isolamento da altre applicazioni (consigliato) | PostgreSQL incluso (predefinito in `.env.example`): `COMPOSE_PROFILES=bundled-db` e `POSTGRES_HOST=db`, sempre insieme. Volume Docker dedicato, nessuna porta pubblicata, backup e ripristino toccano solo questo database |
| PostgreSQL di sistema sull'host | `POSTGRES_HOST=host.docker.internal`; database e ruolo dedicati (sotto); in `pg_hba.conf` solo `host moscorun moscorun <MOSCORUN_SUBNET> scram-sha-256` |
| PostgreSQL di un'altra applicazione in un container | Sconsigliato: lega aggiornamenti, backup e riavvii di MoscoRun a quelli dell'altra applicazione. Usare il database incluso |

Ruolo con privilegi minimi in un'istanza esistente:

```sql
CREATE ROLE moscorun LOGIN PASSWORD '<password>' NOSUPERUSER NOCREATEDB NOCREATEROLE;
CREATE DATABASE moscorun OWNER moscorun;
REVOKE ALL ON DATABASE moscorun FROM PUBLIC;
```

`backup` e `restore` operano solo su `POSTGRES_DB` con l'utente `POSTGRES_USER`: gli altri database dell'istanza non vengono letti né modificati. Il client usato è PostgreSQL 17, quindi l'istanza deve essere alla versione 17 o precedente.

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

## Mappe e zone private

Il percorso degli allenamenti è sempre salvato e servito dal proprio server. La cartografia è facoltativa: senza chiave il cruscotto disegna il tracciato su sfondo neutro e il browser non contatta Google.

Per usare Google Maps nel cruscotto web serve una chiave **Maps JavaScript API**, distinta da quella dell'app Android (che è limitata al pacchetto e all'impronta del certificato e non funziona in un browser):

1. Console Google Cloud → *API e servizi* → *Libreria*: abilitare **Maps JavaScript API** nel progetto.
2. *Credenziali* → *Crea credenziali* → *Chiave API*.
3. Nella chiave: *Restrizioni dell'applicazione* = **Siti web**, referrer `https://<MOSCORUN_PUBLIC_HOST>/*`; *Restrizioni API* = solo **Maps JavaScript API**.
4. In `.env`: `MOSCORUN_GOOGLE_MAPS_API_KEY=<chiave>`, poi `./moscorun.sh update`.

Con la chiave attiva l'applicazione amplia la Content-Security-Policy ai soli domini di Google Maps e invia l'origine come referrer (`Referrer-Policy: strict-origin`), necessaria alla restrizione per sito web. Una chiave dal formato non valido blocca l'avvio con un messaggio esplicito. Google riceve le richieste delle immagini della mappa per l'area visualizzata; il tracciato è disegnato dal browser e non viene inviato.

**Zone private** (spente per impostazione predefinita): `MOSCORUN_PRIVACY_ZONES="lat,lon,raggio;…"` con raggio tra 50 e 5000 m. I punti iniziali e finali dentro una zona non vengono mostrati né restituiti dall'API (cruscotto e app), e il riquadro geografico dei riepiloghi non è esposto. I dati salvati restano completi: togliendo la variabile il percorso torna intero.

## Segreti

| Dove | Cosa |
| --- | --- |
| `.env` sul server (permessi 600, controllati dallo script) | Password PostgreSQL, amministratore iniziale, porta, certificato Data Protection (generato) |
| GitHub Secrets | Solo ciò che serve alle pipeline (es. firma APK e chiave Maps dell'app Android) |
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

1. Installazione da zero con la configurazione di database e proxy scelta (cookie `Secure`, IP reale nel registro accessi, altri siti di nginx invariati).
2. `./moscorun.sh backup` e `restore` su un database di prova con dati.
3. Riavvio del server: container e sessioni tornano da soli.
4. Arresto di PostgreSQL: `status` segnala "non pronto"; alla ripartenza l'app torna pronta.
5. Aggiornamento a una versione successiva e `rollback`.
6. Aggiornamento senza rete verso GHCR: lo script si ferma senza cambiare nulla.
7. Architettura: l'immagine multi-arch scelta (`amd64`/`arm64`) parte e risponde.

## Inventario del server

Per un'installazione nuova bastano: distribuzione e architettura, RAM e spazio, porte e siti già presenti nel proxy, eventuale PostgreSQL esistente e versione, Bluetooth LE (per l'agente bilancia). Non includere password o token: bastano le informazioni descrittive. Il modello della bilancia non serve, perché l'agente lo rileva da solo durante l'installazione (#3).

Server attuale (rilevato il 2026-11-13):
- **Sistema e risorse:** Ubuntu 26.04.1 LTS, x86_64 a 16 thread, 30 GiB di RAM, Docker 29.1 con Compose 2.40.
- **Proxy:** nginx 1.28 sulla porta 80, oggi dietro un Apache di frontiera.
- **Applicazione già presente:** nubarca, su 127.0.0.1:8080/8081, con il proprio PostgreSQL in container. Nessun PostgreSQL di sistema.
- **Scelte per MoscoRun:**
  - `MOSCORUN_PORT=8085`, che è libera;
  - database incluso;
  - prima configurazione dietro la frontiera, poi TLS su nginx.
- **Bluetooth:** adattatore Realtek USB presente ma controller non visibile a BlueZ (da sistemare con l'agente, #3).
- **Frontiera:** Apache su 192.168.1.105 (`MOSCORUN_EDGE_PROXIES=192.168.1.105`) finché il TLS non passa a nginx.
