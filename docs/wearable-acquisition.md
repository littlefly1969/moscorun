# Step 02 — acquisizione estesa, incremento 1

Dipende da Step 01. Il mapping .NET usa i nomi e le unità del bundle Garmin esistente (tools/garmin-personal/client.mjs). Le conversioni sono esplicite e verificabili; cadenza FIT, temperatura FIT (non necessariamente cutanea) e campi sconosciuti restano nativi. Non si ricavano RR dai bpm. Il dato originale e l'hash del FIT devono restare nel bundle di acquisizione: questa classe non è un archivio. Il mapping non è ancora collegato alla persistenza o alle API.

La capability audit distingue mai osservato, valore mancante e valore presente, su snapshot causale per owner e fonte. Non dimostra che una sorgente supporti una metrica in generale.

## Acquisizioni B da completare

| Gruppo | Sorgente da verificare |
| --- | --- |
| RR/IBI e HRV | File FIT Garmin; dati HRV del produttore; Samsung; Health Connect; fascia compatibile |
| Dinamiche di corsa | FIT e dispositivi Garmin compatibili; Samsung dove effettivamente disponibile |
| VO2max e soglie | Stime provider separate dalle misure e con algoritmo/provenienza |
| Sonno e recupero | Health endpoints Garmin e permessi Samsung/Health Connect |
| Nutrizione e idratazione | Health endpoints e feedback manuale |
| Peso e composizione | Bilancia e dati provider, distinguendo stime e misure |
| Ambiente | Fonte meteo con risoluzione e timestamp |

Restano da implementare la persistenza EF, sincronizzazione reale per owner, adattatori di acquisizione e prova con due account. Non inventare canali assenti. Gate: restore, format, build e test, poi prova su dati reali autorizzati.
