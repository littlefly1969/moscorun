# Ingestione dei dati scientifici

Stato: incremento 1 (questa specifica). Chiude il rilievo P1 dell'audit dei 56 moduli: lo strato di persistenza
(C2 `ScientificImportStore`/`ScientificSyncStore`, C3 `WearableImport`, `ScientificSignalStore`) esisteva ma nessun
endpoint lo chiamava, e l'app Android inviava solo gli allenamenti. Di conseguenza RR/HRV, sonno del dispositivo,
meteo e dinamiche di corsa non ricevevano mai dati.

## Principi

- **Nessun dato inventato.** Si inviano solo grandezze che la sorgente espone davvero. Non si ricavano RR dai bpm,
  né efficienza del sonno o stadi dove la sorgente non li fornisce.
- **Identità e revisioni di C2.** L'identità di un campione è `(source, externalId, revision, metric)`; un reinvio
  identico non scrive nulla, un contenuto diverso con la stessa revisione è un conflitto (409), una modifica della
  sorgente porta una nuova revisione e lo storico resta interrogabile con cutoff `asOf`.
- **Proprietario e dispositivo dalle credenziali.** L'atleta e il dispositivo vengono dal token, mai dal corpo.
  Il dispositivo che inoltra è registrato nella provenienza (`ImportDeviceId`, `DeviceId` dei chunk): i lettori
  biomeccanici lo richiedono.
- **Privilegio minimo.** Solo token di tipo *Telefono Android* (`PhoneBridge`). Gli agenti bilancia restano limitati
  alle pesate (403).
- **Allenamenti per riferimento naturale.** Il client non conosce gli ID del server: indica `workout: {source, sourceId}`,
  il server lo risolve per l'atleta autenticato. Un riferimento sconosciuto è un errore di validazione: il client
  invia prima l'allenamento, poi i dati collegati.
- **Unità canoniche.** Metrica nota al catalogo `athlete-analysis v1` e unità canonica obbligatorie; il dato nativo
  (metrica, unità, valore, versione del mapping) viaggia in `native` e resta interrogabile.
- **Compatibilità.** Endpoint nuovi, nessuna modifica di schema: nessuna migrazione expand/contract necessaria.
  Un server precedente risponde 404 e l'app sospende questa parte senza toccare la sincronizzazione degli allenamenti.

## API (v1)

Tutti gli endpoint: `Authorization: Bearer <token dispositivo PhoneBridge>`, corpo JSON (gzip ammesso),
`Cache-Control: no-store` in risposta.

### `POST /api/v1/ingest/observations`

Osservazioni puntuali (giornaliere o legate a un allenamento), al più 500 per richiesta, atomiche.

```json
{
  "source": "health-connect",
  "page": { "stream": "daily", "previousCursor": null, "nextCursor": "2026-11-25" },
  "samples": [
    {
      "externalId": "hc:0b3c…", "revision": "1764115200000", "revisionOrder": 1764115200000,
      "metric": "hrvRmssd", "unit": "ms", "value": 42.5, "time": "2026-11-25T05:12:00Z",
      "method": "ProviderReported", "providerAlgorithm": "com.sec.android.app.shealth",
      "workout": null,
      "native": { "metric": "HeartRateVariabilityRmssdRecord", "unit": "ms", "value": 42.5, "mappingVersion": "hc-science-v1" }
    }
  ]
}
```

