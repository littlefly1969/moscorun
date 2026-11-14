# Architettura proposta

La base applicativa è ASP.NET Core su .NET 10 LTS con PostgreSQL e un cruscotto React servito dalla stessa applicazione. Il servizio Bluetooth Python resta un adattatore hardware; Kotlin resta il ponte Android. I dettagli di prodotto e installazione sono in `prodotto.md` e `deploy-linux.md`.

## Implementazione attuale (incremento 1)

```
Agente bilancia ──Bearer mr1_…──▶ POST /api/v1/ingest/weighings ─┐
App Android    ──Bearer mr1_…──▶ (prossimo incremento)          │   ASP.NET Core (.NET 10)
Browser ──cookie di sessione──▶ /api/auth, /api/v1/* ───────────┼─▶ Domain ─▶ EF Core ─▶ PostgreSQL
        ◀── cruscotto React (file statici, stessa origine) ─────┘
```

- **Monolite modulare** in tre progetti: `Domain` (regole pure e testabili), `Infrastructure` (EF Core, Identity, migrazioni, schema `moscorun`), `Api` (endpoint minimal API raggruppati per funzionalità).
- **Due modi di autenticazione:** cookie ASP.NET Core Identity per le persone, token per dispositivo (`mr1_<id>_<segreto>`) per gli agenti. Le policy impediscono gli usi incrociati.
- **Contratto OpenAPI 3.1** generato in build in `contracts/` e usato per generare il client TypeScript.
- **Comandi operativi** nella stessa immagine: `migrate` (migrazioni + amministratore iniziale) e `healthcheck`.
- **Chiavi Data Protection nel database:** le sessioni sopravvivono ad aggiornamenti e riavvii.

API principali:

| Metodo e percorso | Chi | Scopo |
| --- | --- | --- |
| `POST /api/auth/login`, `/logout`, `GET /api/auth/me` | persona | Sessione del cruscotto |
| `POST /api/v1/ingest/weighings` | dispositivo | Pesata definitiva, idempotente su `sourceEventId` |
| `GET /api/v1/weighings`, `PUT /api/v1/weighings/{id}/attribution` | persona | Elenco e attribuzione (mia / di altri) |
| `GET/POST/DELETE /api/v1/devices` | persona | Registrazione con token mostrato una volta, revoca |
| `GET /api/v1/dashboard/summary` | persona | Ultimo peso, tendenze su medie mobili, copertura, freschezza sorgenti |
| `GET /health/live`, `/health/ready`, `/api/system/info` | pubblico | Stato e versione |

## Acquisizione diretta della bilancia

Il server dispone di Bluetooth LE, integrato oppure tramite adattatore USB, ed è collocato entro portata della bilancia. Su Linux il servizio può usare BlueZ e una libreria come Bleak per ricevere i pacchetti BLE.

Per modelli Xiaomi documentati, tra cui XMTZC02HM e XMTZC05HM, peso e impedenza possono essere trasmessi tramite broadcast BLE. Il modello effettivamente posseduto non è ancora noto: non si assume che tutti i prodotti Xiaomi usino lo stesso protocollo o che siano sempre leggibili senza chiavi.

Passi del servizio:

1. Identificare il dispositivo e il protocollo senza salvare continuamente gli annunci di altri dispositivi vicini.
2. Decodificare unità, peso e indicatori di stabilità/fine pesata.
3. Salvare soltanto le misure definitive; le letture intermedie restano transitorie.
4. Evitare di trasformare pacchetti ripetuti in più pesate. Conservare la distinzione fra ripetizione radio e due pesate reali vicine.
5. Salvare impedenza soltanto se presente nella stessa misura: non riutilizzare valori di una pesata precedente.
6. Conservare la provenienza e lo stato di attribuzione alla persona. Se la bilancia è condivisa, il peso da solo non basta per identificare con certezza l'utente.

Non promettiamo il recupero di pesate avvenute a servizio spento: la disponibilità di memoria e storico dipende dal modello.

## Samsung Health

Percorso principale: Galaxy Watch → Samsung Health sul telefono → Health Connect → applicazione Android del progetto → server.

Il server non usa un login Samsung come sostituto dell'app Android. La sincronizzazione Watch/telefono può avere ritardi e la copertura dei dati va verificata con una corsa reale.

Prima fase in sola lettura:

- Sessione di esercizio, inizio/fine, tipo e identificativo originale.
- Distanza, velocità e campioni cardiaci disponibili nell'intervallo della corsa.
- Eventuali pause e segmenti, distinguendo tempo trascorso e durata attiva quando disponibili.
- Sonno e frequenza a riposo soltanto in una fase successiva e con copertura verificata.

Non associare automaticamente ogni campione nello stesso intervallo alla corsa: verificare provenienza, dispositivo e collegamenti disponibili. Dati mancanti devono rimanere mancanti, non diventare zero.

Health Connect prevede permessi dedicati per storico oltre il limite predefinito e letture in background. Verificare disponibilità e consenso; prevedere sincronizzazione manuale in primo piano. La storia già presente in Samsung Health non è automaticamente tutta disponibile in Health Connect. Samsung offre anche un'esportazione personale, da ispezionare prima di scrivere un importatore.

Alternativa: Samsung Health Data SDK, da valutare solo se servono dati assenti da Health Connect. La distribuzione richiede il percorso partner Samsung; la modalità sviluppatore è destinata ai test.

## Archivio e sincronizzazione

Modello iniziale proposto:

| Oggetto | Contenuto minimo |
| --- | --- |
| Pesata | Identificativo locale, timestamp con fuso/offset, dispositivo, peso in kg, impedenza eventuale, stato di attribuzione |
| Allenamento svolto | Fonte e ID originale, revisione, inizio/fine, tipo, durata e distanza disponibili |
| Campione | Tipo, timestamp/intervallo, valore, unità, fonte, associazione alla sessione se verificata |
| Allenamento pianificato | Data, tipo, obiettivo e segmenti previsti; distinto dalla registrazione svolta |
| Sincronizzazione | Fonte, checkpoint, esito, ultimo aggiornamento riuscito |

Gli ID della fonte e le revisioni consentono aggiornamenti idempotenti. Gestire paginazione, interruzioni, correzioni, cancellazioni e scadenza dei checkpoint con risincronizzazione. Evitare doppio conteggio se la stessa attività arriva anche da altre fonti.

API per il telefono autenticata; trasferimento cifrato o tramite rete privata opportunamente configurata. Database e backup fuori dal repository. Nessuna credenziale nel codice e nessun dato sanitario nei log ordinari. Il servizio di acquisizione ha un solo processo proprietario del Bluetooth.

## Analisi e pianificazione

Primo cruscotto: andamento delle pesate, volume settimanale, durata, distanza, passo e frequenza cardiaca quando disponibili, confronto programmato/svolto. Mostrare freschezza e copertura dei dati.

La pianificazione nel progetto è fattibile indipendentemente dall'invio all'orologio. Health Connect documenta i PlannedExerciseSessionRecord, ma ciò non dimostra che Samsung Health e il Galaxy Watch dell'utente li importino ed eseguano: serve una prova end-to-end dedicata.
