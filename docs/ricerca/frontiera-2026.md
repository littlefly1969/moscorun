# MoscoRun — Ricerca di frontiera per l’AI della corsa
Data della ricognizione: 17 novembre 2026. Obiettivo: definire esperimenti scientifici e strumenti riproducibili per un sistema generalizzabile, partendo dal primo atleta.

## 1. Domanda di ricerca

Come apprendere una rappresentazione della relazione fra sforzo, risposta fisiologica, recupero e rifornimento che rimanga utile su atleti e dispositivi nuovi e migliori decisioni verificabili di allenamento?

L’interfaccia conversazionale è uno strumento di accesso. La ricerca riguarda qualità delle osservazioni, stima dello stato dell’atleta, trasferimento, previsione e utilità dell’intervento.

Questa è una ricognizione critica mirata, non una revisione sistematica PRISMA. Include lavori recenti 2025–2026, precedenti metodologici necessari e documentazione primaria. Sono stati esaminati abstract, metodi e risultati accessibili; i lavori disponibili soltanto in sintesi non sono trattati come repliche verificate. Il cutoff è la data indicata: lavori successivi non rientrano nell’analisi.

## 2. Lavori prioritari e loro reale trasferibilità

### A. Agenti che lavorano sui dati

| Lavoro | Risultato e contributo | Limite da rispettare | Esperimento MoscoRun |
| --- | --- | --- | --- |
| PHIA — Nature Communications, 12 gennaio 2026 [R1] | Agente che usa ragionamento, calcolo tramite codice e recupero di informazioni per domande sui wearable. Benchmark di oltre 4000 domande; gli autori riportano 84% di accuratezza sulle domande numeriche e valutazioni esperte sulle risposte aperte. | La qualità del question answering non dimostra miglioramento delle prestazioni sportive. Il benchmark non sostituisce la verifica su dati Garmin/Samsung reali. | Costruire domande con risposta numerica nota su sedute, buchi, tempi, confronti e feedback; confrontare LLM solo, contesto aggregato e strumenti verificati. |
| The anatomy of a personal health agent — Nature, 21 ottobre 2026 [R2] | Architettura con ruoli distinti per analisi, competenza di dominio e coaching; valutazione su 11 task. | Valutare agenti e consigli non equivale a validare una politica di allenamento in uno studio prospettico. | Separare calcolo, interpretazione e proposta; ogni affermazione numerica richiama un risultato e una revisione dei dati. |
| PH-LLM — Nature Medicine, 2025 [R3] | Modello adattato a coaching di sonno/fitness e dati numerici giornalieri; valutazione su 857 casi e compiti di conoscenza e sonno percepito. | Non è un modello della dinamica a un secondo; dati aggregati e valutazioni di casi non certificano efficacia sulla corsa. | Usare rubriche di qualità, personalizzazione, assunzioni infondate e utilità per valutare l’assistente MoscoRun. |

Decisione: realizzare strumenti di calcolo con contratti espliciti prima di delegare interpretazioni complesse al modello linguistico. L’AI potrà scegliere operazioni autorizzate e spiegare i risultati. Il calcolo numerico non dipenderà dal fatto che il modello ricordi o ricostruisca correttamente migliaia di campioni nel testo.

### B. Foundation model, embedding e trasferimento

