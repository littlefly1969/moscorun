# MoscoRun

Assistente personale per allenamenti e forma fisica: raccoglie peso e allenamenti e confronta il lavoro svolto con quello programmato. La prima applicazione è il percorso Roma–Ostia e Berlino; le entità del prodotto restano indipendenti dalle gare.

**Stato (14 novembre 2026):** primo incremento unito in main. Backend, cruscotto web, pipeline e script di installazione sono testati in CI su uno stack reale; l'installazione sul server di casa non è ancora stata validata. **Non implementati:** agente Bluetooth della bilancia e sincronizzazione Android → server (prossime fasi, sotto). Repository privato: [littlefly1969/moscorun](https://github.com/littlefly1969/moscorun).

## App Android — MoscoRun

[App di verifica e istruzioni](android-probe/README.md): Kotlin, lettura in primo piano di Health Connect e variante completa con Samsung Health Data SDK e Google Maps. La versione completa permette anche una prova della mappa senza caricare allenamenti. Nessun invio al server. Il workflow **Android Probe APK** compila su GitHub e pubblica un APK di debug scaricabile dagli artefatti. La configurazione Codespaces consente lo sviluppo dal browser. Il nome definitivo è **MoscoRun**, dominio di riferimento `littlefly.it`, identificativo Android `it.littlefly.moscorun`; la variante solo Health Connect usa `it.littlefly.moscorun.hc`. [Firma stabile e aggiornamenti](docs/firma-android.md).

## Cosa c'è

| Parte | Tecnologia | Stato |
| --- | --- | --- |
| API e archivio | ASP.NET Core .NET 10 LTS, EF Core, PostgreSQL 17 | Pesate idempotenti, dispositivi con token revocabili, riepilogo del cruscotto |
| Cruscotto web | React 19, TypeScript strict, Vite, TanStack Query | Accesso, andamento del peso, freschezza delle sorgenti, gestione dispositivi |
| Contratto API | OpenAPI 3.1 generato in build ([contracts/](contracts/)) | I tipi TypeScript del cruscotto sono generati dal contratto e verificati in CI |
| Distribuzione | Un'immagine container multi-arch su GHCR, Docker Compose | Aggiornamento con un comando, backup e rollback automatici |
| Agente bilancia | Python, Bleak/BlueZ | **Non implementato**: all'installazione cercherà e abbinerà da solo la bilancia Xiaomi |
| Ponte Android | Kotlin, Health Connect | **Sincronizzazione verso il server non implementata**; prototipo in sola lettura in [android-probe](android-probe/README.md) |

## Prossime fasi

| Fase | Contenuto | Prerequisiti |
| --- | --- | --- |
| Agente bilancia ([#3](https://github.com/littlefly1969/moscorun/issues/3)) | Raccolta di pacchetti BLE reali e decodifica; coda persistente su disco; invio con retry e backoff; idempotenza per `sourceEventId`; deduplicazione; gestione di correzioni e cancellazioni; token del dispositivo | Bluetooth del server (BlueZ) |
| Sincronizzazione Android ([#4](https://github.com/littlefly1969/moscorun/issues/4)) | Lettura incrementale da Health Connect (token di modifica); coda locale; retry; idempotenza; gestione di cancellazioni e modifiche a posteriori | Galaxy Watch5 Pro con Samsung Health Data SDK, prova del prototipo sul telefono |
| Installazione reale | Validazione sul server (vedi [deploy-linux.md](docs/deploy-linux.md)), poi attivazione del timer | Inventario del server |

I test di questa versione non coprono le due fasi non implementate.

## Percorso dei dati

- Bilancia Xiaomi → Bluetooth del server → agente di acquisizione → `POST /api/v1/ingest/weighings` (token del dispositivo).
- Galaxy Watch → Samsung Health → Health Connect → app Android → API autenticata.
- Archivio PostgreSQL → cruscotto React servito dalla stessa applicazione.

Il server legge direttamente la bilancia. Home Assistant non fa parte del percorso dati.

## Avvio rapido

- **Sviluppo locale:** [docs/sviluppo.md](docs/sviluppo.md).
- **Installazione e aggiornamento sul server:** [docs/deploy-linux.md](docs/deploy-linux.md). In breve: `./moscorun.sh update`.
- **Cosa fa GitHub per il progetto:** [docs/github.md](docs/github.md).

## Documenti

- [Architettura](docs/architettura.md)
- [Base di prodotto](docs/prodotto.md)
- [Qualità e test](docs/qualita.md)
- [Verifiche iniziali e fonti](docs/verifiche.md)

## Dati personali e segreti

Il repository contiene codice e documentazione. Pesate, esportazioni Samsung, database, tracciati e credenziali restano fuori da Git. I segreti di esercizio (password PostgreSQL, amministratore) stanno solo nel file `.env` sul server con permessi `600`; nei GitHub Secrets vanno solo le credenziali usate dalle pipeline. L'applicazione rifiuta di avviarsi con segreti mancanti o con valori segnaposto.
