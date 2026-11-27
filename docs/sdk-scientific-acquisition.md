# Acquisizione scientifica tramite SDK (nessuna esportazione)

Incremento Health Connect e Samsung Health Data SDK diretto. La build privata usa già AAR Samsung 1.1.0.
I due lettori sono indipendenti dal provider degli allenamenti, con consensi e checkpoint separati. Il
checkpoint locale include versione, permesso storico e singola famiglia. Il recupero parte dalle misure recenti e procede a ritroso a turni; i fallimenti di lettura non fanno avanzare il relativo cursore. Una quota del tempo di sincronizzazione è riservata ai dati scientifici anche quando gli allenamenti non sono allineati. Le finestre lette e gli errori sono riportati per metrica (i vecchi rapporti restano compatibili).
Lo storico segue la scelta dell’utente (fino al limite tecnico di 20 anni), con 30 giorni se Health Connect non concede il permesso storico.
Il server conserva fonte, revisione, unità e native field; non sommare record provenienti da sorgenti diverse.
La deduplicazione cross-source richiede un successivo matching verificabile, non uguaglianza numerica.

## Copertura del primo incremento

- Health Connect: composizione corporea (peso, grasso %, massa magra, ossea, acqua), BMR, altezza,
  battito durante la giornata, pressione, glicemia, temperatura corporea, passi, distanza, piani, calorie
  totali e attive, nutrizione e acqua, oltre alle sei classi di record preesistenti.
- Samsung Data SDK: composizione corporea (inclusi kg di muscolo scheletrico e volume acqua in L), battito,
  SpO2, temperatura cutanea, sonno e score, pressione, nutrienti, acqua, glicemia, temperatura corporea,
  piani e Energy Score. Campioni di serie storiche mappati come osservazioni puntuali, non come ECG.
- Catalogo condiviso: 122 metriche, unità esplicite e diagnostica per fonte. BMR non è dispendio giornaliero;
  percentuale muscolo non è kg; volume acqua in L non è massa in kg.
- SDK ufficiali: https://developer.samsung.com/health/data/guide/features/data-types.html
  e https://developer.android.com/health-and-fitness/health-connect/data-types

## Limiti espliciti, non funzionalità complete

Stress proprietario ed ECG storico non sono tipi leggibili nella Data SDK documentata.
Samsung Health Sensor SDK consente nuove misure sull'orologio (ECG, PPG, IBI, EDA, MF-BIA ecc.);
richiede AAR separato, app Wear OS, permessi e test fisici. Non implementato in questo incremento.
Sono ora inclusi Steps.TOTAL e i quattro aggregati Activity Summary (giorni conclusi), ottenuti dal fornitore senza sommare dispositivi sovrapposti. L'SDK non espone un ordine di revisione per questi aggregati: valore cambiato conservato come revisione distinta e non ordinata. Le fasi del sonno Health Connect conservano codice nativo e intervallo tramite identità. Non ancora coperti gli obiettivi Samsung, tutti i metadati
nativi, tutte le classi Health Connect o tutti i nutrienti; i record sovrapposti fra canali rimangono
distinti, nessuna imputazione. Nessun export, accesso non ufficiale o simulazione di misure.
Public CI usa lo stub esplicito Samsung; solo la build privata con AAR reale verifica i tipi SDK.

Samsung Health Monitor: l'SDK 1.1.0 espone le notifiche di ritmo irregolare e il risultato della segnalazione di rischio apnea se è stato consentito il trasferimento in Samsung Health. Sono risultati del fornitore, non diagnosi, non ECG. Il valore 1 indica DETECTED; 0 solo NOT_DETECTED esplicitamente presente per apnea. UNDEFINED non è trasformato in zero. Fonte: https://developer.samsung.com/health/data/release-note.html