| Lavoro | Contributo | Compatibilità e limite | Decisione |
| --- | --- | --- | --- |
| SensorLM — NeurIPS 2025 [R4] | Allineamento fra segnali wearable e linguaggio; pretraining su circa 59,7 milioni di ore e oltre 103000 persone, con task di riconoscimento e retrieval. | Le modalità e la preparazione degli ingressi devono essere compatibili. Codice e dati di riferimento pubblicati non implicano automaticamente disponibilità di tutti i dati, pesi o diritti d’uso necessari. | Valutare retrieval di pattern e caption controllate; confrontare con feature semplici e verificare artefatti disponibili. |
| Scaling Wearable Foundation Models — 2025 [R5] | Modello multimodale e studio dello scaling su segnali longitudinali, a risoluzione per minuto, da una grande popolazione. | Gli stream cardiaci/GPS di una corsa non ricostruiscono le altre modalità o un’intera giornata del protocollo originale. | Studiare un encoder di stato giornaliero distinto dall’encoder della seduta. |
| Beyond Sensor Data — ICML 2025 [R6] | Modelli di dati comportamentali su scale temporalmente e fisiologicamente rilevanti, studiati su grandi raccolte e diversi task di salute. | Un embedding utile per task di salute non è già un predittore della risposta all’allenamento. | Confrontare rappresentazioni giornaliere e delle sedute; misurare valore aggiuntivo rispetto a storia e carico interpretabili. |
| PaPaGei — ICLR 2025 [R7] | Foundation model aperto di PPG, con pesi e codice, addestrato su segnali ottici e valutato su più compiti. | Richiede forma d’onda PPG; i bpm a 1 Hz e una HRV giornaliera non sono ingressi equivalenti. | Ramo successivo se il sensore consente acquisizione grezza con qualità adeguata; non simularne l’ingresso dai bpm. |
| TS2Vec — AAAI 2022 [R8] | Riferimento per rappresentazioni temporali a più granularità. | È una baseline metodologica precedente, non una novità del 2026 o una prova di superiorità nel nostro dominio. | Baseline di embedding contro cui confrontare proposte specifiche della corsa. |

Decisione: evitare addestramento da zero di un grande foundation model sul primo atleta. Preparare gli ingressi, fare prove di trasferimento dove possibili e documentare esplicitamente quando un modello non è applicabile.

### C. Modelli fisiologici e rappresentazioni personali

| Lavoro | Contributo | Limite | Applicazione |
| --- | --- | --- | --- |
| Nazaret et al. — npj Digital Medicine, 2023 [R9] | Modello ibrido con equazioni della risposta cardiaca e componenti neurali personalizzate, studiato su una grande raccolta di allenamenti con contesto ambientale. | Già esistente: “embedding personale + fisiologia” non basta come rivendicazione di novità. I risultati non si trasferiscono automaticamente al nostro sensore. | Riferimento per la previsione condizionata allo sforzo e per distinguere parametri dell’atleta e stato della seduta. |
| Physiological-model-based neural network — Computer Methods and Programs in Biomedicine, 2026 [R10] | Combina fisiologia e rete neurale per la relazione metabolismo–FC, con dati di più attività e analisi della sensibilità. | La disponibilità di variabili metaboliche nel lavoro originale limita l’applicazione diretta ai soli dati dell’orologio. | Confrontare modelli con vincoli e modelli puramente statistici, separando variabili misurate e latenti. |
| Digital twin fisico per trail — proof of concept, 2026 [R11] | Calibrazione fisiologica e costo della pendenza per prevedere prestazioni su percorsi eterogenei; dati di 13 gare nel proof of concept. | Campione e validazione limitati; un buon risultato nello stesso piccolo storico non dimostra generalizzazione ad altri atleti o percorsi. | Baseline di simulazione del percorso, con test temporale e su gare escluse. |

Stato latente non significa dato osservato. Fatica, glicogeno e disponibilità a sostenere lavoro possono essere variabili di modello; non vanno visualizzati come percentuali misurate se non disponiamo di osservazioni che li identifichino.

### D. Biomeccanica, dinamiche e carico meccanico

