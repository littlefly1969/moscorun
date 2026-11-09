# MoscoRun

Prima installazione personale di una piattaforma riutilizzabile per raccogliere peso e allenamenti e confrontare il lavoro svolto con quello programmato. La prima applicazione è il percorso Roma–Ostia e Berlino; le entità del prodotto restano indipendenti dalle gare.

**Stato:** progettazione iniziale, 9 novembre 2026. Repository privato: [littlefly1969/moscorun](https://github.com/littlefly1969/moscorun). Sono presenti architettura, scelte tecnologiche e piano di sviluppo; le integrazioni devono ancora essere implementate e provate sui dispositivi reali.

## Collegamenti previsti

- Bilancia Xiaomi → Bluetooth del server vicino alla bilancia → servizio di acquisizione → archivio locale.
- Galaxy Watch → Samsung Health sul telefono → Health Connect → app Android del progetto → API autenticata sul server → stesso archivio.
- Archivio → interfaccia per peso, allenamenti svolti e calendario delle sessioni pianificate.

Il server legge direttamente la bilancia. Home Assistant non è una dipendenza e non fa parte del percorso dati.

## Primo risultato verificabile

Acquisire una pesata definitiva direttamente dalla bilancia sul server e importare una corsa reale da Samsung Health, controllando distanza, durata e disponibilità dei campioni cardiaci. Ripetere l'importazione senza duplicati.

## Scelte proposte, da validare

Backend ASP.NET Core su .NET 10 LTS, Entity Framework Core con provider PostgreSQL compatibile, PostgreSQL come archivio centrale, Python per il solo servizio Bluetooth e Kotlin per il ponte Android. Interfaccia web iniziale Razor Pages responsive. Confermare compatibilità con distribuzione Linux, architettura CPU e versione PostgreSQL già installata.

Health Connect è un'API sul telefono Android: per questa architettura serve un'app Android che legga i dati e li invii al server. La lettura storica e in background richiede permessi e funzionalità disponibili sul dispositivo. Il recupero dell'intero storico Samsung va verificato separatamente.

La lettura degli allenamenti svolti e l'invio di allenamenti strutturati all'orologio sono due capacità diverse: il secondo percorso richiede una prova specifica e non è garantito da questo progetto iniziale.

## Documenti

- [Architettura](docs/architettura.md)
- [Verifiche iniziali e fonti](docs/verifiche.md)
- [Base di prodotto](docs/prodotto.md)
- [Installazione Linux proposta](docs/deploy-linux.md)

## Dati personali

Il repository conterrà codice e documentazione. Pesate, esportazioni Samsung, database, tracciati e credenziali rimangono fuori da Git. Questa bozza non contiene dati sanitari reali.
