# MoscoRun per Android

Un'unica app, **MoscoRun** (`it.littlefly.moscorun`), che copia gli allenamenti dal telefono al proprio server MoscoRun e li mostra rileggendoli dal server. Non esistono più la variante "MoscoRun HC" né l'APK di prova: la sorgente dei dati si sceglie nelle Impostazioni.

Specifica e criteri di accettazione: [docs/piano-app-android-definitiva.md](../docs/piano-app-android-definitiva.md). Firma e identità: [docs/firma-android.md](../docs/firma-android.md).

## Cosa fa

| Schermata | Contenuto |
| --- | --- |
| Stato | Ultimo tentativo, ultimo successo, avanzamento in corso, conteggi del registro, **Sincronizza ora** |
| Allenamenti | Elenco letto dal server, più recenti prima, una pagina alla volta mentre si scorre |
| Dettaglio | Riepilogo, percorso (con le zone private applicate dal server), un grafico per ogni serie che il server possiede, caricato su richiesta |
| Impostazioni | Server (una sola volta), sorgente, sincronizzazione automatica, intervallo, solo Wi-Fi, profondità dello storico |
| Diagnostica | Copertura dello storico, allenamenti rifiutati o trattenuti come possibili doppioni, con l'azione per inviarli comunque |

### Sorgenti

- **Samsung Health Data SDK** (predefinita): dati più completi dal Galaxy Watch. Richiede Samsung Health aggiornato e, finché l'app non è registrata come partner Samsung, la *modalità sviluppatore* del Samsung Health Data SDK attiva sul telefono (Samsung Health → Impostazioni → Informazioni su Samsung Health → toccare più volte la versione → Developer mode for Data Read). La distribuzione ad altri utenti richiede la registrazione partner prevista da Samsung.
- **Health Connect**: alternativa nello stesso APK. Serve il permesso di lettura degli allenamenti; sono facoltativi i percorsi ("Consenti sempre"), l'accesso in background, l'accesso ai dati precedenti e le singole serie. L'app spiega che cosa manca e che effetto ha.

Cambiare sorgente non richiede di reinserire server e token. Gli allenamenti che la nuova sorgente ripropone e che si sovrappongono per orario (almeno il 60% della sessione più breve) a uno già presente sul server con un'altra identità **non vengono inviati né cancellati**: restano in Diagnostica come possibili doppioni e decide l'utente.

### Configurazione una tantum

Indirizzo HTTPS del server e token del telefono (cruscotto → Dispositivi → tipo "Telefono Android") si inseriscono una sola volta. Vengono verificati sul server *prima* di essere salvati. Il token è cifrato con AES-256-GCM e chiave nell'Android Keystore, non è mai mostrato per intero ed è escluso dai backup. Sopravvivono a chiusure, riavvii, aggiornamenti dell'APK e cambi di sorgente. Dalle Impostazioni: **Verifica connessione**, **Sostituisci token**, **Scollega** (con conferma). Se la chiave del Keystore non è più disponibile (per esempio dopo un ripristino del telefono) l'app chiede di reinserire il solo token, una volta.

Lo stato della sincronizzazione è separato per server e per account: un token di un altro account o un altro server non riusa mai il registro del precedente.

### Sincronizzazione

