# Estensione Step 08–10 — Cardiac, recovery, environment

Calibrazione della costante di tempo della FC su griglia esplicita e protocollo noto, con equilibrio e stato iniziale forniti; RMSE descrittivo del fitting, non errore fuori campione. DFA-alpha1 su profilo integrato, detrending lineare e finestre forward/backward di 4–16 intervalli; minimo computazionale 128 NN consecutivi già sottoposti a screening. Nessuna ricostruzione da bpm, nessuna correzione implicita, nessuna soglia clinica o fisiologica automatica. Questa variante non replica l'intera pipeline Kubios (es. smoothness priors); test sintetici non dimostrano accordo fra pipeline. Fonte: https://doi.org/10.1080/02640414.2024.2421691 .

Regolarità del sonno tramite media e dispersione circolare dell'orario locale, con fuso e notti distinte stabiliti a monte. Vento contrario con direzione meteorologica, distanza Haversine e gestione antimeridiano; non sono una correzione energetica o termica individuale. I modelli di acclimatazione richiedono serie longitudinali, endpoint e controllo dei confondenti.

Nessuna modifica di database/API. Restano taratura personale, validazione contro riferimenti e acquisizione meteorologica/wearable reale.
