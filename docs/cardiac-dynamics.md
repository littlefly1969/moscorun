# Step 08 — Cardiac Dynamics and HRV, incremento 1

F08: recupero FC = valore a fine sforzo meno valore a un tempo di recupero noto; il chiamante deve fissare protocollo, postura, durata e modalità di recupero. C11: media RR, RMSSD e SDNN campionaria su intervalli NN consecutivi, già verificati per artefatti e battiti ectopici. La primitiva non ricostruisce RR da frequenze cardiache medie, non unisce segmenti discontinui, e rifiuta intervalli invalidi. Tre intervalli sono il minimo computazionale, non una durata clinicamente valida.

F07/F12: risposta esponenziale del primo ordine verso una FC di equilibrio a parametri forniti. Non è un modello individuale calibrato. Restano stima dei parametri, input dipendenti da intensità e ambiente, confronto con persistenza, DFA alpha1 F11, validazione RR reale e controllo qualità. Nessuna diagnosi o prescrizione.

Politica RR v2: rifiuto integrale della finestra se un intervallo è fuori 300–2000 ms o se la variazione rispetto al precedente supera il 25%. ValidateRr espone la causa; soglie conservative euristiche per analisi a riposo, non diagnosi né correzione di artefatti. Non adatte automaticamente a ogni atleta o fase di sforzo.
