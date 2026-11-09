# Installazione Linux proposta

Questo è un piano di installazione. Nessun comando è stato eseguito sul server dell’utente e non è disponibile in questa sessione un collegamento al server.

## Servizi

- Backend e web ASP.NET Core nello stesso container applicativo.
- Database dedicato e ruolo dedicato nell’istanza PostgreSQL già presente, previa verifica di capacità e versione. Non creare automaticamente un secondo cluster.
- Lettore Bluetooth come servizio systemd sull’host Linux. Un solo processo usa il dispositivo configurato. Accesso BlueZ/D-Bus limitato alle operazioni necessarie; utenza dedicata. Non richiedere un container privilegiato come configurazione ordinaria.
- Ingresso HTTPS tramite un virtual host del reverse proxy già in uso. Se Apache gestisce i siti del server, usare quello; il dettaglio va verificato.

Il backend viene esposto al proxy su una porta locale libera e assegnata dopo l’inventario. PostgreSQL rimane raggiungibile sulla sola rete necessaria all’applicazione. Evitare nuove esposizioni pubbliche del database o conflitti con i servizi esistenti.

Il servizio BLE usa un ambiente Python dedicato e dipendenze fissate. Se l’API è in container sullo stesso server, l’agente la raggiunge attraverso un endpoint locale configurato e autenticato. Il collegamento dal telefono usa HTTPS e autenticazione; la disponibilità da rete esterna o tramite VPN dipende dall’uso desiderato.

## Dati persistenti e aggiornamenti

Persistenza distinta dal rilascio del codice: dati PostgreSQL, coda dell’agente e backup. Segreti esterni al repository, file di configurazione con accesso limitato e credenziali separate per app e agente.

Le migrazioni del database sono versionate e applicate come passo esplicito di rilascio, una sola volta. Un backup verificato precede migrazioni che modificano dati; non supporre che tornare a una vecchia immagine annulli una migrazione.

Il processo di rilascio deve registrare versione applicativa, schema e compatibilità dell’agente. Riavvio automatico, controlli di salute, log con rotazione e controllo dello spazio su disco sono parte della prima installazione stabile. Verificare un ripristino del backup prima di considerarlo operativo.

## Inventario da acquisire prima di scrivere la configurazione di deploy

| Informazione | Perché serve |
| --- | --- |
| Distribuzione Linux e versione | Compatibilità runtime, BlueZ e gestione servizi |
| Architettura CPU, RAM e spazio | Immagini corrette e budget risorse |
| Bluetooth LE e posizione fisica | Ricezione effettiva della bilancia |
| Container e reti già presenti | Collegamento al database e assenza di conflitti |
| Versione PostgreSQL e gestione backup | Compatibilità e riuso dell’istanza |
| Proxy, domini e porte | Nuovo endpoint HTTPS sul server condiviso |

Non includere password, token o file di configurazione con segreti nell’inventario condiviso. I dati sopra possono essere forniti come riepilogo; non occorre inviare credenziali.
