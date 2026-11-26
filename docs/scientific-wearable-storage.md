# C3 — Ingresso wearable

Adattatore server per campioni già normalizzati, separazione sorgenti Garmin, Samsung Health e Health Connect, persistenza idempotente tramite C2. Il chiamante deve autenticare il dispositivo e risolvere l'atleta, normalizzare unità e provenienza tramite i contratti dell'agente acquisizione (#39). Le sorgenti sono etichette interne da concordare con i connettori.

Non implementa le API dei produttori, token, mapping nativo, collegamento workout/dispositivo o sincronizzazione automatica. Non inviare campioni ad alta frequenza tramite questo adattatore senza benchmark.
