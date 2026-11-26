# Replay nativo della riserva critica

GET /api/v1/workouts/{id}/science/reserve con metric (Speed o Power), criticalIntensity, capacity, initialRemaining e recoveryTimeConstantSeconds. I parametri sono ipotesi calibrate fornite esplicitamente, non ricavati automaticamente dalla seduta in analisi.

Il modello usa supporto continuo a partire dall'inizio del workout, mantenimento a sinistra tra campioni validi e gap massimo 5 s. Si interrompe alla prima lacuna: non può recuperare uno stato sconosciuto. Sopra soglia sottrae l'eccesso per la durata; sotto soglia recupera esponenzialmente, con tau fornito. All'esaurimento condizionale stima l'istante interno all'intervallo e interrompe il replay. Un esaurimento indica incompatibilità con le ipotesi del modello, non esaurimento fisiologico accertato. Riserva D' in m o W' in J, mai miscelate. Il riepilogo usa tutti gli intervalli, la serie esposta ha al massimo 300 punti. Nessuna prescrizione.

La calibrazione della costante di recupero richiede prove separate e non è implicita.
