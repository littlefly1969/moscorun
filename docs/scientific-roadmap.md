# Roadmap scientifica pre-LLM

Approvazione progettuale: 25 novembre 2026. Riferimenti: [ricerca di frontiera](ricerca/frontiera-2026.md) e [piano AI](piano-ai-allenamento.md).

## Scope

Tutti i 56 moduli A e B (37 A, 19 B). D10, D11, P10, M06 (categoria C) rinviati. Nessun LLM prima dei gate scientifici. Un modulo privo di input reali non produce valori inventati: stato non disponibile e validazione su fixture/dataset compatibili, distinta dalla validazione sul singolo atleta.

## Pacchetti incrementali

| Step | Pacchetto | Moduli / output |
| --- | --- | --- |
| 01 | Scientific Data Foundation | Osservazioni, catalogo, feedback, revisioni, persistenza, autorizzazione |
| 02 | Wearable Extended Acquisition | Samsung/Garmin, RR/IBI, dinamiche, sonno, capacità e dati originali |
| 03 | Signal Processing | Qualità, finestre, allineamento causale, segmentazione |
| 04 | Performance Physiology | F01–F06, F09, F10 |
| 05 | Durability | D01–D04 |
| 06 | Training Load | C01–C07 |
| 07 | Biomechanics | D05–D09 |
| 08 | Cardiac Dynamics & HRV | F07, F08, F11, F12, C11 |
| 09 | Recovery & Sleep | C12, C13 |
| 10 | Environmental Physiology | N07, N08 |
| 11 | Nutrition & Body Composition | N01–N06 |
| 12 | Fitness-Fatigue & Adaptation | C08–C10 |
| 13 | Performance Prediction & Pacing | P01–P08 |
| 14 | Machine Learning | M01–M05 |
| 15 | Athlete Digital Twin | P09 |
| 16 | Research Lab | N-of-1, baseline, validazione temporale, ablation |
| 17 | Analytics API & Dashboard | Contratti, visualizzazione autonoma |
| 18 | LLM | Autorizzazione, tool calling, benchmark; dopo gate pre-LLM |

## Gate per ogni incremento

- Restore bloccato, format, build Release e test secondo CI esistente.
- Nessuna modifica dei contratti API o dello schema senza migrazione/test coerenti.
- Risultati con versione, unità, provenienza, qualità, disponibilità e incertezza quando identificabile.
- Nessun leakage temporale o tra atleti. Test sintetici non equivalgono a validazione prospettica.
- PR successive basate su incrementi verificati; nessun merge implicito.

## Step 01: primo incremento

Contratto di dominio .NET senza dipendenze nuove: catalogo v1, osservazioni immutabili, distinzione dei metodi, valori mancanti, selezione as-of e controllo della derivazione. Le metriche sconosciute sono conservabili ma non canonizzate. Il catalogo C# deriva da `tools/athlete-analysis/catalog.json`; mantenere sincronizzate le due rappresentazioni (test automatico di parità incluso).

Restano da completare nello Step 01: persistenza EF e migrazioni generate, feedback, profilo/obiettivi, revisioni di allenamento, prescrizioni/segmenti, risultati analitici e test d'isolamento a livello di database/API. Il selettore in memoria non è un confine di autorizzazione. La risoluzione della lineage richiede il dataset degli ingressi, che dovrà essere caricato dal repository autorizzato. Nessun endpoint pubblico o cambiamento dello schema in questo primo incremento.

I risultati scientifici richiederanno validazione su dati reali; le API wearable non garantiscono tutte le voci del catalogo.
