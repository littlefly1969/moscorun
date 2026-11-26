# C2 — Importazioni e revisioni

Identità SHA-256 troncata a 128 bit per sorgente, identificativo campione, revisione e metrica; chiave DB per atleta. Identificativo esterno univoco per campione. Replay identico mantiene la prima disponibilità server, modifica richiede nuova revisione. Batch massimo 500, commit atomico, retry limitato. Metadati esterni persistiti e interrogabili con cutoff temporale; la revisione corrente richiede un ordinamento provider univoco esplicito (RevisionOrder).

ScientificSyncStore associa un checkpoint per atleta/sorgente/stream alla stessa transazione del batch. Un cursore obsoleto o un replay con nuovi campioni non avanza. Il token è opaco, non una credenziale; scope dedicato e atleta autenticato dal chiamante. L'API di acquisizione e il mapping del provider rimangono di competenza dell'agente wearable.
