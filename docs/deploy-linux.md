# Installazione e aggiornamento sul server Linux

Procedura verificata su uno stack di prova (installazione, aggiornamento, rollback automatico, ripristino). Non è ancora stata eseguita sul server reale: restano da rilevare i dati dell'inventario in fondo.

## Come funziona

- Un'unica immagine `ghcr.io/littlefly1969/moscorun` contiene API e cruscotto React, gira come utente non root su filesystem in sola lettura ed espone la porta 8080 solo su `127.0.0.1` dell'host.
- Il reverse proxy già presente (Apache) pubblica l'HTTPS: esempio in [deploy/apache-moscorun.conf.example](../deploy/apache-moscorun.conf.example).
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

./moscorun.sh update      # scarica, applica le migrazioni, crea l'amministratore, avvia e verifica
```

## Aggiornamento con un comando

```bash
./moscorun.sh update            # versione del canale scelto in .env (es. latest)
./moscorun.sh update 0.3.1      # versione esatta
```

Passi eseguiti, nell'ordine:

1. Scarica l'immagine; se è identica a quella in uso non fa nulla.
2. Backup del database (`pg_dump`, formato custom) in `MOSCORUN_BACKUP_DIR`, con rotazione.
3. Applica le migrazioni con un container temporaneo. Se falliscono, la versione in uso resta attiva.
4. Riavvia l'applicazione e attende l'healthcheck.
5. Se la nuova versione non diventa sana entro 90 secondi, torna automaticamente all'immagine precedente.

Altri comandi: `rollback`, `backup`, `restore <file>` (chiede conferma), `status`, `logs`.

## Aggiornamento automatico

```bash
sudo cp systemd/moscorun-update.* /etc/systemd/system/
sudo systemctl daemon-reload && sudo systemctl enable --now moscorun-update.timer
systemctl list-timers moscorun-update.timer   # prossima esecuzione (ogni notte alle 04:30 circa)
journalctl -u moscorun-update                 # esito
```

Con `MOSCORUN_VERSION=latest` arrivano tutte le release; con `MOSCORUN_VERSION=0` solo quelle della major 0 (nessuna modifica incompatibile).

## Segreti

| Dove | Cosa |
| --- | --- |
| `.env` sul server (permessi 600, controllati dallo script) | Password PostgreSQL, amministratore iniziale, porta |
| GitHub Secrets | Solo ciò che serve alle pipeline (es. firma APK, eventuale `RELEASE_PLEASE_TOKEN`) |
| Repository | Nessun segreto. `.env.example` contiene solo nomi e spiegazioni |

L'applicazione si rifiuta di partire se manca la stringa di connessione, se la password del database è vuota o un segnaposto (`change-me`, `password`…) o se la password dell'amministratore è più corta di 12 caratteri.

## Inventario ancora da rilevare

| Informazione | Perché serve |
| --- | --- |
| Distribuzione Linux e versione | BlueZ per l'agente bilancia e systemd |
| Architettura CPU, RAM e spazio | Limite di memoria del container e spazio per i backup |
| Bluetooth LE e posizione fisica | Ricezione effettiva della bilancia |
| Container e reti già presenti, versione PostgreSQL | Come raggiungere l'istanza esistente (`POSTGRES_HOST`) |
| Proxy, domini e porte | Virtual host HTTPS e porta locale libera (`MOSCORUN_PORT`) |

Non includere password o token nell'inventario: bastano le informazioni descrittive.
