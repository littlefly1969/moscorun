# Strumenti iniziali per l’AI dell’allenamento

Libreria Node 22 senza dipendenze: qualità delle serie irregolari, allineamento causale, contesto per atleta e tempo di disponibilità, feedback, baseline e split di valutazione. Catalogo estensibile oltre ai quattro canali attuali.

## Presente e verificabile
- Campioni mancanti espliciti, duplicati discordanti esclusi, tempi nativi e copertura temporale con supporto limitato nei buchi.
- Media pesata nel tempo, intervalli e ripetizioni; il conteggio a 1 Hz non equivale a misure indipendenti.
- Allineamento solo sul passato e baseline di persistenza.
- Separazione per seduta, per tempo e per atleta.
- Osservazioni con unità, fonte, metodo, disponibilità e derivazione; feedback numerici e testo trattato come dato non fidato.
- Contesto deterministico con impronta, selezione dichiarata e nessun GPS. Non effettua chiamate LLM.
- Catalogo di segnali cardio, dinamiche di corsa, sonno, recupero, composizione, nutrizione, ambiente e feedback. Disponibilità dipendente da capacità e permessi.

`node tools/athlete-analysis/run-checks.mjs`

## Contratto iniziale
Dataset: schemaVersion, ownerId, workouts, observations, feedback.
Ogni record appartiene all’owner autenticato e ha availableAt. Un’osservazione usa metric, unit, value (anche null), time, source, method. Le stime proprietarie richiedono providerAlgorithm; i derivati richiedono derivationVersion e inputIds.
Una seduta usa id, revision, source, startedAt, endedAt, availableAt e series. Ogni serie esplicita metric, unit, availableAt e samples.
Il tempo di disponibilità è obbligatorio: una correzione ricevuta dopo asOf non può entrare nel contesto storico.

Il feedback usa una scala di prodotto versionata: non la presentiamo come questionario clinicamente validato. Campi facoltativi: RPE, fatica, stress, indolenzimento, dolore/localizzazione, sonno, fame, motivazione, disturbi gastrointestinali, note, esito e motivi della modifica. I dati alimentari usano eventi di osservazione per quantità, nutrienti, fluidi, sodio e orari, con fonte e incertezza.

## Integrazione e gate
- [ ] Persistenza .NET di osservazioni, feedback, prescrizioni e segmenti; migrazione retrocompatibile e cancellazione per owner.
- [ ] Catalogo/capability discovery da Samsung e Garmin, mapping verificato di ogni unità e mantenimento delle metriche sconosciute.
- [ ] Endpoint autenticati in lettura per AI, OpenAPI e UI feedback. ownerId deve provenire dalla sessione autorizzata.
- [ ] Snapshot delle revisioni per replay storico: il solo stato più recente non basta a riprodurre il passato.
- [ ] Contratti di tool use, limiti di costo/dimensione e autorizzazione separata per modificare un piano.
- [ ] Correlazioni contestualizzate, modelli dinamici, embedding e previsioni confrontati fuori campione.
- [ ] Benchmark di domande con risposte numeriche note e prove su secondo atleta/dispositivo.
- [ ] Replay, osservazione live e validazione prospettica prima del coaching in tempo reale.

Vedere docs/ricerca/frontiera-2026.md e docs/piano-ai-allenamento.md. L’incremento non installa pesi, non addestra modelli e non genera prescrizioni autonome; abilita esperimenti riproducibili e definisce cosa manca alla connessione applicativa.
