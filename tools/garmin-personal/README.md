# Garmin Personal API — connettore iniziale

Client personale Node 22 per API ufficiale Garmin, senza scraping e senza login con password. La chiave del titolare viene passata solo al costruttore e non viene salvata o stampata dal modulo.

## Presente
- Chiave personale → access token, cache e rinnovo dopo scadenza/401.
- Finestra di attività paginata, deduplicazione e errore esplicito se la lettura è incompleta.
- Dettaglio, download del file originale e lettura iniziale di categorie giornaliere; readHealth copre esplicitamente i 19 endpoint Health del catalogo pubblico verificato (sonno, HRV, peso, idratazione, passi, stress, nutrizione, Body Battery e altri payload disponibili). I payload rimangono nativi finché non esiste una conversione verificata.
- SDK FIT Garmin fissato a 21.221.0 con lock file; controllo integrità prima della decodifica.
- Bundle attribuito a un owner MoscoRun, hash del file originale, tempi nativi, campi, eventi, lap e developer fields conservati.
- Serie normalizzate iniziali e campi nativi con unità proprie: nessuna promessa di risoluzione a un secondo o disponibilità di ogni metrica.

## Verifica
`cd tools/garmin-personal && npm ci && npm test`
Provider HTTP simulato, dati sintetici e file FIT minimo ufficiale. Nessuna credenziale o corsa personale nelle fixture.

## Integrazione applicativa e accettazione
- [ ] Creazione amministrativa del secondo utente con Identity e test di accesso incrociato.
- [ ] Associazione verificata Garmin → owner MoscoRun; le chiavi non vanno condivise fra atleti.
- [ ] Archivio cifrato delle credenziali, chiave separata e scadenza/revoca visibili.
- [ ] Registro persistente e checkpoint aggiornati solo dopo commit dell’attività; retry e backoff fra esecuzioni, riconciliazione e cancellazioni.
- [ ] Download originali: verificare risposta FIT/ZIP reale; gli ZIP vengono rifiutati finché non esiste estrazione con limiti e controllo percorsi.
- [ ] Mapping verso l’ingestione .NET, revisioni stabili dopo cambio contenuto e deduplicazione fra provider.
- [ ] Archiviazione privata dei campi non ancora supportati dal server e collegamento al catalogo metriche della PR AI.
- [ ] Verifica live di attività, sonno, HRV e altre categorie disponibili sull’account scelto; confronto campioni e unità con Garmin.
- [ ] UI di collegamento, stato e disconnessione; sincronizzazione automatica solo dopo prova manuale.

Questa è una libreria eseguibile e testata, non ancora un servizio schedulato o una schermata nel cruscotto. PR draft fino all’integrazione e al test reale. Le API personali riguardano i dati propri; per un servizio multiutente va adottato il canale Business appropriato.

## Fonti ufficiali (17 novembre 2026)
- https://developer.garmin.com/gc-developer-program/personal/personal-api/
- https://developer.garmin.com/gc-developer-program/personal/personal-faq
- https://developer.garmin.com/gc-developer-program/personal/swagger-docs
- https://github.com/garmin/fit-javascript-sdk
