# Step 03 — Signal Processing, incremento 1

Primitive .NET per audit di un canale, duplicati, conflitti, copertura temporale, media pesata, allineamento causale e mediana robusta. Usare campioni già autorizzati, della stessa seduta e metrica; per replay storico applicare prima il filtro di disponibilità dello Step 01. Non estendere le finestre oltre il termine della seduta. I conflitti interrompono il supporto degli intervalli adiacenti (scelta conservativa rispetto al prototipo Node). La copertura non equivale alla qualità fisiologica.

Restano resampling a griglia, segmentazione, filtri avanzati, regressione robusta, GPS e stima di incertezza. Nessuna modifica DB/API o dipendenza.
