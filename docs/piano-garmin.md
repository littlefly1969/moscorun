# Integrazione Garmin e secondo atleta
## Obiettivo
Aggiungere Garmin come sorgente ufficiale, mantenendo identità, dati nativi e qualità. Il secondo atleta serve anche come prima prova di trasferimento tra persone e dispositivi, senza confondere questa prova con una validazione generale.

## Primo incremento
tools/garmin-personal fornisce accesso alla Personal API, paginazione, download originali, decodifica FIT verificata e bundle attribuiti all’owner. Restano gate della PR draft: secondo utente, persistenza, adattatore .NET, UI, scheduling e prova reale.

## Identità e connessione
Aggiungere creazione amministrativa di un utente mediante ASP.NET Identity; il bootstrap iniziale non va riutilizzato cambiando le variabili dell’amministratore. Ogni connessione deve associare l’owner MoscoRun al titolare Garmin verificato con la chiave autorizzata. I token di acquisizione derivano l’owner sul server.

Il percorso Personal riguarda il recupero dei propri dati. Per l’evoluzione in un servizio multiutente usare il programma Garmin appropriato. La registrazione del secondo atleta non autorizza automaticamente il primo a leggere i suoi dati: ricerca condivisa e ruolo di allenatore richiedono condivisione esplicita.

## Conservazione
Preservare originali e messaggi FIT con hash e attribuzione privata; separare GPS riservato e contesto AI. Conservare campi sconosciuti, lap, pause, unità originali, provenienza e disponibilità. Le conversioni vengono documentate e testate, compresa cadenza/cicli/passi.

## Sync
Prima prova manuale di una finestra limitata. Poi registro durevole con hash, revisioni, checkpoint dopo conferma del server, backoff, recupero dopo errore e riconciliazione. Cambi di dati e cancellazioni devono invalidare gli output analitici dipendenti. La presenza di un duplicato da Samsung/Health Connect richiede confronto della provenienza, non unione automatica.

## Verifiche prima del merge
- [ ] Due account, token distinti e test di isolamento su acquisizione, query, file originali, metriche e AI.
- [ ] Attività e file reali: formato FIT/ZIP, cardinalità multisport, GPS e tempi.
- [ ] Confronto delle unità e campioni con Garmin; frequenza reale e buchi dichiarati.
- [ ] Sonno, HRV, peso, attività quotidiana, stime e nutrizione solo se disponibili e autorizzati.
- [ ] Scadenza/revoca, rate limit, pagine ripetute e ripresa senza perdita o duplicazione.
- [ ] UI e contratto backend aggiornati, migrazione e rollback verificati.