| Lavoro | Contributo | Dati necessari e limite | Applicazione |
| --- | --- | --- | --- |
| GaitDynamics — Nature Biomedical Engineering, gennaio 2026 [R12] | Modello generativo per cammino e corsa, stima di forze e cinematica, con codice, dati e modelli pubblici. | Gli esempi usano dati cinematici e modelli muscoloscheletrici. Non può essere alimentato direttamente con solo FC, velocità e cadenza. | Ramo biomeccanico successivo con video/IMU e riferimenti, dopo aver verificato l’utilità incrementale. |
| Bone stress digital twin — npj Digital Medicine, 2025 [R13] | Anatomia individuale, simulazioni meccaniche, due sensori inerziali e domain adaptation per stimare stress ossei. | La previsione dello stress simulato non è una validazione prospettica della previsione di infortuni. Richiede anatomia e sensori assenti nei dati attuali. | Definire protocolli di acquisizione specifici prima di considerare questo tipo di inferenza. |

Le metriche di dinamica di corsa del wearable — tempo di contatto, oscillazione, bilanciamento, lunghezza del passo — restano utili come osservazioni proprie del dispositivo. Non sostituiscono automaticamente forze di reazione al suolo, consumo di ossigeno o carico interno sui tessuti.

### E. Durability, nutrizione e tempo reale

| Lavoro | Risultato o ipotesi | Limite critico | Esperimento |
| --- | --- | --- | --- |
| Test–retest della resilience nella corsa — 2026 [R14] | Su 26 runner allenati la ripetibilità cambia molto tra le variabili e i loro deterioramenti. | Una variazione apparente può essere rumore del test; ogni endpoint va qualificato. | Prima misurare ripetibilità e errore tipico, poi giudicare un cambiamento o il beneficio di un intervento. |
| Carboidrati e costo della corsa in maratoneti — 2025 [R15], con discussione metodologica 2026 [R16] | Otto maratoneti maschi di alto livello in prove controllate; endpoint metabolici e di economia. | Campione piccolo, contesto di laboratorio e diversi rapporti dei carboidrati; esiste una critica metodologica pubblicata. Non prova una dose universale o un miglioramento della maratona per tutti. | Registrare dose, miscela, forma, orario e tolleranza; protocollo individuale concordato e risultati prestabiliti. |
| Nutrizione e durability nel ciclismo — 2026 [R17] | Prove controllate su atleti allenati studiano rifornimento e conservazione della capacità dopo sforzo prolungato. | Sport, protocolli e popolazioni differenti dalla corsa ricreativa; distinguere prestazione residua e singoli marker fisiologici. | Ispirazione per un disegno sperimentale, non trasferimento diretto della prescrizione. |
| Cadence optimization con digital twin — BMC Sports Science, 2026 [R18] | Studio esplorativo su 12 specialisti degli 800 m con velocità, cadenza, ossigenazione muscolare e indicazioni audio. | Piccolo campione, strumentazione dedicata e possibile effetto psicologico del feedback; gli autori discutono la necessità di un controllo audio. | Mostra la fattibilità di un circuito di feedback; non dimostra efficacia per lungo, mezza o maratona. |
| Ottimizzazione del carico tramite DQN — Scientific Reports, 2026 [R19] | Propone apprendimento di politiche in un modello di transizione/digital twin usando dati di una stagione. | Ottimizzare una ricompensa in un simulatore non dimostra efficacia o sicurezza su atleti reali. Occorre controllare come il simulatore e la ricompensa sono stati validati. | Benchmark offline contro regole semplici; nessuna esplorazione autonoma su atleti per addestrare l’agente. |

La disponibilità di studi recenti non implica che la prescrizione completamente automatica sia risolta. L’obiettivo iniziale rimane costruire evidenza per decisioni ristrette e verificabili.

## 3. Le ipotesi originali da mettere alla prova

### H1 — Rappresentazione della relazione sforzo–risposta
Un embedding che distingue sforzo esterno, risposta interna e contesto migliora la previsione rispetto a un embedding unico e a baseline fisiologiche semplici.

Ingressi: serie di sforzo, FC, qualità, fase della seduta, meteo e storia già disponibile. Uscite iniziali: FC futura su orizzonti predefiniti e residui rispetto alla risposta attesa. Non attribuire automaticamente il residuo a una causa.

