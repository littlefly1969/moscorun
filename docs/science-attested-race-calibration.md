# Calibrazione prestazionale da prove attestate

La rotta GET /api/v1/science/race-calibration richiede source, from, split, until (date UTC, estremi di fine esclusi), targetMeters. Finestra <=366 giorni. Usa snapshot coerente delle revisioni correnti di allenamento e del feedback; ogni prova deve avere attestation maximalFlatRun=1 per percorso piano misurato, sforzo massimale e durata trascorsa senza pause. Non si presume massimale il miglior allenamento.

Il modello personale Riegel Theil–Sen usa solo prove concluse prima dello split e disponibili entro lo split. Servono almeno 3 distanze distinte, rapporto max/min >=2 e pendenza plausibile. Le prove tardivamente attestate non entrano nel fit. I risultati delle prove successive allo split calibrano il quantile finito degli errori assoluti, senza riaddestrare il modello. Distanze fuori supporto non producono previsioni; minimo 5 errori in-range e copertura nominale 80%, non garantita in serie dipendenti. Non si tratta di validazione prospettica indipendente o prescrizione di pacing.

Il report include evidenze con ID, revisioni, hash, disponibilità, ruolo e residui; non ricostruisce allenamenti sovrascritti. Prove sovrapposte causano rifiuto; fonti e atleti non vengono mescolati. Le attestazioni retrospettive sono autodichiarate, non verifiche indipendenti del percorso. Nessuna previsione senza prove reali.
