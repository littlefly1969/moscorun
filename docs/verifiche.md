# Verifiche prima dello sviluppo

## Informazioni da raccogliere

- Repository privato: `littlefly1969/moscorun`.
- Modello esatto della bilancia, preferibilmente il codice sull'etichetta inferiore.
- Server Linux condiviso con stack LAMP e applicazioni .NET; PostgreSQL già presente in container e MariaDB disponibile. Restano da rilevare distribuzione/versione, architettura CPU, risorse, Bluetooth LE, versione PostgreSQL e configurazione di rete.
- Modello Galaxy Watch e telefono, versione Android e Samsung Health.
- Eventuale utilizzo della bilancia da parte di più persone.

## Ordine di lavoro

1. Verificare il server Linux e predisporre la configurazione di sviluppo del repository `littlefly1969/moscorun`.
2. Eseguire sul server una scansione BLE mentre viene completata una pesata.
3. Confermare protocollo, unità e segnale di pesata definitiva; acquisire un campione minimo per il decoder.
4. Salvare una sola pesata, poi verificare pacchetti ripetuti, seconda pesata e riavvio del servizio.
5. Nel telefono abilitare le categorie necessarie in Samsung Health → Impostazioni → Health Connect. I nomi dei menu possono cambiare con versione e lingua.
6. Registrare/sincronizzare una corsa reale e verificare che sessione, distanza e frequenza cardiaca siano effettivamente accessibili.
7. Costruire il ponte Android e l'importazione sul server; ripetere il trasferimento senza duplicati e verificare correzioni/cancellazioni.
8. Realizzare il primo cruscotto e il calendario; verificare separatamente l'eventuale invio di sessioni al Watch.

Le integrazioni hardware non sono state provate in questo ambiente. Dal 12/11/2026 esistono API, archivio e cruscotto (vedi README); manca il decoder BLE. L'app Android in `android` sincronizza gli allenamenti verso il server da Samsung Health Data SDK (AAR ufficiale scaricato dalla release privata, non presente nel repository) o da Health Connect; motore di sincronizzazione, registro, pianificazione e client sono coperti da test JVM, mentre lettura reale dalle sorgenti, esecuzione in background, memoria e batteria vanno provate sul telefono (elenco in `android/README.md`).

## Fonti tecniche consultate il 9 novembre 2026

- [Samsung: Health Connect FAQ](https://developer.samsung.com/health/health-connect-faq.html): percorso Galaxy Watch → telefono e sincronizzazione.
- [Samsung: Accessing Samsung Health Data through Health Connect](https://developer.samsung.com/health/blog/en/accessing-samsung-health-data-through-health-connect): categorie condivise; la copertura può variare. Per i permessi recenti di background usare la documentazione Android aggiornata.
- [Android: lettura dei dati](https://developer.android.com/health-and-fitness/health-connect/read-data): paginazione, storico e funzionalità disponibili.
- [Android: sincronizzazione](https://developer.android.com/health-and-fitness/health-connect/sync-data): sincronizzazione e lettura in background.
- [Android: training plans](https://developer.android.com/health-and-fitness/health-connect/features/training-plans): record di allenamenti pianificati e verifica della funzionalità.
- [Samsung: Health Data SDK, processo](https://developer.samsung.com/health/data/process.html): alternativa SDK e requisiti di distribuzione.
- [Samsung: esportazione personale](https://www.samsung.com/us/support/answer/ANS10001379/): scaricamento dei dati.
- [ESPHome: Xiaomi Miscale](https://esphome.io/components/sensor/xiaomi_miscale/): riferimento al protocollo e ai modelli documentati; ESPHome non è una dipendenza del progetto.
- [Bleak: scanner](https://bleak.readthedocs.io/en/latest/api/scanner.html): ricezione dei pacchetti BLE.
- [Bleak: Linux backend](https://bleak.readthedocs.io/en/latest/backends/linux.html): integrazione con lo stack Bluetooth Linux.

- [Microsoft: supporto .NET](https://dotnet.microsoft.com/en-us/platform/support/policy): .NET 10 LTS come base proposta per il nuovo sviluppo.
- [Android: pubblicare un’app Health Connect](https://developer.android.com/health-and-fitness/health-connect/publish): dichiarazioni di funzionalità, accessi e privacy per Google Play.
