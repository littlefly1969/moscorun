# Scenario pacing condizionale

POST /api/v1/science/pacing accetta 1–200 segmenti (distanza in metri, costo relativo 0,1–10), distanza complessiva 100–100000 m, e un solo obiettivo: tempo esplicito in secondi o riferimento a calibrazione personale (sorgente, from, split, until UTC). Il modello personale è ricalcolato su prove autorizzate; fuori supporto o dati insufficienti produce nessun tempo e nessuna ripartizione. Non ricade su coefficienti generici.

La ripartizione è proporzionale a distanza × costo relativo e conserva il tempo obiettivo. Il costo è dichiarato, non inferito da pendenza, vento o temperatura senza calibrazione. Il report espone fonte, evidenze, eventuale intervallo empirico e split. Il piano è uno scenario matematico, non una prescrizione né un'ottimizzazione metabolica. L'intervallo, se presente, riguarda il tempo globale, non i singoli split.
