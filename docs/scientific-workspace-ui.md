# Scientific Workspace

Prima implementazione del concept 04 sul server. L'Atlante è la pagina iniziale autenticata; il precedente
cruscotto Forma resta disponibile in /forma. Nessuna modifica a schema, motori, acquisizione o autorizzazione.

## Ambienti

- Atlante: snapshot del catalogo scientifico corrente (100 voci), filtro per famiglia, ricerca, preferiti
  locali alla vista, dettaglio, interrogazione esplicita delle 14 metriche del motore daily e dei valori
  del diario giornaliero, mantenendo separate le fonti.
- Cronoscopio: archivio sedute paginato, serie reali dal contratto workout/series, scala temporale comune,
  zoom di finestra e campione più vicino entro 5 s, senza interpolazione o imputazione.
- Laboratorio: accesso ai pannelli di protocollo esistenti, incluso Research Lab, carico, RR, sonno,
  feedback/nutrizione e analisi della seduta.
- Simulatore: pannelli esistenti per pacing, previsione gara, adattamento e Digital Twin.
- Evidenze: associazioni sonno/prestazione calcolate dal server; nessuna linea per risultati nulli,
  insufficienti o invalidi; nessuna interpretazione causale.

## Limiti e fasi successive

Il catalogo non prova che tutte le metriche siano state acquisite. Le voci senza lettura diretta restano
esplicitamente non collegate; non si inventano valori. Le serie della seduta espongono solo i canali
supportati dal contratto corrente, non l'intero archivio dei chunk. La mappa presenta il solo metodo di
associazione già disponibile. Non sono ancora presenti un registro persistente degli esperimenti,
favoriti sincronizzati, composizione arbitraria dei motori, catalogo derivato completo o navigazione
longitudinale di ogni segnale grezzo. L'app Android resta fuori perimetro.

Il test di parità verifica l'allineamento del catalogo frontend a tools/athlete-analysis/catalog.json
finché non sarà disponibile un endpoint catalogo versionato. Non introdurre fallback sintetici in produzione.
