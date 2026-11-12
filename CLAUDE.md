# Regole di lavoro per Circuit Trainer

Progetto: **Circuit Trainer** (ATCRoom Circuit Trainer), simulatore di circuito per la torre, istruttore / studente (C# .NET 9 WPF),
repository **ATCRoom/Circuit-Trainer**. Le cose da fare, le idee e le decisioni sono in `TODO.md`.
Progetto fratello: **ATCRoom/AuroraPAR-v2** (AuroraPAR v2 e AuroraPAR v2 Trainer), da cui viene il codice di partenza.

## Comunicazione
- Rispondi sempre in italiano.
- L'utente prova le build su Windows e le passa ad alcuni amici: ogni build deve essere utilizzabile da chi non è sviluppatore.

## Quando scrivere codice
- Prima proponi; scrivi codice solo dopo un via esplicito ("vai", "parti", "procedi", "ok").
- Chiedi SEMPRE prima di scrivere codice o di fare una build, anche per correggere un bug. Proponi la correzione e aspetta il via.
- Raggruppa più modifiche in una sola build, per risparmiare utilizzo. Ogni push fa partire una build: niente push di modifiche sparse (documentazione compresa), si fa il commit in locale e si manda tutto insieme al prossimo gruppo.
- Tieni le cose semplici: niente realismo o dettagli non chiesti. Proponi la soluzione più semplice che funziona.
- Le idee vanno in `TODO.md`.
- "Non fare niente" o "aspetta" vuol dire non fare niente.

## Build
- Niente SDK dotnet in locale: si compila solo tramite GitHub Actions (`.github/workflows/build.yml`).
- Gli errori di compilazione si leggono dalle annotations dei check-run.

## Revisioni
- Per lavori grandi o rischiosi, fai fare una revisione da un agente indipendente in sola lettura.

## Immagini pubbliche
- Niente nominativi reali e niente percorsi con il nome utente di Windows dell'utente.

## Mappe e dati di terzi
- I file di esempio dell'utente (IVAO, EuroScope, AIP, KML...) non vanno nel repository: si annota solo il formato in `TODO.md`.
- Le mappe fatte con il Map Creator sono per uso personale e solo per simulazione: mai distribuite.

## Codice condiviso con AuroraPAR-v2
- La casa di **ATCRoom Map Creator** e della libreria delle mappe (`MapCreator/`) è QUESTO repository.
- Prima di ogni nuova release di AuroraPAR v2 Trainer (in ATCRoom/AuroraPAR-v2), ricopiare `MapCreator/` da qui in AuroraPAR-v2.
- ATCRoom Map Creator si scarica a parte (deciso il 9 novembre): le sue release sono in **ATCRoom/MapCreator**. Prima di ogni release del Map Creator ricopiare lì `MapCreator/` e `docs/MAP-FORMAT.md` e alzare `<Version>`.
- Il motore (`Sim/Engine`) qui è una copia che cambia per la torre: una correzione trovata in uno dei due si riporta a mano nell'altro quando serve.
- Il formato delle mappe deve restare compatibile all'indietro: un Map Creator non deve mai perdere ciò che non conosce.

## Repository
- Non fare mai push su **bornac1/AuroraPAR** (il progetto originale).

## Flusso di pubblicazione
1. Commit.
2. Push sul ramo di prova (`dev`).
3. Attesa della CI.
4. Link ad Actions.
- Su `main` solo quando lo dice l'utente.

## Test e idee
- Per test e idee usa una checklist interattiva con i pulsanti **Reject** e **Frozen**.
- Gli elementi Frozen vanno nella sezione `## Frozen` di `TODO.md`.
