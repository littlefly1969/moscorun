# Completamento scientifico — segnali e prestazione

Estende Step 03 e 04 senza alterare il contratto di osservazione usato dalla persistenza. Griglia causale ZOH, limite di età e conflitti/buchi non imputati; finestre a passo regolare con supporto completo; regressione robusta Theil–Sen; fit lavoro-tempo CP/W' su sforzi attestati e fit CS robusto con RMSE; bilancio D'/W' a parametri espliciti e arresto alla deplezione; stima VO2max da protocollo Cooper attestato; interpolazione a concentrazione di lattato definita da protocollo misurato.

Le finestre resampled non equivalgono a campioni indipendenti; confrontare copertura e segnale nativo. I limiti della CS sono euristici. Il modello di riserva è condizionale e non replica automaticamente una variante Skiba calibrata: le costanti di recupero devono provenire da calibrazione individuale. L'arresto avviene al termine del segmento simulato, non stima il tempo esatto di esaurimento. Le stime Cooper possono avere bias di popolazione e non sono VO2max misurato. Una concentrazione di lattato arbitraria non identifica universalmente MLSS, VT o una soglia individuale.

Fonti: revisione CS/D' https://pubmed.ncbi.nlm.nih.gov/41931241/ ; W' https://pubmed.ncbi.nlm.nih.gov/22382171/ e applicazione corsa https://pubmed.ncbi.nlm.nih.gov/31776696/ ; Cooper https://pmc.ncbi.nlm.nih.gov/articles/PMC4314605/ ; limitazioni https://pubmed.ncbi.nlm.nih.gov/42516264/ ; metodi lattato https://pmc.ncbi.nlm.nih.gov/articles/PMC2769631/ .

Test sintetici e nessuna modifica DB/API. Restano fitting delle costanti sui protocolli reali, verifica di qualità e prova su dati reali.
