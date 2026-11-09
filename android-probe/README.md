# MoscoRun per Android

App Kotlin di verifica locale per confrontare i dati leggibili attraverso Health Connect e il Samsung Health Data SDK. Nessun collegamento a server MoscoRun, PostgreSQL, AI o tracciamento pubblicitario. La variante Health Connect non ha accesso Internet; la variante Samsung include il permesso Internet dichiarato dal suo SDK ufficiale. Il codice MoscoRun non invia rapporti sanitari a un proprio server. Android 10 o superiore; prove Samsung su telefono reale.

## Cosa verificare

1. Sincronizzare il Watch aprendo Samsung Health sul telefono.
2. In Samsung Health abilitare la condivisione delle categorie necessarie con Health Connect.
3. Installare l'APK, scegliere Health Connect, concedere i permessi di lettura e premere **Leggi e verifica**.
4. Scegliere 7 o 30 giorni. Il filtro iniziale considera solo i dati pubblicati da Samsung Health; disattivarlo per individuare eventuali altre origini.
5. Controllare le categorie e aprire una corsa. Il dettaglio legge campioni cardiaci, velocità, potenza e cadenza, riporta gli intervalli fra timestamp e visualizza una traccia GPS schematica locale, quando disponibile. Sono consultabili anche i record di distanza, calorie totali e VO₂max relativi all’intervallo; i valori rimangono nei record originali, senza convertirli in campioni puntuali fittizi.
6. Se Health Connect richiede un consenso specifico per il percorso, usare il pulsante nella corsa e confermare nell'interfaccia Android. Non viene richiesto il permesso di localizzazione del telefono: l'app legge un percorso già registrato.
7. Facoltativamente esportare il rapporto JSON nella destinazione scelta. Non caricare il rapporto in GitHub: può contenere dati sanitari e posizioni precise.

L'elenco può contenere attività diverse dalla corsa. I titoli mancanti sono mostrati con il codice tipo originale; il JSON conserva il tipo originale.

## Due varianti di compilazione

| Variante | Cosa contiene | Dipendenze aggiuntive |
| --- | --- | --- |
| `connectOnlyDebug` | Lettura reale tramite Health Connect; scheda Samsung con spiegazione del prerequisito | Nessuna AAR Samsung |
| `withSamsungDebug` | Health Connect e connettore diretto Samsung | AAR ufficiale Samsung in `app/libs/samsung-health-data-api.aar` |

Le varianti hanno identificativi definitivi distinti: **MoscoRun** (`it.littlefly.moscorun`, variante Samsung completa) e **MoscoRun HC** (`it.littlefly.moscorun.hc`, solo Health Connect). Si possono installare entrambe. La variante completa contiene anche Health Connect. La scheda Samsung non simula dati se l'SDK è assente.

Il dominio di riferimento è `littlefly.it`; non occorre configurare DNS per usare questi identificativi. I precedenti pacchetti `it.moscorun.probe` e `it.moscorun.probe.samsung` restano app separate: il primo passaggio richiede una nuova installazione e nuovi consensi. Samsung Health e i suoi dati originali non vengono modificati.

**SDK Samsung integrato:** il progetto usa l'AAR ufficiale 1.1.0 fornito dal proprietario. La dipendenza viene conservata nella release privata `samsung-sdk-local`, fuori dai sorgenti versionati; il workflow ne verifica SHA-256 prima di usarla. La compilazione automatica produce entrambe le varianti. La lettura dei dati reali richiede la prova sul telefono: i test del codice non certificano i permessi o la presenza dei dati nel proprio Samsung Health.

## Compilazione locale / Android Studio

Aprire la cartella `android-probe` in Android Studio. Servono JDK 17, piattaforma Android API 36 e Build Tools 36.0.0. Il progetto usa Gradle Wrapper 8.13, AGP 8.13.2, Kotlin 2.2.10 e Health Connect 1.1.0.

