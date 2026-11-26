# Step 16 — Research Lab

Benchmark su unità indipendenti (una seduta per atleta), stesso outcome per modello e baseline, emissione predittiva prima dell'esito, cutoff esplicito e disponibilità entro as-of. MAE, RMSE, bias, differenza di MAE, numero di atleti e sedute, copertura e ampiezza empiriche degli intervalli. Walk-forward a finestre non sovrapposte con nuovo fit sul training passato e baseline media del training, anche atleta-out. Le valutazioni di ablazione devono usare le stesse unità ed endpoint e non selezionare iperparametri sul test. N-of-1: differenze abbinate descrittive con sedute distinte, senza affermare randomizzazione, assenza di carryover o causalità.

La media per seduta non è una media pesata per atleta: per trasferimento riportare anche risultati stratificati. L'errore standard non assume che campioni adiacenti siano indipendenti. Il laboratorio fornisce il motore per eseguire benchmark riproducibili ma non un risultato empirico su MoscoRun: dati, protocollo predefinito, calibrazione separata e validazione prospettica sono gate reali. Nessuna raccomandazione autonoma.

Nessuna modifica DB/API. Riferimenti e criteri: docs/ricerca/frontiera-2026.md.
