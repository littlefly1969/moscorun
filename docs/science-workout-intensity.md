# Distribuzione intensità e carico cardiaco su seduta

GET /api/v1/workouts/{id}/science/intensity richiede il canale HeartRate, Speed o Power e 1–10 soglie positive strettamente crescenti, separate da virgole con punto decimale. Una soglia è il limite superiore escluso: un valore uguale alla soglia entra nella zona successiva.

Le serie native della revisione corrente sono lette insieme all'allenamento con filtro per proprietario. Il supporto usa lo stesso ZOH conservativo dell'audit: due estremi validi e massimo 5 s; le lacune, il tempo prima del primo campione e dopo l'ultimo sono mancanti. Il totale è il tempo trascorso, non il solo tempo attivo. I minuti non supportati non sono imputati nelle zone.

La FC a riposo e massima sono facoltative ma devono essere entrambe esplicite; per TRIMP servono anche entrambi i coefficienti positivi. Il carico lineare di riserva cardiaca e il TRIMP esponenziale sono calcolati **soltanto sui minuti supportati e sulla loro FC media ponderata**. Nessuna estrapolazione al tempo totale. Se un valore supportato esce dall'intervallo di calibrazione il carico non è disponibile. Nessuna stima automatica di FC max, nessuna interpretazione di rischio o prescrizione.

La risposta contiene protocollo, distribuzione, copertura, media osservata, carichi eventualmente calcolabili, stato, revisione, hash, fonte e versioni di metodo. Il pannello nella seduta permette di eseguire i protocolli senza LLM.
