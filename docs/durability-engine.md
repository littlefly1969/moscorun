# Step 05 — Durability Engine, incremento 1

D01/D02: confronto della perdita relativa di efficienza tra due finestre. E = carico esterno medio / FC media; perdita percentuale = 100 * (1 - E tardiva / E iniziale). Il segno negativo indica miglioramento relativo. La scelta del denominatore e del segno è esplicita, non necessariamente equivalente alle metriche di altri software.

Entrambe le medie devono essere calcolate sul medesimo supporto temporale accoppiato, nella stessa seduta, con lo stesso canale esterno (velocità in m/s o potenza in W). Copertura minima configurabile e nessuna imputazione delle finestre mancanti. Il motore rifiuta finestre sovrapposte, supporto impossibile e valori non fisici. Prima del confronto escludere pause, riscaldamento e condizioni non confrontabili; grade, temperatura, intensità e rifornimento sono confondenti.

Restano da sviluppare costruzione automatica delle finestre accoppiate a partire dalle serie, D03 onset e D04 pendenza del deterioramento, test di ripetibilità e normalizzazione ambientale. L'output è descrittivo, non diagnosi né misura diretta della fatica. Nessuna modifica DB/API.
