# Centro acquisizione dati

Incremento 1: inventario reale dei dati remoti, non una simulazione dei sensori.

- Endpoint utente autenticato GET /api/v1/science/inventory. Raggruppamento per metrica, unità, sorgente,
  metodo, dispositivo e contesto; numero di revisioni e valori non nulli, primo e ultimo istante,
  disponibilità server. Chunk e campioni sono contati separatamente, senza decodifica del blob.
- GET /api/v1/science/inventory/observations: storico paginato per metrica e sorgente, tutte le revisioni,
  valori nulli espliciti, cutoff e cursore (time, id). Nessuna imputazione.
- Le pesate native sono distinte per dispositivo e attribuzione (anche quelle escluse).
- L'assenza di record non prova assenza di un sensore o di permessi. L'inventario non indica
  che una misura sia valida per una specifica analisi e non somma le revisioni come misure uniche.
- L'Atlante rinvia all'inventario anche per metriche non supportate dal motore daily.
- Limiti: i dati legacy delle sedute restano nel Cronoscopio; chunk grezzi censiti ma non navigabili in
  questo incremento; permessi e contatori del telefono non ancora trasmessi al server. L'inventario
  non deduce gli stati di acquisizione da assenza di record.
