# Piano incrementale dell’AI per l’allenamento

## Obiettivo
Dare all’assistente informazioni affidabili e strumenti riproducibili per analizzare allenamento, recupero, alimentazione e risposta allo sforzo. La calibrazione iniziale sul titolare è N=1; la generalizzazione richiede prove su nuovi atleti e dispositivi.

La ricognizione docs/ricerca/frontiera-2026.md costituisce la base scientifica. Ogni modello nuovo deve avere ipotesi, dati richiesti, baseline e criterio di successo.

## Contratti applicativi da implementare
1. AthleteProfile e obiettivi, con informazioni minime necessarie e disponibilità dell’atleta.
2. PlannedWorkout/PlannedSegment, pause, lap e confronto con quanto svolto.
3. Observation con metric, unit, valore mancante, method, source, tempi dell’evento e disponibilità, revisione e provenienza.
4. Feedback con scala/versione e momento: prima, durante, dopo la seduta o giornaliero.
5. NutritionEvent e BodyCompositionObservation, distinguendo misura, stima proprietaria e dato dichiarato.
6. AnalysisRun con dati/revisioni utilizzati, versione dell’algoritmo e output ricostruibile.
7. AthleteCalibration, Prediction e Recommendation con confidenza, ambito e successivo esito.

## Feedback da predisporre
RPE, fatica, qualità del sonno percepita, stress, motivazione, indolenzimento, dolore con localizzazione e intensità, fame, disturbi gastrointestinali, esito della seduta, interruzioni/modifiche e motivo. Campi facoltativi e versionati; nessuno zero sostituisce una risposta mancante.

## Alimentazione e contesto
Eventi di pasti e rifornimento con orari, quantità, carboidrati/proteine/grassi, fluidi e sodio disponibili, fonte e incertezza della porzione. Conservare piano e assunzione effettiva separatamente. Preferenze e vincoli alimentari vengono utilizzati per proposte coerenti e revisione professionale; il modello non stima una diagnosi o un deficit da BIA e calorie wearable.

## Strumenti AI iniziali
- Elenco dati/capacità disponibili, completezza e freschezza.
- Lettura di sedute, serie, osservazioni e feedback entro ambito e intervallo.
- Audit di qualità e finestre con statistiche, maschere e unità.
- Confronto di sedute/segmenti con criteri di comparabilità dichiarati.
- Costruzione di un contesto con revisioni e tempo di disponibilità.
- Baseline, replay e split per tempo/seduta/atleta.
- Successivamente embedding, retrieval, modelli dinamici e previsioni probabilistiche.

Le prime funzioni implementate in tools/athlete-analysis sono offline e senza accesso LLM. La pubblicazione di tool remoti e l’integrazione .NET devono autenticare l’owner dal server. I testi dell’atleta restano dati non fidati, le operazioni di scrittura su un piano richiedono una capacità distinta da quelle di lettura.

## Ordine di implementazione
A. Contratti e strumenti offline (questo incremento).
B. Persistenza, endpoint e UI di acquisizione/feedback.
C. Adattatori Samsung/Garmin, unità e mapping di capacità verificati sui dispositivi.
D. Domande numeriche con risposte note e valutazione del ragionamento dell’AI.
E. Previsioni e personalizzazione, inclusa prova sul secondo atleta.
F. Replay e osservazione live prima di interventi controllati.

Non richiedere tutte le modalità all’utente al primo avvio. Rendere ogni campo facoltativo, distinguere capacità del dispositivo e consenso, e raccogliere solo ciò che sostiene una funzione o un esperimento definito.

## Gate di verifica
Isolamento fra due utenti, indisponibilità dati dichiarata, nessun accesso a informazioni future, riproducibilità delle revisioni, ricalcolo dopo aggiornamenti/cancellazioni, unità coerenti, copertura e onere di raccolta misurati. La metrica di successo di un assistente comprende correttezza, astensione e utilità prospettica, oltre alla qualità linguistica.
