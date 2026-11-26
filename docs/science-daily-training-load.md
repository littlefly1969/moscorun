# Carico longitudinale session-RPE

La rotta autenticata GET /api/v1/science/load?last=YYYY-MM-DD aggrega 35 giorni UTC conclusi, dalla revisione corrente degli allenamenti e dai feedback versionati. Non ricostruisce revisioni storiche sovrascritte degli allenamenti.

- Il diario giornaliero deve attestare trainingDayComplete=1 dopo la chiusura del giorno. Zero revoca l'attestazione; null significa non dichiarato.
- Un giorno attestato senza sedute vale zero. Le sedute richiedono durata attiva e RPE della revisione corrente.
- Allenamenti sovrapposti, a cavallo di due giorni o aggiornati dopo l'attestazione rendono il giorno indeterminato. La nuova attestazione è necessaria dopo importazioni tardive.
- Monotonia e strain richiedono sette giorni completi; confronto delle medie degli ultimi sette con i 28 precedenti richiede 35 giorni completi. Denominatori nulli non producono rapporti. Nessuna inferenza di rischio infortunio.
- La lettura usa una transazione repeatable read e filtra per proprietario. L'API espone disponibilità temporale, stato e copertura delle sedute.
- I vecchi diari non attestati restano mancanti; una nuova revisione del diario può aggiungere il campo.
