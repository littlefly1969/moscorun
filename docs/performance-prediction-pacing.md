# Step 13 — Performance Prediction & Pacing

Baseline Riegel con esponente esplicito, inversione del modello CS limitata al dominio di durata della Step 04, intervallo empirico a rango finito da residui assoluti di calibrazione indipendenti dal test, ripartizione di un tempo-obiettivo per distanza e costo relativo dei segmenti.

L'esponente Riegel non è un parametro personale senza calibrazione; non trasferire automaticamente una prestazione breve alla maratona. La distanza-obiettivo e il protocollo devono essere comparabili. Il quantile è nominale e non garantisce copertura su serie temporali dipendenti o cambi di atleta/sensore; misurarla su test separato (Step 16). L'allocazione dei tempi non stima il costo energetico della pendenza o il pacing ottimale: richiede costi forniti e convalidati. Nessun suggerimento autonomo in tempo reale.

P01–P08: baseline e contratti disponibili; restano personalizzazione su storico, calibrazione dei costi, residui per distanza/atleta, verifica su gare escluse e confronto prospettico. Nessuna modifica DB/API.

`PersonalRaceCalibration.Fit` stima intercetta ed esponente Riegel tramite Theil–Sen in log-spazio su sforzi massimali attestati, dello stesso atleta e protocollo, entro una finestra di training e cutoff. Richiede almeno tre distanze distinte e rapporto max/min almeno 2, nessuna sessione duplicata, esponente nel dominio 1–2. Il modello conserva owner, protocollo, range e cutoff; `Predict` rifiuta estrapolazioni fuori range e predizioni precedenti al cutoff. Il residuo assoluto mediano in log-tempo descrive il fit; non è un intervallo di previsione. Calibrazione empirica e valutazione fuori campione rimangono separate.
