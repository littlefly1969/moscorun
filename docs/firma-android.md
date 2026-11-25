# Identità Android e firma stabile

Nome: **MoscoRun**. Dominio di riferimento: **littlefly.it**. Non serve modificare DNS o hosting per usare gli identificativi Android.

| Nome sul telefono | Identificativo applicazione |
| --- | --- |
| MoscoRun | `it.littlefly.moscorun` |

L'app è una sola. La precedente variante "MoscoRun HC" (`it.littlefly.moscorun.hc`) non viene più prodotta: la restrizione Google Maps per quell'identificativo può essere rimossa.

## Google Maps

La SHA-1 del certificato stabile, uguale per debug e release, è:

```text
EE:9C:07:8D:07:77:F1:8D:FA:42:9B:BA:B8:E7:8B:F0:E5:56:7D:BF
```

In Google Cloud, nella chiave per Maps SDK for Android, aggiungere una restrizione **App Android** per l'identificativo della tabella, con questa SHA-1. Questa coppia sostituisce, per le nuove app, i valori dei precedenti APK di prova. Le modifiche al codice e al numero versione non cambiano il certificato.

L'app integra Maps SDK for Android per la visualizzazione dei percorsi. La chiave viene inserita nella compilazione dal secret `GOOGLE_MAPS_ANDROID_API_KEY`, separato dal bundle di firma. Non aggiungere chiavi nei sorgenti; per lo sviluppo locale usare la variabile di ambiente omonima o `android/maps.properties` con `apiKey=...`. Il valore presente nell’APK è recuperabile, quindi mantenere le restrizioni Google per API, pacchetto e certificato. Il namespace interno Kotlin resta invariato: per Google Cloud si usa l'`applicationId` della tabella, non il namespace.

## Attivazione una sola volta su GitHub

Il materiale privato è nel backup riservato consegnato al proprietario; il repository contiene soltanto certificato pubblico e impronte attese. Non rigenerare il backup ad ogni compilazione.

I Secrets di firma vivono **solo** nel repository privato `littlefly1969/MoscoRun-private`, mai nel repository sorgente (che diventerà pubblico e riceve PR da fork).

1. Aprire il repository `littlefly1969/MoscoRun-private` su GitHub.
2. **Settings → Secrets and variables → Actions → New repository secret**.
3. Nome: `MOSCORUN_SIGNING_BUNDLE`.
4. Valore: contenuto completo di `MOSCORUN_SIGNING_BUNDLE.txt` estratto dal backup riservato.
5. Salvare e avviare il workflow **MoscoRun - APK ufficiale privato** indicando lo SHA del main sorgente approvato e il `versionCode`.

Alternativa con GitHub CLI autenticata:

```bash
gh secret set MOSCORUN_SIGNING_BUNDLE --repo littlefly1969/MoscoRun-private < MOSCORUN_SIGNING_BUNDLE.txt
```

Il file contiene la chiave privata cifrata e le informazioni per usarla: conservarlo come credenziale riservata. Non caricarlo nei sorgenti, negli artefatti delle Actions o negli issue. Conservare una copia di backup controllata dal proprietario; GitHub non consente di rileggere il valore del secret.

Il workflow ripristina un PKCS12 temporaneo, verifica il certificato atteso, compila e controlla l'APK. Se il secret manca o non coincide, non viene pubblicato un APK firmato con una chiave casuale. I controlli di test e Lint precedono il ripristino; il mancato ripristino lascia il workflow fallito, senza far sembrare pronto un APK non prodotto.

## Sviluppo locale

Con il backup riservato fuori dal repository:

```bash
cd android
python3 scripts/restore-signing.py --bundle /percorso/riservato/MOSCORUN_SIGNING_BUNDLE.txt --directory /percorso/riservato/firma-moscorun
export MOSCORUN_SIGNING_PROPERTIES=/percorso/riservato/firma-moscorun/signing.properties
bash gradlew testDebugUnitTest lintDebug assembleDebug
```

Sono necessari JDK 17 e `keytool`. Occorre anche l'AAR Samsung (vedi [android/README.md](../android/README.md)). `signing.properties`, PKCS12 e bundle sono esclusi da Git. Non usarli in rami o workflow di terzi non fidati.

## Aggiornamenti

- Conservare `applicationId`, certificato e chiave privata: cambiare uno di questi elementi può impedire un aggiornamento diretto o richiedere nuove restrizioni Google.
- Il `versionCode` dell'APK ufficiale è un input esplicito del workflow privato: deve essere maggiore di quello di ogni APK già distribuito (la verifica della monotonicità è manuale, registrare SHA e codice a ogni rilascio). Per compilazioni locali specificare `-PmoscorunVersionCode=<numero>` coerente e non inferiore alla versione installata.
- I vecchi `it.moscorun.probe*` e `it.littlefly.moscorun.hc` sono app separate, da disinstallare a mano. La prima installazione della nuova identità richiede nuovi consensi Health Connect e, dove previsto, nuova abilitazione Samsung per il pacchetto definitivo. Samsung Health conserva i propri dati originali.
- L'eventuale futura distribuzione con Play App Signing richiederà verificare il certificato usato da Google Play e autorizzarlo in Maps. Non viene configurata in questo incremento.

## Verifiche riproducibili

```bash
python3 -m unittest discover -s scripts/tests -v
```

I test controllano due ripristini indipendenti, secret mancante, certificato inatteso, password errata, base64 non valido e assenza di sovrascrittura di un keystore diverso, e le regole di verifica dell'APK (uno solo, identificativo, etichetta, firma, permessi non previsti). La CI aggiunge test Android, Lint e, dopo l'attivazione del secret, verifica crittografica con `apksigner` e controllo del pacchetto nell'APK.
