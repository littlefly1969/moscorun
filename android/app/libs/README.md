La libreria proprietaria Samsung non è inclusa nel repository e non deve esserlo.

L'app MoscoRun è unica e la include sempre: ottenere Samsung Health Data SDK 1.1.0 dal portale ufficiale Samsung Developer con il proprio account, nel rispetto della relativa licenza, e copiare l'AAR in questa cartella con il nome `samsung-health-data-api.aar`. Senza il file la build si ferma con un messaggio esplicito. Il `.gitignore` esclude tutte le AAR.

Per le compilazioni ufficiali l'AAR sta nella release `samsung-sdk-local` del repository privato `littlefly1969/MoscoRun-private`, non in questo repository: il workflow privato lo scarica e ne controlla il digest SHA-256 `f5d3d83cf00b97d0bb1b1db4da076e861eb1c3e6e704d89a34e68909d2f38654`. L'AAR non viene mai pubblicato negli artefatti. Gli avvisi open source forniti con l'SDK sono inclusi negli asset dell'app.
