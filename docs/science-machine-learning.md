# Step 14 — Machine Learning

Baseline supervisionata ridge L2, standardizzazione appresa solo dal training, intercetta non penalizzata, soluzione numerica con pivot e rifiuto di sistemi degeneri. Un esempio per seduta e atleta; feature disponibili al momento di emissione, outcome successivo e timestamp di disponibilità distinto. Split temporale e opzionale atleta-out; label tardive non entrano nel training, test dopo cutoff e disponibile all'as-of. Nessun riempimento di feature mancanti. Feature omogenee e schema definito dal chiamante; niente confronto tra unità incompatibili.

M01–M05: baseline interpretabile, contratto feature/outcome, controllo leakage e personalizzazione tramite training intra-atleta. La baseline non è un foundation model, non implementa ancora encoder neurali o adattamento trasferibile validato. Selezione degli iperparametri e calibrazione dell'incertezza richiedono set separati e benchmark Step 16. Il modello non è una politica di allenamento.

Test sintetici: recupero della relazione lineare, dimensioni, dati non finiti, label tardive, sessioni duplicate, atleta-out. Nessuna dipendenza nuova, nessuna persistenza o API. Validazione su dati reali ancora necessaria.
