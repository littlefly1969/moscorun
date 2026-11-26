# C8 — Backup scientifico

Estende lo scenario operativo CI: crea due osservazioni sintetiche con relazione di derivazione, esegue backup, simula perdita, ripristina e confronta il contenuto di tutte le righe scientifiche. Il backup esistente usa pg_dump in formato custom.

Eseguire soltanto sullo stack di test isolato. Per produzione verificare RPO/RTO, copia off-site cifrata, accessi e rotazione, prova periodica su ambiente isolato e procedura per cancellazione dai backup. Nessun backup di produzione eseguito da questa PR.
