# Analisi di intervalli RR persistiti

GET /api/v1/science/rr: source, day UTC del primo campione, context (resting/exercise), screened, timestampsAreBeatEnds, chunkId facoltativo. Senza ID elenca fino a 64 revisioni immutabili, comprese quelle storiche; con ID analizza la revisione esplicitamente selezionata. Non risolve o mescola revisioni automaticamente. Snapshot autorizzata con metadati di fonte, algoritmo, tempo, hash e disponibilità.

Solo intervalli misurati o riportati, con attestazione di screening di artefatti/ectopie e timestamp a fine battito. Non trasforma FC media in RR; non unisce chunk. Controlla la differenza tra timestamp consecutivi rispetto all'intervallo corrente (tolleranza 2 ms), rifiuta null e valori fuori 250–2500 ms. Durata 60–1200 s; protocolli diversi non sono automaticamente comparabili.

A riposo applica inoltre la politica RR v2 (300–2000 ms, salto relativo <=25%) e restituisce RMSSD e SDNN descrittivi. DFA alpha1 richiede almeno 128 NN e profilo non degenere, anche sotto sforzo. Nessuna soglia clinica, prescrizione o correzione automatica. L'attestazione non sostituisce una validazione indipendente dei dati originali.

L'interfaccia permette la scoperta e selezione esplicita. L'acquisizione dei chunk dipende dalla reale disponibilità del dispositivo e dagli adattatori di importazione.
