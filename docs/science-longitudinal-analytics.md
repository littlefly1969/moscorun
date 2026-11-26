# Analisi longitudinali operative

GET /api/v1/science/daily?metric=weight&source=samsung-health&day=2026-11-25&baselineDays=28, con sessione utente, restituisce osservazioni da PostgreSQL e confronto intra-atleta. Parametro opzionale asOf ISO 8601 per riprodurre una lettura storica; non può superare l'orologio server. Giorno e calendario UTC; il giorno richiesto deve essere concluso al cutoff. Baseline di 7–90 giorni precedenti (almeno sette osservazioni omogenee), limite 10000 candidati.

- Metriche: restingHeartRate, hrvRmssd, hrvSdnn, sleepDuration, sleepEfficiency, weight, fatPercentage, fatMass, fatFreeMass, fatigue, soreness, sleepQuality, stressPerceived, motivation.
- Una sola osservazione giornaliera per sorgente. Non somma episodi di sonno e non aggrega campioni multipli non attestati; questi giorni sono ambigui. Il chiamante deve selezionare una sorgente con protocollo giornaliero omogeneo; il sistema confronta metodo e versione dell'algoritmo del produttore.
- Revisioni ordinate risolte all'as-of anche se la revisione sposta il timestamp. Revisioni concorrenti senza ordine univoco non vengono utilizzate. Esclude stime derivate/model-inferred e lineage non diretto. Nessuna imputazione di null, zero osservato conservato quando ammesso dall'unità.
- Risultato: valore, unità, sorgente, identificativo/tempo/disponibilità/metodo, stato di ogni giorno, mediana, MAD, robust z quando identificabile. Per masse, differenza percentuale dalla mediana; solo per il peso, pendenza Theil–Sen kg/settimana. Non equivale a variazione del grasso, diagnosi REDs o indicazione clinica di recupero.
- Dashboard Forma: scelta esplicita di sorgente, metrica, giorno e finestra; valore e stato della baseline, storico non imputato. Nessun risultato richiede il modello LLM.

Test unitari e HTTP contro PostgreSQL: isolamento owner, cutoff, revisioni, dato zero, insufficienza della baseline, robustezza del trend e dashboard. I dati di bilancia legacy non vengono automaticamente convertiti in osservazioni scientifiche; serve un adattatore con attribuzione confermata e provenienza. Non garantisce disponibilità delle metriche wearable per tutti gli utenti.
