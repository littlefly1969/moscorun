# Integrazione personale Sign in with ChatGPT e utilizzo del piano
Decisione del titolare: compatibilità verificata, procedere con il percorso ufficiale di utilizzo del piano. Non introdurre un fallback a consumi API a pagamento.

## Primo incremento
tools/chatgpt-plan implementa protocollo, validazione e client di inferenza. La UI e il collegamento al processo .NET sono gate aperti della PR draft. Le credenziali ChatGPT appartengono a una registrazione verificata e a un owner MoscoRun; il login MoscoRun resta il riferimento per autorizzare i dati sportivi.

## Percorso personale
Il flusso documentato usa un callback loopback: un companion sul computer del browser deve aprire il listener prima della finestra di autorizzazione. Per un host remoto personale seguire il percorso ufficiale self-hosted; un callback loopback del browser non raggiunge automaticamente il server di casa.

Persistenza del host ID prima del primo accesso, tentativi a durata limitata, registrazione/account selezionati separati fino alla validazione. Nessun segreto in URL applicativi, log, browser storage o Git. Il listener deve verificare percorso, stato e tentativo e chiudersi dopo completamento o timeout.

## Utilizzo del piano
Il catalogo modelli viene dal medesimo account autorizzato. Le richieste includono solo il contesto sportivo selezionato e vengono consumate fino all’evento terminale. Le funzionalità della route preview non equivalgono a tutte le funzionalità della piattaforma API. Limiti, errori, annullamento e stato dell’account sono visibili; nessun cambio silenzioso di account o modello.

## Verifiche prima del merge
- [ ] OAuth reale, autorizzazione negata, state/nonce errati, tentativo scaduto e callback duplicato.
- [ ] Cambio account e separazione utenti MoscoRun.
- [ ] Riavvio, cifratura, rotazione refresh, refresh concorrenti tra processi e disconnessione/revoca.
- [ ] Catalogo e inferenza sul piano del titolare; errore arrivato dopo l’inizio dello stream.
- [ ] Integrazione UI/backend, contratto OpenAPI dove pertinente e test dei percorsi utente.
- [ ] Documentazione di configurazione e prova sul server personale.

Fonti: https://developers.openai.com/siwc/token-sharing-open-source e pagine collegate, verificate il 17 novembre 2026.
