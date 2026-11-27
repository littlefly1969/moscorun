# Centro acquisizione dati

Inventario reale dei dati remoti e diagnostica dell'ultima esecuzione del telefono, non una simulazione dei sensori.

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
  questo incremento. L'inventario non deduce gli stati di acquisizione da assenza di record.

## Diagnostica del telefono

Il report POST /api/v1/ingest/science-diagnostic è autenticato con il token PhoneBridge. Ultimo report
per dispositivo/sorgente, aggiornamento monotono per avvio della sessione; replay identico accettato.
Non contiene valori biometrici, identificativi dei record del wearable o testo libero. È incluso in
esportazione, retention e cancellazione scientifica. Migrazione additiva della tabella science_diagnostics.

- I permessi sono quelli noti al momento della lettura, non lo stato corrente del sensore.
- Letti = osservazioni mappate; tentati = osservazioni avviate all'invio (senza contare di nuovo la bisezione).
- Confermati = accettati (anche replay); rifiutati = rifiuti isolati; conflitti = sottoinsieme 409.
- Incerti = tentati - confermati - rifiutati; non tentati = letti - tentati.
- I report non sono una prova di completezza del database; dopo offline o server vecchio il telefono conserva
  il riepilogo locale del run, mentre il server mostra solo l'ultimo report ricevuto.
- Per i connettori che non implementano la telemetria, le capacità restano ignote.

L'Atlante mostra lo stato dell'archivio remoto anche prima della selezione di un giorno di analisi.
La colonna Lettore HC attuale indica le sette metriche implementate nel codice Android, indipendentemente
dal rapporto reale o dall'hardware. Le capacità correnti del dispositivo non sono inferite da assenza di dati.
