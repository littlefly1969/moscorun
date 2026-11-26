# C3 — Persistenza wearable

Adattatore server registrato in DI per campioni normalizzati Garmin, Samsung Health e Health Connect. Import singolo o paginato con checkpoint atomico C2. Revisioni, unità canoniche, dato nativo, mapping versionato, workout e dispositivo sono conservati; gli ID contestuali sono validati per atleta.

Il connettore deve ricavare l'atleta da autenticazione, qualificare gli ID per account provider e normalizzare tramite i contratti #39. I test verificano isolamento multi-sorgente, replay, round-trip del dato nativo, associazione a workout esistente e rifiuto di contesti altrui. I connettori, token e API esterne sono responsabilità del flusso acquisizione, non di questa PR.
