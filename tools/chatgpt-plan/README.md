# ChatGPT plan client — primo incremento

Libreria Node 22 senza dipendenze di esecuzione per collegare un account ChatGPT e inviare richieste con il piano autorizzato. Compatibilità personale confermata dal proprietario del progetto. Non usa chiavi API a pagamento come fallback.

## Funzioni presenti

- Registrazione dinamica e riautorizzazione, host stabile fornito dal chiamante, PKCE, state e nonce, scadenza e consumo singolo del tentativo.
- Validazione crittografica RS256 del token tramite discovery/JWKS ufficiali; issuer, audience, scadenza, nonce e identità.
- Associazione esplicita a owner MoscoRun e registrazione ChatGPT, cifratura autenticata delle credenziali.
- Refresh serializzato nello stesso processo con salvataggio atomico delegato al chiamante, catalogo modelli dell’account e stream Responses fino al completamento.
- Strumenti `function`/`custom` (e `web_search` dove l'account lo consente) passati al modello; le chiamate a funzione arrivano al chiamante come eventi `tool_call`. Gli strumenti ospitati non supportati dalla preview vengono rifiutati prima della richiesta.
- Annullamento tramite `AbortSignal` del chiamante, con errori distinti per annullamento, timeout e flusso interrotto.
- Nessuna chiamata automatica durante il caricamento del modulo e nessun invio di dati dell’atleta senza una chiamata esplicita.

## Esecuzione delle verifiche

```sh
cd tools/chatgpt-plan && npm ci && npm run lint && npm run format:check && npm test
```

Test con `node:test` e soglie di copertura (righe 90%, rami 80%, funzioni 90%), eseguiti in CI dal workflow `tools.yml`. Le verifiche usano chiavi RSA effimere e provider simulato: non consumano il piano.

## Ruolo nell'architettura

Prototipo di riferimento eseguibile: fissa protocollo OAuth, controlli di sicurezza e formato del flusso con test. L'implementazione di produzione andrà nel backend .NET, portando questi stessi casi di test; la libreria Node non viene distribuita nell'immagine. Poiché il callback deve arrivare su `127.0.0.1` del computer con il browser, l'accesso da un server remoto richiede un companion locale oppure il percorso ufficiale "self-hosted VMs".

## Collegamento all’applicazione da completare nella stessa linea di lavoro

- [ ] Listener loopback sul computer del browser prima di beginSignIn; pulsante Continue with ChatGPT e selezione registrazione.
- [ ] Persistenza del solo involucro cifrato; chiave custodita separatamente, host ID stabile, scritture atomiche e ACL adeguate.
- [ ] Adattatore .NET personale/self-hosted, sessione MoscoRun autenticata come origine di ownerId; non fidarsi di ownerId inviato dal browser.
- [ ] Lock e compare-and-swap della rotazione refresh fra processi; il client iniziale serializza solo nel processo corrente.
- [ ] Revoca remota della sessione e disconnessione locale.
- [ ] Test live con l’account del titolare, gestione limiti e prova di riavvio/cambio account.

La libreria non introduce un endpoint pubblico o una schermata di chat in main. Questa PR rimane draft finché i punti di integrazione e la prova live non sono completati. Non loggare URL di autorizzazione che contengono id_token_hint. Non salvare token in browser storage. Il chiamante gestisce storia e finestra di contesto; non viene usata memoria delle conversazioni ChatGPT.

## Riferimenti ufficiali verificati il 17 novembre 2026

- https://developers.openai.com/siwc/token-sharing-open-source/sign-in
- https://developers.openai.com/siwc/token-sharing-open-source/models-and-inference
- https://developers.openai.com/siwc/token-sharing-open-source/profiles-and-sessions
- https://developers.openai.com/siwc/token-sharing-open-source/preview-limitations
