# C8 — Backup, verifica e ripristino scientifico

Il backup esistente usa pg_dump custom e ora produce un checksum SHA-256 separato, verifica la lista dell'archivio prima della pubblicazione, usa nome univoco e ruota insieme dump/checksum. Il comando verify-backup controlla integrità e struttura. I backup legacy senza checksum vengono segnalati e controllati strutturalmente.

Restore: verifica prima di fermare l'app; conferma RIPRISTINA; pg_restore in singola transazione con exit-on-error; riavvio e readiness. In caso di errore il comando fallisce e tenta di riavviare l'app, senza dichiarare un ripristino riuscito. Prima di sovrascrivere un database reale conservare una copia separata dello stato corrente.

La CI usa lo stack isolato, popola tutte le otto tabelle scientifiche (revisioni, metadati nativi, feedback, chunk lossless, registry, run, checkpoint, audit e fence), verifica rifiuto di archivio alterato, simula perdita e confronta digest e conteggi dopo restore. Mantiene le verifiche legacy, rollback e N-1.

## Operazioni sul server

- Copiare dump e file .sha256 insieme; proteggere i backup (dati fisiologici e artefatti), cifrare la copia off-site e limitare l'accesso.
- Pianificare backup con RPO/RTO definiti dall'amministratore, monitorare età dell'ultimo backup e spazio disco; provare periodicamente il ripristino su ambiente isolato.
- Conservare separatamente e in modo sicuro la configurazione .env/certificato di Data Protection necessari al ripristino.
- Non ripristinare indiscriminatamente backup antecedenti a una cancellazione: riapplicare le richieste di erasure/fence prima di rendere disponibili i dati.
- Nessun backup o restore di produzione è stato effettuato in questa PR.
