# Persistenza scientifica PostgreSQL — incremento 1

Dipende dal contratto Scientific Data Foundation (#38). Nessuna modifica alle API, ai motori o alle tabelle degli allenamenti esistenti.

- Tabella `moscorun.scientific_observations`: identità composta atleta/osservazione, valore nullable, unità, sorgente, metodo, versione e identificativi degli ingressi.
- Tempi UTC distinti: evento e disponibilità effettiva. Il chiamante deve fornire quest'ultima senza retrodatare informazioni apprese successivamente.
- Indice atleta/tempo/disponibilità; lettura point-in-time con verifica del lineage tramite ScientificSnapshot.
- Store scoped: il chiamante server deve passare l'identità ricavata dall'autenticazione, mai un ownerId arbitrario dal client.
- Inserimento append-only: l'identità composta impedisce duplicati, ma il replay idempotente e la riconciliazione delle revisioni del provider non sono ancora implementati. Gli ingressi devono essere già presenti per lo stesso atleta; sono memorizzati come uuid[] (integrità applicativa, non FK individuali).
- Migrazione additiva compatibile con rollback dell'immagine N-1. Non eseguire Down su dati da conservare.
- I test PostgreSQL reali verificano round-trip, isolamento, null, lineage e coerenza modello/migrazione.

## Prossimi incrementi

1. Idempotenza per identificativi/revisioni dei provider e batch transazionali, collegamento opzionale all'allenamento e alla fonte dispositivo.
2. Ingestione dei wearable e feedback autenticato con timestamp di disponibilità assegnati dal server.
3. Query paginata e proiezioni per metriche ad alta frequenza; mantenere le serie compresse esistenti.
4. Snapshot/versioni dei modelli, audit, retention ed export/cancellazione dei dati.

La lettura corrente carica tutte le osservazioni disponibili per un atleta; non è progettata per campioni PPG/ECG ad alta frequenza. Nessun cambiamento alle dipendenze.
