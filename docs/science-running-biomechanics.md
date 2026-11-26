# Biomeccanica accoppiata della corsa

GET /api/v1/workouts/{id}/science/steps?cadenceIsStepsPerMinute=true analizza la revisione corrente di una seduta RUNNING autorizzata. Richiede conferma che il canale cadenza esprima passi totali al minuto, non cicli o pedalate.

Le serie velocità e cadenza sono ricondotte a intervalli ZOH con due estremi positivi, senza attraversare conflitti o lacune oltre 5 s. Si intersecano gli intervalli in tempo lineare; per ogni intervallo comune si calcola lunghezza del passo = 60 × velocità / cadenza, quindi media e CV di popolazione ponderati sul tempo. Non si usa il rapporto fra medie di canali con supporto differente. La copertura è relativa al tempo trascorso, non al tempo attivo.

Il risultato è una stima cinematica e il CV rappresenta variazione temporale della stima e della cadenza, non variabilità dei singoli passi. Mancanti espliciti; nessuna deduzione di asimmetria, oscillazione verticale o contatto dal solo passo. Il report include provenienza, revisione, hash e versione. Restano acquisizione dei canali specifici, confronto condizionato per pendenza e validazione su dispositivi.

Test unitari su supporto asincrono, isolamento, cutoff, assenza e unità; pannello dedicato e test frontend.