Confronti: persistenza, regressione dinamica, modello fisiologico, feature interpretabili, encoder condiviso, encoder con adattamento personale. La ricerca è interessante solo se migliora fuori campione o ottiene prestazioni simili con minori dati/costi.

### H2 — Personalizzazione trasferibile
Un backbone comune con calibrazione individuale ridotta mantiene utilità su un atleta non visto e su un altro dispositivo.

Primo atleta: verifica della catena e ripetibilità. Secondo atleta Garmin: prova iniziale di trasferimento e differenze di sensore. Validazione più ampia: altri atleti esclusi dall’addestramento. Due atleti non dimostrano generalizzazione di popolazione.

Misurare la curva di calibrazione con quantità crescenti di storia, fissate prima della prova. Escludere dati futuri, persone di test dal pretraining locale e qualsiasi normalizzazione che usi il test.

### H3 — Informazione aggiuntiva di recupero e rifornimento
Feedback, sonno, attività quotidiana e rifornimento migliorano spiegazione/predizione della risposta e della durability oltre a carico e meteo.

Eseguire confronti con eliminazione di gruppi di variabili. Misurare beneficio e onere di raccolta. Un miglioramento predittivo non identifica automaticamente un effetto causale del nutriente, del sonno o del carico.

### H4 — Coaching con incertezza e astensione
Suggerimenti limitati, basati su previsioni calibrate e confronto con la prescrizione, migliorano esecuzione/tollerabilità rispetto a promemoria o regole semplici.

Ordine: replay con dati disponibili nel passato, osservazione live senza suggerimenti, prova controllata di pochi tipi di indicazione. Valutare numero di interventi, falsi allarmi, aderenza, esperienza dell’atleta, batteria e beneficio prospettico.

MPC e politiche apprese rimangono ipotesi di sviluppo. Prima servono stime affidabili e un confronto con controlli semplici.

## 4. Cosa acquisire e come rappresentarlo

### Livello immediato: già coerente con gli strumenti abituali
- Allenamento previsto, segmenti, scopo, esito e modifica della seduta.
- Serie FC, velocità, cadenza, potenza e GPS/altitudine se presenti, senza inventare canali assenti.
- Pause, lap, tempi nativi, origine, modello del dispositivo e versione del mapping.
- RPE, fatica, stress, sonno percepito, indolenzimento, dolore con localizzazione, motivazione.
- Sonno giornaliero, FC a riposo, HRV valida se fornita, attività quotidiana.
- Ambiente e contesto di percorso, distinguendo stazione meteo e misura locale.
- Peso, impedenza e composizione disponibile con metodo e condizioni.
- Eventi nutrizionali: quantità, nutrienti, orari, fluidi, sodio e tolleranza. Dati incompleti dichiarati.

### Livello estensibile, guidato dalle capacità reali
- Dinamiche di corsa, temperatura, respirazione, SpO2 e altri segnali disponibili.
- VO2max, soglie, training effect/load, recovery time, stress e Body Battery come stime proprietarie, mantenendo il produttore e la versione quando nota.
- Composizione corporea distinta per misura fisica e output algoritmico.
- Dati grezzi PPG, RR/IBI, accelerometria e giroscopio solo con catena di acquisizione adeguata.
- Eventuali sensori di ossigenazione o cinematica per sottostudi dedicati.

Il catalogo iniziale contiene 84 voci con unità e categorie. È un modello di capacità: non afferma che ogni wearable fornisca tutte le voci. I valori mancanti restano mancanti, compresi gli esiti non rilevati. Le metriche sconosciute vengono conservate nella rappresentazione nativa e richiedono mapping prima del confronto.

Ogni osservazione deve avere tempo dell’evento, tempo di disponibilità, fonte, unità e metodo. Ogni dato derivato deve indicare versione e ingressi. Questa disciplina consente di riprodurre le risposte dell’AI e di riconoscere errori di conversione o informazioni future.

