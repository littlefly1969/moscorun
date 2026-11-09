# Base di prodotto

Decisione proposta il 9 novembre 2026. Questo documento descrive il progetto, non funzionalità già implementate.

## Scelte

| Area | Proposta | Motivo |
| --- | --- | --- |
| Backend | ASP.NET Core, .NET 10 LTS | Coerenza con l’esperienza e le applicazioni .NET già presenti |
| Organizzazione | Un backend organizzato in moduli | Sviluppo e manutenzione sostenibili su un server piccolo |
| Accesso dati | EF Core e provider PostgreSQL compatibile | Modello e migrazioni versionati |
| Database | PostgreSQL già disponibile | Un solo archivio centrale; database e ruolo dedicati all’app |
| Web | Razor Pages responsive | Interfaccia iniziale semplice da distribuire nello stesso backend |
| Bilancia | Servizio Python sul server Linux | Adattatore BLE indipendente dal modello di dominio |
| Telefono | App Android Kotlin e Health Connect | Ingresso documentato dei dati Samsung Health |

MariaDB è una scelta valida, ma non c’è una necessità emersa che giustifichi il mantenimento di due database applicativi. La versione PostgreSQL, le risorse e i backup dell’istanza esistente vanno verificati prima di adottarla.

## Moduli del backend

- Identità e installazioni: utenti, profili atleta, installazioni, dispositivi e autorizzazioni.
- Acquisizione: connettori, checkpoint, validazione, aggiornamenti, cancellazioni e deduplicazione.
- Misure: pesate e campioni con unità, provenienza e qualità.
- Allenamenti: attività registrate, segmenti e associazione dei campioni.
- Pianificazione: sessioni previste e confronto con quelle svolte.
- Analisi: riepiloghi derivati e ricostruibili dai dati importati.

Una sola applicazione, un database, contratti espliciti fra i moduli. Il lettore Bluetooth e il ponte Android comunicano con le API e non scrivono direttamente nel database.

## Installazione locale e futuro prodotto

La prima modalità è un’installazione personale ospitata dall’utente. Ogni agente Bluetooth possiede un’identità e appartiene a un’installazione; ciascun dispositivo è registrato in quell’installazione. L’identità dell’atleta è distinta da quella del dispositivo: una bilancia può essere condivisa.

Gli oggetti di dominio includono sin dall’inizio il proprietario o ambito di appartenenza. Il backend ricava l’ambito autorizzato dalle credenziali e controlla l’accesso: un identificativo inviato dal client non autorizza da solo l’accesso a un altro utente. L’isolamento fra utenti richiederà test specifici prima di una distribuzione multiutente.

L’eventuale versione ospitata centralmente potrà ricevere dati da agenti locali associati, ma non viene implementato adesso un sistema SaaS completo. Roma–Ostia e Berlino sono obiettivi configurabili, non nomi di tabelle o regole speciali.

## Affidabilità da costruire nel primo incremento

- Salvataggio locale persistente degli eventi del lettore quando l’API non risponde, con limiti di spazio e gestione esplicita degli errori.
- Conferma server soltanto dopo il commit; riinvio idempotente tramite ID evento stabile.
- Deduplicazione radio prima della creazione dell’evento; la stessa pesata ritrasmessa non diventa un nuovo evento.
- Provenienza e identificativo originale conservati per i dati Health Connect.
- Aggiornamenti e cancellazioni propagati; interruzioni e checkpoint scaduti gestiti con risincronizzazione.
- Campi mancanti espliciti e copertura visibile. Conteggi e riepiloghi non devono presentare come completa una sincronizzazione parziale.
- Stato separato per ultima ricezione BLE, ultima sincronizzazione telefono e ultimo salvataggio riuscito.

## Gestione dei dati del prodotto

L’interfaccia web richiede autenticazione con componenti standard del framework. Gli agenti hanno credenziali distinte, revocabili e limitate all’acquisizione. Il meccanismo di autenticazione dell’app mobile va definito prima di esporre le API; nessuna chiave permanente condivisa da tutti gli utenti nel binario Android.

Esportazione e cancellazione sono capacità previste del prodotto. Richiedere al telefono solo i dati usati dalle funzioni effettivamente offerte. Per la distribuzione Google Play preparare privacy policy e dichiarazioni Health Apps/Data Safety e giustificazioni dei tipi Health Connect richiesti. L’eventuale uso diretto del Samsung Health Data SDK richiederebbe il percorso partner Samsung: non viene assunto come canale disponibile per il rilascio.

## Primo incremento

1. Rilevare il server e identificare la bilancia.
2. Provare una pesata stabile e una corsa reale attraverso Health Connect.
3. Implementare backend, schema iniziale e ingresso di una pesata autenticata e idempotente.
4. Collegare l’agente Linux con riavvio e coda persistente; verificare rete interrotta e riinvio.
5. Implementare il ponte Android e verificare importazione, aggiornamenti e assenza di duplicati.
6. Mostrare peso, volume settimanale e confronto programmato/svolto nel primo cruscotto.

L’invio di allenamenti strutturati al Galaxy Watch resta una verifica separata. Il supporto Health Connect ai piani non garantisce che Samsung Health e quel modello di Watch li eseguano.
