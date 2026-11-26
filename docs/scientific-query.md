# C5 — Query e segnali longitudinali

Query scalari con keyset (tempo, disponibilità, ID), cutoff fisso e indici PostgreSQL per atleta con o senza metrica. Massimo 500 righe per pagina; non sostituisce il controllo lineage di ScientificSnapshot.

Per PPG/ECG/accelerometria e altre serie: chunk immutabili di massimo 16.384 campioni, codec Brotli lossless (timestamp UTC a 100 ns, bit IEEE754, null), SHA-256 dell'encoding canonico, metodo e fonte, revisioni, isolamento, associazioni autorizzate. Import idempotente e lettura paginata di chunk sovrapposti alla finestra, non di campioni già ritagliati. I connettori devono usare ID chunk e revisioni stabili; ogni chunk ha commit atomico, il checkpoint di una serie si avanza solo dopo aver salvato tutti i chunk. Le vecchie serie workout restano inalterate.

Test su PostgreSQL reale di keyset, cutoff, round-trip a precisione piena, replay, isolamento e corruzione codec. Benchmark di throughput sul volume di produzione da eseguire con dati rappresentativi; non è una misura di performance clinica.