Prima di produrre un APK, ripristinare la firma come descritto in [Firma e aggiornamenti](../docs/firma-android.md). Senza firma sono disponibili test JVM e Lint, ma la generazione di APK viene bloccata. Il file `signing.properties` contiene credenziali e rimane fuori dal repository.

```bash
cd android-probe
bash gradlew testConnectOnlyDebugUnitTest lintConnectOnlyDebug assembleConnectOnlyDebug
```

L'APK è in `app/build/outputs/apk/connectOnly/debug/`. Per includere Samsung, aggiungere l'AAR ufficiale e usare:

```bash
bash gradlew testWithSamsungDebugUnitTest lintWithSamsungDebug assembleWithSamsungDebug
```

Samsung Health deve essere aggiornato e l’app abilitata secondo il processo ufficiale Samsung. Per questa prova personale di sviluppo, seguire [Samsung Health developer mode](https://developer.samsung.com/health/data/guide/developer-mode.html) per abilitare la lettura dei dati durante i test. La procedura di test non sostituisce la registrazione partner per distribuire un prodotto. Le app distribuite richiedono registrazione partner; i test dello sviluppatore usano le condizioni specifiche di sviluppo Samsung. Non è necessario installare un'app sul Watch né fornire la password Samsung a MoscoRun.

## APK direttamente da GitHub

Finché il progetto è nella PR #1, selezionare il ramo `feat/android-probe` per aprire il codice o creare Codespaces. L’avvio manuale di un workflow richiede che la sua definizione sia presente sul ramo predefinito; per la prova attuale usare le esecuzioni automatiche collegate alla PR.

Il workflow **Android Probe APK** si avvia su push o pull request che interessano il progetto, oppure manualmente da **Actions → Android Probe APK → Run workflow**. Produce:

- APK Health Connect di debug;
- risultati dei test e di Android Lint;
- APK con entrambi i collegamenti, usando la dipendenza Samsung privata predisposta. Nel lancio manuale è possibile disattivare `with_samsung` per generare soltanto Health Connect.

Aprire un'esecuzione completata con successo e scaricare `moscorun-probe-apk-<numero>-<tentativo>` dalla sezione **Artifacts**, estrarre lo ZIP e installare l'APK sul telefono. Ogni tentativo usa un nome distinto anche per i rapporti dei controlli, così un rilancio non entra in conflitto con i file del tentativo precedente. Gli artefatti sono conservati per 7 giorni e sono soggetti alle quote GitHub dell'account. Non occorre un PC configurato per compilarli; l'installazione dell'APK sul telefono resta un passaggio esplicito dell'utente.

Gli APK di debug e release usano lo stesso certificato stabile MoscoRun, controllato prima della compilazione e verificato negli APK prima della pubblicazione. La chiave privata viene ripristinata dal secret GitHub Actions `MOSCORUN_SIGNING_BUNDLE`; non viene generata sui runner. Se il secret manca o il certificato è diverso, il workflow esegue i controlli precedenti ma non pubblica APK. Il materiale temporaneo viene rimosso a fine esecuzione.

Il `versionCode` delle compilazioni GitHub corrisponde al numero progressivo del workflow. Per compilazioni locali da installare sopra una versione CI, passare un `-PmoscorunVersionCode=<numero>` coerente con la versione installata. Mantenendo identificativo e certificato, gli aggiornamenti non richiedono ogni volta disinstallazione o nuove restrizioni Maps. I permessi restano comunque sotto il controllo di Android e dell'utente.

### Dipendenza Samsung nelle compilazioni private GitHub

La release privata di dipendenze `samsung-sdk-local` contiene l’asset `samsung-health-data-api.aar` fornito dal proprietario. Questa versione è già predisposta per compilarlo automaticamente; un futuro aggiornamento dell’SDK richiede anche l’aggiornamento del digest nel workflow. L'azione lo scarica con il token del repository, senza incorporarlo nei sorgenti o pubblicarlo negli artefatti. Se l'asset manca, la variante Samsung fallisce esplicitamente; non viene sostituita con dati simulati.

Non rendere pubblico l'asset proprietario. L’app non scarica SDK né richiede credenziali Samsung. Le dipendenze vengono gestite soltanto durante la compilazione. I file sorgenti includono gli avvisi open source forniti con l’SDK, senza attribuire alla libreria proprietaria una licenza open source.

## Sviluppo nel browser

La configurazione `.devcontainer` permette di aprire il repository in GitHub Codespaces con JDK 17 e SDK Android. Da **Code → Codespaces** si può creare un ambiente e poi compilare dal terminale come sopra. Codespaces è facoltativo e soggetto alle quote dell'account; non viene avviato automaticamente dal progetto.

GitHub Actions esegue la compilazione; Codespaces è l'ambiente in cui modificare il codice. Per verificare i dati Samsung serve comunque il telefono con Samsung Health e i permessi reali. Un emulatore non dimostra il funzionamento dell'accesso Samsung.

## Cosa significano i risultati

- `letto`: la richiesta è riuscita; zero record significa nessun record restituito in quell'intervallo e con quel filtro.
- `permesso_negato`: categoria non interrogata perché manca il permesso.
- `errore`: richiesta fallita; il messaggio non viene trasformato in una lista vuota.
- `parziale`: limite esplicito raggiunto, non un archivio completo.
- Il percorso può essere disponibile, assente, non autorizzato o richiedere consenso separato.
- I timestamp e i valori originali non vengono ricampionati. Precisione del timestamp e frequenza di misura sono due cose diverse.
- Le serie Health Connect sono selezionate per intervallo e app di origine. Questo non dimostra un legame univoco con la sessione; sono conservati ID e origine dei record per verificarlo.
- Il dato cardiaco è una frequenza in bpm o una serie già elaborata, non un ECG, un'onda PPG o necessariamente gli intervalli fra singoli battiti.

Il rapporto contiene un'anteprima dei primi 3 record per categoria, fino a 300 record di allenamento e i dettagli delle sessioni aperte (Samsung include già i log delle sessioni lette). La paginazione si ferma a 200 pagine da 500 record per categoria; nel dettaglio Health Connect si applica anche una soglia di 100.000 campioni. Questi limiti sono dichiarati nel rapporto; per una verifica ridurre l'intervallo. Non è ancora un'esportazione completa dello storico.

I risultati restano in memoria e si perdono alla chiusura o ricreazione dell'app. Non viene eseguita sincronizzazione in background. L'esportazione è esplicita tramite il selettore file Android; scegliendo un fornitore cloud nel selettore, il file sarà gestito da quel fornitore. Il codice MoscoRun non invia rapporti al server MoscoRun. La libreria Samsung conserva il permesso Internet previsto dal proprio manifest: questa variante non è dichiarata priva di comunicazioni di rete dell’SDK.

## Primo confronto utile

Scegliere la stessa corsa reale nelle due varianti e confrontare: inizio/fine, tipo, distanza e durata nel JSON, disponibilità GPS, numero dei punti, intervalli mediano/minimo/massimo, numero dei campioni cardiaci e altre serie, campi assenti, provenienza e stato dei permessi. Annotare modello Watch, versione Android e Samsung Health. Conservare il rapporto localmente; condividere soltanto ciò che si decide esplicitamente.

## Riferimenti

- https://developer.android.com/health-and-fitness/health-connect/get-started
- https://developer.android.com/health-and-fitness/health-connect/features/exercise-routes
- https://developer.android.com/jetpack/androidx/releases/health-connect
- https://developer.samsung.com/health/data/process.html
- https://developer.samsung.com/health/data/guide/hello-sdk/read-data.html
- https://developer.samsung.com/health/data/api-reference/-shd/com.samsung.android.sdk.health.data.data.entries/-exercise-session/index.html
- https://docs.github.com/en/actions/concepts/workflows-and-actions/workflow-artifacts
