# C6 — Registry modelli e Digital Twin

Registry privato per atleta e versione: artefatto binario (max 16 MiB), SHA-256, formato, feature schema, cutoff di training, configurazione e metriche di validazione JSON. Versioni immutabili e replay idempotente. Lo store non esegue l'artefatto.

Run atomico: modalità operativa o backtest, istante decisionale e target, disponibilità server, ingressi validati transitivamente alla decisione (owner e lineage), output scalari con intervalli, stato JSON del twin. Un modello operativo dev'essere già registrato alla decisione; un backtest non può usare training o input futuri e non modifica lo stato operativo corrente: gli output di backtest restano nel registro delle esecuzioni e non entrano nelle osservazioni usate dall'AI Coach. Query as-of e ultimo stato operativo.

L'agente ML deve produrre artefatti e run secondo questi contratti; la persistenza non attribuisce validità scientifica automatica ai modelli. Gli artefatti non devono essere eseguiti dal processo di persistenza. Test PostgreSQL per round-trip, revisioni, isolamento e anti-leakage.