- **A blocchi, dai più recenti ai più vecchi.** Lo storico iniziale è configurabile: **1 anno (predefinito), 2, 3, 4, 5 anni oppure tutto lo storico disponibile** (si ferma dopo 365 giorni consecutivi senza allenamenti, comunque entro 20 anni). Un blocco copre al massimo 90 giorni e si accorcia da solo nei periodi con molti allenamenti.
- **Riprende da dove era arrivata.** Registro e checkpoint sono in un database SQLite privato; un allenamento è segnato come sincronizzato solo dopo la conferma del server, un blocco solo quando è completo. Chiusura forzata, riavvio, rete assente o stop del sistema non perdono lavoro e non creano doppioni (l'invio è idempotente per sorgente e identificativo).
- **Memoria limitata.** Quattro allenamenti per pagina e un solo allenamento serializzato alla volta; sul telefono non resta alcuna copia degli allenamenti.
- **In background con WorkManager.** Lavoro periodico unico e persistente, circa ogni 4 ore (1–24 configurabile), con rete disponibile e batteria non scarica, ritentativi con attesa crescente. Ogni esecuzione dura al massimo 7 minuti; uno storico lungo prosegue in esecuzioni successive. Android decide il momento effettivo (Doze, risparmio energetico): l'intervallo è indicativo. Nessun servizio in primo piano, nessun allarme esatto, nessuna notifica fissa, nessuna schermata aperta dal background.
- La sincronizzazione automatica è **spenta finché non la si attiva**: è il consenso all'invio non presidiato. Gli ultimi 7 giorni vengono riletti a ogni esecuzione per cogliere modifiche tardive.
- Risposte del server: 200/201 confermano; 400, 409 e 413 vengono registrati in Diagnostica e non ritentati finché la sorgente non cambia il dato; 401/403 fermano tutto finché il token non viene sostituito; 429, 5xx e assenza di rete rimandano con backoff.

Quando la sorgente richiede l'utente (permesso revocato, consenso ai percorsi, Samsung Health da aggiornare) l'esecuzione si ferma, registra il motivo e l'app lo mostra alla prossima apertura.

## Requisiti del server

Server MoscoRun della stessa versione o successiva (lista a cursore `pageSize`/`cursor`, serie per metrica `metric`/`maxPoints`, `GET /api/v1/device`). Con un server precedente l'app continua a sincronizzare e mostra una sola pagina di allenamenti.

Non è stato introdotto un endpoint di riconciliazione: l'invio idempotente e la lista con `from`/`to` bastano a evitare doppioni anche dopo la perdita del registro, al costo di reinviare allenamenti che il server riconosce come già presenti.

## Passaggio dalle versioni precedenti

- L'APK aggiorna in place l'app "MoscoRun" già installata (stesso identificativo e stessa firma): indirizzo, token e registro degli allenamenti già inviati vengono conservati e importati, senza reinvii.
- La vecchia "MoscoRun HC" (`it.littlefly.moscorun.hc`) è un'app distinta: un APK non può rimuoverne un'altra. Non conteneva dati da trasferire. Dopo la prima sincronizzazione riuscita la Diagnostica propone di aprirne la scheda per disinstallarla a mano (Impostazioni Android → App → MoscoRun HC → Disinstalla).
- I vecchi APK `it.moscorun.probe*` vanno disinstallati allo stesso modo.

## Compilazione

Servono JDK 17, piattaforma Android API 36, Build Tools 36.0.0 e:

1. **Samsung Health Data SDK 1.1.0** in `app/libs/samsung-health-data-api.aar`. È proprietario: si scarica dal portale Samsung Developer con il proprio account accettandone la licenza, **non è nel repository e non va pubblicato** (né nei sorgenti né negli artefatti). Senza il file la build si ferma subito con un messaggio che spiega cosa fare; non esiste una variante di ripiego.
2. **Chiave Maps SDK for Android** in `GOOGLE_MAPS_ANDROID_API_KEY` oppure `maps.properties` (`apiKey=...`), fuori da Git.
3. **Firma stabile** ripristinata come descritto in [docs/firma-android.md](../docs/firma-android.md). Test e Lint funzionano anche senza.

```bash
cd android
python3 -m unittest discover -s scripts/tests -v   # script di firma e di verifica dell'APK
bash gradlew testDebugUnitTest lintDebug           # test JVM (anche Robolectric) e analisi statica
bash gradlew assembleRelease                       # app/build/outputs/apk/release/app-release.apk (R8)
python3 scripts/check-r8.py                        # R8 non ha tolto campi del contratto JSON (@WireFormat)
python3 scripts/verify-apks.py                     # un solo APK release: firma, identificativo, etichetta, versione, permessi
```

Senza l'AAR Samsung (per esempio in una PR da fork) si possono comunque compilare il codice, eseguire i test JVM e Lint:

```bash
bash gradlew -Pmoscorun.withoutSamsungSdk=true testDebugUnitTest lintDebug
```

In questa configurazione la sorgente Samsung è sostituita da `src/samsungStub` (risponde sempre "non disponibile") e la build rifiuta di impacchettare un APK: l'app installabile contiene sempre l'SDK. È ciò che esegue il workflow `android.yml` del repository sorgente, senza segreti. L'APK firmato viene prodotto esclusivamente dal repository privato, secondo il [runbook di migrazione](../docs/hybrid-public-readiness.md).

## Rilascio e ritorno alla versione precedente

1. Un commit `feat`/`fix` su main con CI verde crea il tag `vX.Y.Z`. Avviare il workflow privato **MoscoRun - APK ufficiale privato** (tag vuoto = ultimo tag), scaricare `MoscoRun-X.Y.Z-<versionCode>.apk` dalla release `apk-<versionCode>` del repository privato, controllarne l'impronta con `SHA256SUMS` e installarlo sopra la versione presente (aggiornamento in place: stessa firma, codice maggiore).
2. Verificare in Impostazioni che il server risulti configurato, poi **Sincronizza ora**.
3. Per tornare indietro: Android non installa un `versionCode` inferiore sopra uno superiore. Disinstallare l'app (si perdono configurazione e registro locali, **non** i dati sul server) e installare l'APK precedente; al primo avvio reinserire indirizzo e token. Il server riconosce gli allenamenti già presenti e non li duplica.

## Prove sul telefono (non automatizzabili in CI)

- Aggiornamento in place dalla versione attuale: server e token non vengono richiesti di nuovo.
- Prima build release (R8): sincronizzazione completa con sorgente Samsung e con Health Connect, mappa del percorso e grafici. R8 è verificato in CI sul contratto JSON, ma le librerie che usano riflessione (SDK Samsung) si provano solo sul telefono.
- Primo storico di un anno: chiudere forzatamente l'app a metà, riavviare il telefono, togliere la rete; alla ripresa i conteggi sul server non mostrano doppioni.
- Sincronizzazione automatica con app chiusa: confrontare "Ultimo successo" dopo alcune ore.
- Cambio sorgente Samsung ↔ Health Connect: i possibili doppioni compaiono in Diagnostica.
- Memoria e batteria: `adb shell dumpsys meminfo it.littlefly.moscorun` durante lo storico e `adb shell dumpsys batterystats --charged it.littlefly.moscorun` dopo 24 ore. L'app non resta in memoria tra un'esecuzione e l'altra; il consumo non è nullo e dipende da quanti allenamenti ci sono da leggere.

## Struttura del codice

| Pacchetto | Contenuto |
| --- | --- |
| `model` | Allenamento come letto da una sorgente |
| `source` | Interfaccia `WorkoutSource` e le due implementazioni (Samsung SDK, Health Connect) |
| `server` | Client HTTPS, formato di invio, lettura di lista, dettaglio, serie, percorso |
| `sync` | Motore a blocchi, registro e checkpoint SQLite, guardia dei doppioni, pianificazione WorkManager |
| `settings` | Server e token cifrato, preferenze, configurazione iniziale |
| `ui` | Schermate; dipendono solo da `AppHost`, quindi la logica è verificabile senza telefono |
