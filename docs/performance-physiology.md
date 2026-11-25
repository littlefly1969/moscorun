# Step 04 — Performance Physiology, incremento 1

Baseline distanza-tempo: D = CS * t + Dprime. Regressione OLS con almeno tre durate distinte e sforzi massimali attestati, miglior distanza per durata. Il residuo RMSE descrive il fit, non è un intervallo predittivo. La durata appropriata (sforzi severi), la qualità del percorso, la comparabilità delle condizioni, la ripetibilità e l'incertezza devono essere verificate prima di usare il risultato; parametri positivi non dimostrano validità fisiologica. La velocità osservata è riferita esclusivamente alla durata richiesta. Efficienza cardiovascolare in metri per battito, descrittiva.

Moduli parziali F01, F02, F04, F06. Restano fitting robusto, selezione protocolli e finestre, curve di potenza F05, ricostituzione dinamica F03, VO2max F09 e soglie F10. Non stimare VO2max o soglie da queste primitive. Nessuna prescrizione automatica. Nessuna modifica DB/API.

Politica v2: sforzi massimali tra 120 e 1200 secondi, almeno tre durate distinte (raggruppate al millisecondo) e rapporto durata massima/minima almeno 2. La ricerca di finestre osservate ammette una tolleranza di 1 ms e usa la durata effettiva. Questi limiti sono euristici, non una validazione individuale del dominio severo.
