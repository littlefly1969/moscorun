La libreria proprietaria Samsung non è inclusa nel repository.

Per compilare la variante `withSamsung`, ottenere Samsung Health Data SDK 1.1.0 dal portale ufficiale Samsung con il proprio account e nel rispetto della relativa licenza. Copiare l'AAR in questa cartella con il nome `samsung-health-data-api.aar`.

La variante `connectOnly` non richiede questo file. Il `.gitignore` esclude tutte le AAR.

Per le compilazioni GitHub del progetto è già predisposta la release privata `samsung-sdk-local`. Il workflow scarica l'AAR e controlla il digest SHA-256 `f5d3d83cf00b97d0bb1b1db4da076e861eb1c3e6e704d89a34e68909d2f38654`. Gli avvisi open source forniti con l'SDK sono inclusi negli asset della variante Samsung.
