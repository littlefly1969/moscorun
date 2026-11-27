# Acquisizione scientifica tramite SDK (nessuna esportazione)

Incremento Health Connect e Samsung Health Data SDK diretto. La build privata usa già AAR Samsung 1.1.0.
I due lettori sono indipendenti dal provider degli allenamenti, con consensi e checkpoint separati. Il
checkpoint locale include versione del lettore e set di permessi, così un nuovo consenso non salta lo storico.
Il limite storico iniziale rimane quello del motore ScienceSync (30 giorni o 365 con history).
Il server conserva fonte, revisione, unità e native field; non sommare record provenienti da sorgenti diverse.
La deduplicazione cross-source richiede un successivo matching verificabile, non uguaglianza numerica.

## Copertura del primo incremento

- Health Connect: composizione corporea (peso, grasso %, massa magra, ossea, acqua), BMR, altezza,
  battito durante la giornata, pressione, glicemia, temperatura corporea, passi, distanza, piani, calorie
  totali e attive, nutrizione e acqua, oltre alle sei classi di record preesistenti.
- Samsung Data SDK: composizione corporea (inclusi kg di muscolo scheletrico e volume acqua in L), battito,
  SpO2, temperatura cutanea, sonno e score, pressione, nutrienti, acqua, glicemia, temperatura corporea,
  piani e Energy Score. Campioni di serie storiche mappati come osservazioni puntuali, non come ECG.
- Catalogo condiviso: 120 metriche, unità esplicite e diagnostica per fonte. BMR non è dispendio giornaliero;
  percentuale muscolo non è kg; volume acqua in L non è massa in kg.
- SDK ufficiali: https://developer.samsung.com/health/data/guide/features/data-types.html
  e https://developer.android.com/health-and-fitness/health-connect/data-types

## Limiti espliciti, non funzionalità complete

Stress proprietario ed ECG storico non sono tipi leggibili nella Data SDK documentata.
Samsung Health Sensor SDK consente nuove misure sull'orologio (ECG, PPG, IBI, EDA, MF-BIA ecc.);
richiede AAR separato, app Wear OS, permessi e test fisici. Non implementato in questo incremento.
Non ancora coperti tutti gli aggregati Samsung (Steps/Activity Summary e obiettivi), tutti i metadati
nativi, tutte le classi Health Connect o tutti i nutrienti; i record sovrapposti fra canali rimangono
distinti, nessuna imputazione. Nessun export, accesso non ufficiale o simulazione di misure.
Public CI usa lo stub esplicito Samsung; solo la build privata con AAR reale verifica i tipi SDK.
