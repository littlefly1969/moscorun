# Recupero cardiaco su seduta

GET /api/v1/workouts/{id}/science/cardiac-recovery: endOffsetSeconds (fine sforzo dall'inizio della registrazione), mode (passive-seated, passive-standing, passive-supine, active-walking), confirmed, equilibriumBpm facoltativo. Il protocollo è dichiarato dall'atleta, non dedotto dalla FC.

FC a fine sforzo e a +60/+120 s: ultimo campione valido non successivo al target, età <=5 s, timestamp effettivo esposto. Nessun valore oltre la fine della seduta o attraverso un campione conflittuale/non valido. La riduzione è FC fine − FC recupero. Non applica soglie cliniche.

Fit descrittivo esponenziale del primo ordine su primi 120 s: equilibrio fornito da protocollo separato, campioni esatti ai due estremi, nessun gap >5 s, FC iniziale sopra equilibrio, griglia tau 1–300 s. RMSE in-sample su campioni nativi, non errore predittivo o validazione individuale. Non ricostruisce RR da BPM.

API autorizzata con snapshot singola, revisione, hash, fonte e metodo; UI con protocollo esplicito. Test di segnale sintetico, mancanti e isolamento.
