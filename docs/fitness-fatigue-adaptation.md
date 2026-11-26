# Step 12 — Fitness-Fatigue & Adaptation

C08–C10: modello discreto impulso-risposta con decadimenti esponenziali distinti, replay causale dei carichi giornalieri, valutazione descrittiva delle predizioni su osservazioni successive al cutoff. Il carico è omogeneo e la condizione iniziale è esplicita; giorni mancanti non equivalgono a riposo. Si applica il carico a fine intervallo giornaliero, dopo il decadimento di un giorno. Il guadagno e le costanti temporali sono parametri del modello, non costanti universali.

La calibrazione individuale dei parametri e il confronto con baseline devono usare dati precedenti al test (Step 14–16). Il modello non misura fitness, fatica, glicogeno o rischio di infortunio; non prescrive carico. Per valutare gli esiti sono richieste predizioni archiviate prima della loro disponibilità: il chiamante deve conservarne emissione, revisione, parametri e provenienza. Non è ancora un modello personalizzato validato.

Test su decadimento, impulso, giorni mancanti/duplicati/tardivi e holdout. Nessuna modifica di persistenza e API.
