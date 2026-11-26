# Step 15 — Athlete Digital Twin

P09: simulazione condizionale della risposta cardiaca con modello esponenziale del primo ordine e continuità dello stato tra segmenti. Integrazione analitica della FC media, distanza cumulativa da velocità di scenario, verifica dell'orizzonte per il confronto fra alternative. Versione e cutoff della calibrazione espliciti; non si accettano parametri calibrati nel futuro.

Non è un twin validato dell'atleta: FC di equilibrio, costante temporale, FC a riposo e massima devono essere calibrate sul soggetto con train e test separati. Lo scenario non predice automaticamente metabolismo, temperatura corporea, disponibilità di glicogeno, infortuni o prestazione di gara. L'integrazione con i modelli di carico e nutrizione deve dimostrare identificabilità e valore incrementale. Gli input sono ipotetici, non osservazioni future; differenze tra simulazioni non sono effetti causali.

Test sintetici di continuità, integrali, cutoff, limiti e orizzonti. Nessun intervento autonomo, nessun MPC/RL (categoria C). Nessuna modifica di persistenza.
