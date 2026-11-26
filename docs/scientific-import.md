# C2 — Importazioni e revisioni

Identità deterministica SHA-256 troncata a 128 bit, per sorgente, identificativo campione, revisione e metrica; la chiave DB include l'atleta. Identificativo esterno univoco per campione, non soltanto per attività. Nessun overwrite: una correzione produce una nuova revisione; un replay identico mantiene la prima disponibilità assegnata dal clock server. Contenuto differente con stessa revisione: errore. Batch massimo 500, transazione implicita di SaveChanges, retry limitato in caso di collisione concorrente.

Usare scope dedicato e identità autenticata. Non ancora implementati registro leggibile delle chiavi esterne, cursore di sincronizzazione e selezione della revisione corrente. Nessun endpoint pubblico.
