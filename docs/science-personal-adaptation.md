# Calibrazione personale fitness-fatigue

GET /api/v1/science/adaptation: source, from, split, until (date UTC, fine esclusa), distanceMeters. Finestra massima 180 giorni. Una transazione repeatable-read legge carichi session-RPE attestati (tutte le attività) e prove massimali piane della sorgente selezionata, stessa distanza ±0,5 m. Giornate mancanti, incomplete o con revisioni incompatibili non diventano zero.

Endpoint osservato: velocità della prova. Il modello impulso-risposta ha stati iniziali zero per ipotesi all'inizio della finestra, non misurati. Griglia dichiarata: tau fitness 14/28/42/60 giorni, tau fatigue 3/7/10 giorni, guadagni 0/0,0001/0,0005/0,001 m/s per UA; intercetta appresa. Almeno sei prove di addestramento in giorni distinti. Se manca un carico precedente o è disponibile dopo l'inizio della prova, il fit si astiene.

Predizioni retrospettive delle prove successive al cutoff con solo carichi disponibili al loro inizio. Confronto MAE/RMSE contro media delle velocità del fit sullo stesso insieme di almeno tre esiti; nessuna selezione dei parametri sull'holdout. Il risultato non prova causalità, non equivale a predizioni emesse in passato né garantisce validità prospettica. Il modello può essere meno accurato della baseline. Previsione corrente condizionale solo per until uguale a oggi UTC e storia completa disponibile. Nessuna prescrizione.

Non ricostruisce revisioni di workout sostituite; usa le revisioni correnti e le loro effettive disponibilità, escludendo dati tardivi. Report include evidenze e carichi mancanti, parametri, baseline e errori. Le finestre e i parametri sono scelte metodologiche dichiarate, non valori universali.
