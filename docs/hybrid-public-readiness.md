# Migrazione ibrida: sorgenti pubblicabili e build Android privata

**Stato: preparazione, non autorizzazione alla pubblicazione.** Non fare merge di questa PR prima della prova privata riuscita: il nuovo workflow sorgente non genera più APK. Non cambiare visibilità prima di tutti i gate.

## Destinazione privata

1. Creare un repository autonomo `littlefly1969/MoscoRun-private`, visibilità Private, inizializzato con README. Non un fork. Nessun runner self-hosted.
2. Verificare licenza e diritto di conservazione/uso CI del Samsung Health Data SDK. Dal portale Samsung o dalla release riservata esistente, procurarsi l'AAR e il notice. Caricarli nella release `samsung-sdk-local` della sola destinazione privata; non committare i binari.
3. Verificare il download dalla destinazione: AAR da 501608 byte, SHA-256 `f5d3d83cf00b97d0bb1b1db4da076e861eb1c3e6e704d89a34e68909d2f38654`; notice da 11137 byte, confronto integrale con l'originale. Non eliminare la sorgente prima della verifica.
4. Configurare i Secrets privati `MOSCORUN_SIGNING_BUNDLE` dal backup originale e `GOOGLE_MAPS_ANDROID_API_KEY` da Google Cloud. Non inviare valori in chat, issue o Git. Per la prova mentre Moscorun è privato, usare un token temporaneo fine-grained con accesso Contents in sola lettura solo a Moscorun, salvato come `MOSCORUN_SOURCE_READ_TOKEN` nella destinazione privata. Rimuovere/revocare quando il sorgente è pubblico. Mai dare al sorgente pubblico accesso alla destinazione.
5. Installare il [workflow modello](hybrid/android-release.yml) come `.github/workflows/android-release.yml` su main della destinazione privata. Il modello qui è documentazione, non workflow attivo. Limitare accesso e scrittura del repo privato.

## Prova prima del cutover

- Revisionare workflow e intero commit sorgente, inclusi Gradle, plugin e script: codice approvato su main, CI verde sullo stesso SHA, nessuna PR/fork da firmare. Il modello rifiuta se il main scaricato non coincide con lo SHA completo immesso e se l'identità Android non coincide.
- Indicare manualmente un versionCode maggiore di tutti gli APK già distribuiti, registrare SHA e codice versione nel privato. Il nuovo contatore Actions riparte; non va usato per aggiornamenti in place. La verifica della monotonicità resta manuale.
- Avviare la build da Actions del repo privato su main. Verificare test JVM, lint, integrità SDK, firma, package, APK, e installazione in aggiornamento sul telefono.
- Il nuovo workflow nel sorgente esegue esclusivamente test Python con chiavi temporanee e manifest sintetici. Non è una build Android completa; non riceve segreti reali e non pubblica APK. La pipeline privata è necessaria per test JVM/Lint e APK.
- Dopo prova privata riuscita, fare merge della PR di separazione, verificare CI del sorgente e rimuovere i Secrets Android dal sorgente. Eliminare gli asset SDK e la release originale solo dopo verifica privata. Non modificare server o database.

## Gate prima della pubblicazione

- [ ] SDK e notice nella destinazione privata, integrità e licenza verificate.
- [ ] APK ufficiale generato nel privato, aggiornamento sul telefono riuscito.
- [ ] Workflow pubblico senza segreti; Secrets Android rimossi dal sorgente.
- [ ] Scansione Gitleaks completa di tutti i branch/tag/ref, controllo indipendente delle eccezioni.
- [ ] Audit dei dati sanitari, GPS e configurazioni personali anche nella storia Git, nei commenti PR/issue, negli allegati, nelle release e nei tag.
- [ ] Audit e bonifica di log, cache, artefatti Actions storici e APK; log e report non redatti non vanno pubblicati.
- [ ] Verifica restrizioni Maps per package/certificato e quota; revisione visibilità GHCR e contenuti immagini.
- [ ] Decisione su licenze e divulgazione della ricerca; conferma esplicita del proprietario prima del cambio visibilità.
- [ ] Protezione main e approvazione workflow esterni, token read-only; attivare CodeQL, secret scanning, push protection appena disponibili.
- [ ] Dopo pubblicazione: CI fork senza segreti, nuova build privata senza token sorgente, revoca token temporaneo.

I gate amministrativi non sono eseguiti automaticamente da questa PR. Se la storia non può essere bonificata, usare un nuovo repository pubblico con snapshot ripulito e conservare quello originale privato. Nessun cambiamento di visibilità implicito.