## 5. Energia, composizione e alimentazione: identificabilità

Usare una contabilità coerente del dispendio totale, distinguendo riposo, termogenesi, attività quotidiana ed esercizio netto. Calorie attive, lorde e totali devono avere definizioni esplicite per evitare doppio conteggio.

Peso e diario alimentare non identificano automaticamente il consumo: errori di registrazione e variazioni d’acqua possono produrre pattern simili. Le calorie del wearable e la BIA hanno errori dipendenti da metodo e contesto [R20–R21].

La proposta nutrizionale iniziale deve sostenere il lavoro richiesto e il recupero, con preferenze e tolleranza. I calcoli devono essere trasparenti e le strategie riviste da competenza nutrizionale appropriata. Il modello linguistico può spiegare o proporre alternative coerenti; non deve inventare quantità ingerite, glicogeno residuo o una diagnosi di disponibilità energetica.

Non derivare una diagnosi REDs da apporto incompleto, calorie dell’orologio e massa magra della bilancia. Il consenso IOC e la metodologia associata evidenziano limiti importanti nella misura di queste componenti [R22]. Il CGM non misura il glicogeno muscolare e la sua utilità per la prestazione negli atleti senza diabete rimane da dimostrare nei contesti pertinenti [R23].

## 6. Benchmark e criteri di successo

| Livello | Endpoint principale | Controllo necessario |
| --- | --- | --- |
| Dati | Copertura, unità, allineamento e campioni mancanti corretti | Fixture con risposte note e confronto al file originale |
| Question answering | Correttezza numerica, provenienza, assunzioni non supportate | LLM solo vs contesto vs strumenti; valutazione cieca delle risposte |
| Previsione | MAE/RMSE per orizzonte e atleta, calibrazione e ampiezza degli intervalli | Test su sedute future; baseline identiche per dati e orizzonte |
| Trasferimento | Errore e dati necessari alla calibrazione del nuovo atleta | Athlete-out e device-out; risultati per sottogruppi quando il campione lo permette |
| Raccolta | Miglioramento per costo/onere di ogni dato aggiuntivo | Ablazione e misura dell’aderenza |
| Intervento | Esecuzione, tollerabilità e beneficio prospettico | Confronto controllato, carryover considerato e endpoint predefiniti |
| Live | Ritardo end-to-end, stabilità delle indicazioni e batteria | Prova reale sul dispositivo e simulazione di buchi/disconnessioni |

Secondi adiacenti e finestre sovrapposte non sono unità indipendenti. Metriche e intervalli di incertezza vanno stimati a livello appropriato di seduta e atleta. La conformal prediction non garantisce automaticamente copertura dopo cambio di atleta/dispositivo o in serie dipendenti: la calibrazione va verificata nel regime di utilizzo.

## 7. Collegamento alle tre PR

1. **ChatGPT plan client:** flusso ufficiale di autorizzazione e utilizzo del piano personale; identità, credenziali, modelli e limiti separati dai dati dell’atleta. L’accesso a un modello non rende scientificamente valida una raccomandazione.
2. **Garmin Personal API:** recupero autorizzato e preservazione delle osservazioni native, per avviare il secondo caso sperimentale e il confronto fra sorgenti.
3. **AI foundations:** catalogo, feedback, osservazioni, qualità, contesto riproducibile e baseline; questa base permette di valutare agenti e modelli senza dipendere dalla sola conversazione.

Gli incrementi iniziali contengono librerie verificabili e CI. L’integrazione nel backend, le interfacce e le prove con account reali restano gate espliciti delle PR draft.

## 8. Fonti primarie

