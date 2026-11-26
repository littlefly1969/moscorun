# Sicurezza

MoscoRun tratta dati sanitari e tracciati GPS: le vulnerabilità vanno segnalate **in privato**, mai con issue, PR o discussioni pubbliche.

## Come segnalare

Usare *Security → Report a vulnerability* di questo repository (segnalazione privata di GitHub). Indicare:

- componente coinvolto (API, cruscotto web, app Android, script di deploy, agente) e versione o SHA;
- passi per riprodurre e impatto atteso;
- eventuale proposta di correzione.

Non includere dati sanitari reali, token, password o file `.env`: bastano dati sintetici.

*English: please report vulnerabilities privately through "Security → Report a vulnerability" on this repository, never through public issues.*

## Versioni supportate

Si corregge solo l'ultima release pubblicata (rilascio continuo da `main`, vedi [docs/github.md](docs/github.md)). Le installazioni si aggiornano con `./moscorun.sh update`.

## Ambito

- Nell'ambito: codice di questo repository e immagine `ghcr.io/littlefly1969/moscorun`.
- Fuori ambito: installazioni self-hosted configurate diversamente da [docs/deploy-linux.md](docs/deploy-linux.md), componenti di terze parti (vanno segnalati ai rispettivi progetti), SDK Samsung e build dell'APK ufficiale, che non sono in questo repository.
