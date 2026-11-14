# Tablet di Oculandia per VRChat

Porta dentro la stanza VRChat di Oculandia le cose del sito: prossimi eventi e ultime news VR.

**Devi montare il tablet nel mondo? Parti da [`docs/da-dove-partire.md`](docs/da-dove-partire.md).**

## A che punto siamo

| Pezzo | Stato |
| --- | --- |
| Feed dal sito (`tools/build_feed.py`) | Funziona, con 10 test |
| Primo feed vero (`feed/tablet.json`) | Generato il 14 novembre 2026 |
| Script del tablet (`unity/.../OculandiaTabletFeed.cs`) | Scritto, **non ancora provato in Unity** |
| Pubblicazione automatica del feed | Da decidere (vedi sotto) |
| Tablet personale, inviti, QR | Non iniziato |

## Cosa può fare VRChat, in breve

- Un mondo può **leggere** testo e immagini da un indirizzo web. Non può scrivere né inviare dati.
- L'indirizzo deve essere tra quelli fidati da VRChat, oppure il giocatore deve attivare «Allow Untrusted URLs».
- Al massimo una richiesta ogni 5 secondi.
- L'indirizzo va scritto nell'editor: il mondo non può comporlo mentre gira.
- Non esiste un login «Accedi con VRChat».

Fonte: documentazione VRChat Creators, pagine External URLs, String Loading e Image Loading.

## I tre livelli

1. **Bacheca** (questo repo, in corso): eventi e news uguali per tutti.
2. **Tablet personale**: un link personale generato sul sito e incollato una volta nel tablet.
3. **Azioni**: il tablet mostra un codice o un QR, si conferma dal telefono.

## Struttura

```
tools/build_feed.py        costruisce il feed dai dati pubblici del sito
tools/test_build_feed.py   test
feed/tablet.json           il feed che il mondo scarica
docs/formato-feed.md       i campi del feed
docs/da-dove-partire.md    cosa è pronto e cosa serve da chi costruisce il mondo
docs/unity-setup.md        come montare lo script nel mondo e provarlo
unity/Assets/OculandiaTablet/Scripts/   script UdonSharp
```

## Provare il feed

```sh
cd tools && python3 -m unittest test_build_feed
python3 tools/build_feed.py --out feed/tablet.json
```

Serve Python 3.9 o più recente. Nessuna dipendenza.

## Dove pubblicare il feed: da decidere

Il mondo deve scaricare `tablet.json` da un indirizzo fisso. Due strade:

| Strada | Pro | Contro |
| --- | --- | --- |
| GitHub Pages di questo repo (`*.github.io`) | Dominio fidato: funziona a tutti senza toccare le impostazioni | Il repo deve essere pubblico (o l'account a pagamento) |
| Il sito (`oculandiavr.it`) | Nessun passaggio in più | Ogni giocatore deve attivare «Allow Untrusted URLs» |

Il workflow `.github/workflows/feed.yml` è pronto per la prima strada. Parte solo a mano finché non si decide: così non consuma minuti di GitHub Actions.

## Chi fa cosa

- Sito e feed: questo repo e `xcapsa/oculandia`.
- Mondo, tablet, chiamate, permessi, teletrasporto: chi costruisce la stanza. Lo script qui si aggancia al tablet che esiste già.