- [R1] Merrill et al., Transforming wearable data into personal health insights using large language model agents. Nature Communications (2026). https://www.nature.com/articles/s41467-025-67922-y
- [R2] The anatomy of a personal health agent. Nature (2026). https://www.nature.com/articles/s41586-026-11161-8
- [R3] Khasentino et al., A personal health large language model for sleep and fitness coaching. Nature Medicine (2025). https://www.nature.com/articles/s41591-025-03888-0
- [R4] Zhang et al., SensorLM. NeurIPS (2025). https://proceedings.neurips.cc/paper_files/paper/2025/hash/42cd98f0e7520d4a63c34891ac1c972f-Abstract-Conference.html ; codice di riferimento https://github.com/Google-Health/consumer-health-research/tree/main/sensorlm
- [R5] Narayanswamy et al., Scaling Wearable Foundation Models (2025). https://research.google/pubs/scaling-wearable-foundation-models/
- [R6] Erturk et al., Beyond Sensor Data: Foundation Models of Behavioral Data from Wearables Improve Health Predictions. ICML (2025). https://proceedings.mlr.press/v267/erturk25a.html
- [R7] Pillai et al., PaPaGei. ICLR (2025). https://proceedings.iclr.cc/paper_files/paper/2025/hash/7871f51d332c8661cc729539d7987b21-Abstract-Conference.html ; https://github.com/Nokia-Bell-Labs/papagei-foundation-model
- [R8] Yue et al., TS2Vec. AAAI (2022). https://arxiv.org/abs/2106.10466
- [R9] Nazaret et al., Modeling personalized heart rate response to exercise and environmental factors with wearables data (2023). https://www.nature.com/articles/s41746-023-00926-4
- [R10] Physiological-model-based neural network for modeling the metabolic–heart rate relationship during physical activities (2026). https://doi.org/10.1016/j.cmpb.2026.109240
- [R11] A Physics-Based Digital Twin for Trail Running Race Performance Prediction: A Proof-of-Concept Study (2026). https://pubmed.ncbi.nlm.nih.gov/42356703/
- [R12] Tan et al., GaitDynamics (2026). https://www.nature.com/articles/s41551-025-01565-8 ; https://github.com/stanfordnmbl/GaitDynamics
- [R13] Integrating personalized shape prediction, biomechanical modeling, and wearables for bone stress prediction in runners (2025). https://www.nature.com/articles/s41746-025-01677-0
- [R14] Test-Retest Reliability of Physiological Resilience During and After Prolonged Moderate-Intensity Running in Well-Trained Runners (2026). https://doi.org/10.1002/ejsc.70178
- [R15] Ravikanti et al., 13C-labelled glucose-fructose show greater exogenous and whole-body CHO oxidation and lower O2 cost of running ... (2025). https://pubmed.ncbi.nlm.nih.gov/41191077/
- [R16] Does 120 g/h carbohydrate ingestion really confer a metabolic advantage in elite marathoners? Methodological concerns and alternative interpretations (2026). https://doi.org/10.1152/japplphysiol.00303.2026
- [R17] Graded Carbohydrate Ingestion ... Critical Power Following 3 h ... (2026). https://doi.org/10.1111/sms.70326 ; studio distinto 100 vs 60 nel ciclismo https://pubmed.ncbi.nlm.nih.gov/42841583/
- [R18] Qiu et al., Real-time AI-Driven cadence optimization in elite 800-m runners ... (2026). https://doi.org/10.1186/s13102-026-01745-2
- [R19] Zhang et al., Adaptive training load optimization for track and field athletes: A reinforcement learning approach (2026). https://www.nature.com/articles/s41598-026-41946-w
- [R20] Keeping Pace with Wearables (2024). https://pmc.ncbi.nlm.nih.gov/articles/PMC11560992/
- [R21] Real-world assessment of MFBIA (2025). https://pubmed.ncbi.nlm.nih.gov/40957934/
- [R22] IOC REDs e metodologia (2023). https://doi.org/10.1136/bjsports-2023-107359
- [R23] Application potential of CGM in elite endurance athletes without diabetes (2025). https://doi.org/10.1186/s44410-025-00013-7