- `source`: `samsung-health`, `health-connect` o `garmin-connect`.
- `method`: `Measured`, `ProviderReported` o `ProviderEstimated` (quest'ultimo richiede `providerAlgorithm`).
  `SelfReported` resta riservato al diario dell'atleta (`PUT /science/feedback`).
- `page` facoltativo: se presente, import e avanzamento del checkpoint `(source, stream)` avvengono nella stessa
  transazione; un `previousCursor` diverso da quello salvato è un conflitto (409, rileggere il cursore). Un replay
  dello stesso `nextCursor` senza campioni nuovi è idempotente.
- Risposta 200: `{ "received": n, "imported": k, "cursor": "…" }` (`imported` = revisioni nuove).
- 400 dati non validi (campo e messaggio), 409 revisione o cursore in conflitto, 403 token non di telefono.

### `GET /api/v1/ingest/observations/cursor?source=…&stream=…`

Restituisce `{ "source", "stream", "cursor" }` (`cursor` null se mai scritto). Serve ai connettori che tengono il
proprio stato sul server.

### `POST /api/v1/ingest/signals`

Un chunk di segnale ad alta frequenza, lossless (codec v1, al più 16.384 campioni ordinati, valori finiti o null).

```json
{
  "source": "garmin-connect", "externalId": "fit:123:rr:0", "revision": "1", "revisionOrder": 1,
  "metric": "rrInterval", "unit": "ms", "method": "Measured", "providerAlgorithm": null,
  "workout": { "source": "garmin-connect", "sourceId": "123" },
  "samples": [ { "time": "2026-11-25T06:00:00.812Z", "value": 812 } ]
}
```

- Metriche ammesse: le serie temporali del catalogo (cardiache, RR, SpO2, respirazione, temperatura cutanea,
  velocità, distanza, cadenza, potenza, altitudine, pendenza, dinamiche di corsa, accelerazioni e velocità angolari,
  ECG/PPG/EDA, SmO2 e meteo: temperatura dell'aria, umidità, vento, pressione).
- 201 al primo arrivo, 200 su replay identico, 409 se la stessa revisione arriva con contenuto diverso.

## App Android (incremento 1)

La lettura scientifica usa **Health Connect qualunque sia la sorgente degli allenamenti**: Samsung Health vi
scrive sonno, frequenza a riposo, SpO2 e (dove disponibile) HRV, e lo stesso vale per Garmin Connect e altre app.
`providerAlgorithm` conserva l'app d'origine (`dataOrigin`), così le provenienze restano distinguibili.

| Record Health Connect | Metrica | Unità | Metodo |
| --- | --- | --- | --- |
| `RestingHeartRateRecord` | `restingHeartRate` | bpm | ProviderReported |
| `HeartRateVariabilityRmssdRecord` | `hrvRmssd` | ms | ProviderReported |
| `SleepSessionRecord` (fine sessione) | `sleepInBedDuration` | s | ProviderReported |
| `SleepSessionRecord` con stadi | `sleepDuration` (somma degli stadi di sonno) | s | ProviderReported |
| `OxygenSaturationRecord` | `spo2` | % | Measured |
| `RespiratoryRateRecord` | `respirationRate` | 1/min | ProviderReported |
| `Vo2MaxRecord` | `vo2max` | mL/kg/min | ProviderEstimated (metodo di misura HC) |

- Identità: `externalId = hc:<metadata.id>`, `revision`/`revisionOrder` = `lastModifiedTime` in ms; ogni modifica
  in Health Connect diventa una nuova revisione.
- Finestra: al primo avvio gli ultimi 30 giorni (o lo storico scelto se il permesso ai dati precedenti è concesso),
  poi dal giorno successivo al checkpoint locale riletto con 7 giorni di sovrapposizione per le correzioni tardive.
  Il checkpoint avanza solo dopo la conferma del server.
- Permessi facoltativi: mancano? Quella metrica non viene letta e le Impostazioni lo spiegano. Nessun permesso
  scientifico blocca la sincronizzazione degli allenamenti.
- Errori: 400/409 registrati in Diagnostica senza ritentare lo stesso lotto; 401/403 fermano tutto come per gli
  allenamenti; 404 (server precedente) sospende la parte scientifica fino al prossimo avvio; 429/5xx/rete: backoff.

## Fuori da questo incremento (prossimi passi)

| Dato | Sorgente prevista | Dove |
| --- | --- | --- |
| RR/IBI, dinamiche di corsa (GCT, oscillazione, bilanciamento) | File FIT Garmin (`tools/garmin-personal`) → `POST /ingest/signals` | connettore Garmin |
| Velocità, cadenza, potenza, FC per D07–D09 | Proiezione lato server delle serie già presenti negli allenamenti in chunk con `workoutId` e `deviceId` (anche retroattiva) | backend |
| Meteo dell'allenamento | Servizio meteo storico lato server sul percorso GPS, con fonte e risoluzione dichiarate | backend |
| Dati Samsung non presenti in Health Connect | Samsung Health Data SDK (richiede l'AAR privato per compilare) | build privata |
