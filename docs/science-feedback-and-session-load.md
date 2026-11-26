# Feedback scientifico e carico session-RPE

GET /api/v1/science/feedback?day=2026-11-25 oppure ?workoutId=<guid> (esattamente un contesto). PUT sullo stesso endpoint salva una revisione completa di feedback dichiarato. Sessione utente, owner dal principal, nessun owner fornito dal client. Risposta privata no-store.

- Diario giornaliero UTC: fatica, stress, indolenzimento, dolore, qualità sonno, fame, disturbi gastrointestinali, motivazione, durata sonno/riposo e quantità nutrizionali dichiarate (energia, carboidrati, proteine, grassi, liquidi, sodio e caffeina).
- Seduta: RPE, fatica, indolenzimento, dolore, disturbi gastrointestinali e rifornimento dichiarato. Contesto legato alla revisione esatta dell'allenamento. Dopo una revisione del workout non si riutilizza implicitamente il vecchio RPE.
- Identità deterministiche per giorno o workout/revisione, revisioni intere consecutive da 1, conflitto 409 per un salto, una modifica senza incremento o una revisione superata. Identica ripetizione idempotente.
- Il server scrive tutte le metriche del protocollo con unità canonica, anche null: la rimozione di un dato è una revisione esplicita. Zero è un valore valido; niente imputazione. Tempo di riferimento e disponibilità sono distinti. Le osservazioni legate a sedute non entrano nel confronto giornaliero.
- Session-RPE = RPE × durata attiva in minuti, UA, metodo session-rpe-active-minutes-v1. Non usa durata totale come fallback. Nessun RPE o durata attiva => carico null. Non equivale a un punteggio di rischio infortunio.

Interfacce nel cruscotto Forma e nel dettaglio allenamento. Test HTTP con PostgreSQL per identità, conflitti, azzeramento, separazione degli utenti e revisione workout; test UI di salvataggio e rappresentazione del risultato.
