# Tablet di Oculandia per VRChat

Porta dentro la stanza VRChat di Oculandia le cose del sito [oculandiavr.it](https://oculandiavr.it/): prossimi eventi e ultime news VR, con immagini.

**Devi montare il tablet nel mondo? Parti da [`docs/da-dove-partire.md`](docs/da-dove-partire.md).**

## A che punto siamo

| Pezzo | Scritto | Provato | Note |
| --- | --- | --- | --- |
| Feed dal sito (`tools/build_feed.py`) | Sì | Sì | 27 test senza rete. Generato con i dati veri del sito. |
| Immagini a caselle fisse (`feed/img/`) | Sì | Sì | 22 file, rigenerati identici se non cambia niente. |
| Feed pubblicato su GitHub Pages | Sì | In parte | `tablet.json` è online. Le immagini lo saranno dopo il passaggio su `main`. |
| Workflow automatico ogni 30 minuti | Sì | No | Parte solo da `main`. Da controllare al primo giro. |
| Workflow dei test a ogni push | Sì | No | Stessi comandi provati in locale. |
| Anteprima nel browser (`anteprima/`) | Sì | Sì | Provata con Playwright: schede, pagine, dettaglio, info. |
| Script del tablet (UdonSharp) | Sì | **No** | Mai compilati: nessuno aveva Unity. Vedi [`docs/da-verificare-in-unity.md`](docs/da-verificare-in-unity.md). |
| Menu Oculandia, Crea tablet | Sì | **No** | Come sopra. |
| Tablet personale, inviti, QR | No | No | Idee per dopo. |

## Indirizzi

| Cosa | Indirizzo |
| --- | --- |
| Feed | <https://xcapsa.github.io/oculandia-tablet/feed/tablet.json> |
| Immagini | `https://xcapsa.github.io/oculandia-tablet/feed/img/evento-1.jpg` … `news-10.jpg` |
| Anteprima | <https://xcapsa.github.io/oculandia-tablet/anteprima/> |

`*.github.io` è tra i domini fidati di VRChat sia per il testo sia per le immagini: nessuno deve attivare «Allow Untrusted URLs».

## Cosa può fare VRChat, in breve

- Un mondo può **leggere** testo e immagini da un indirizzo web. Non può scrivere né inviare dati.
- Una richiesta di testo e una di immagini ogni 5 secondi, in tutto il mondo.
- L'indirizzo va scritto nell'editor: il mondo non può comporlo mentre gira.
- VRChat non apre i link nel browser del giocatore.
- Non esiste un login «Accedi con VRChat».

Fonte: creators.vrchat.com, pagine String Loading, Image Loading, External URLs. Lo schema completo è in [`docs/come-funziona.md`](docs/come-funziona.md).

## Struttura

```
tools/build_feed.py              costruisce feed e immagini dai dati pubblici del sito
tools/test_build_feed.py         test, senza rete
feed/tablet.json                 il feed che il mondo scarica
feed/img/                        immagini a caselle fisse
anteprima/index.html             il tablet nel browser
unity/Assets/OculandiaTablet/    script UdonSharp e menu Oculandia per Unity
docs/                            guide in italiano
.github/workflows/feed.yml       feed automatico ogni 30 minuti
.github/workflows/test.yml       test a ogni push e pull request
```

## Comandi

```sh
pip install -r requirements.txt
cd tools && python3 -m unittest test_build_feed     # test
python3 tools/build_feed.py --out feed/tablet.json   # feed e immagini dal sito
python3 -m http.server 8080                          # poi apri http://localhost:8080/anteprima/
```

Serve Python 3.9 o più recente e Pillow (in `requirements.txt`).

## Chi fa cosa

- Sito e feed: questo repo e `xcapsa/oculandia`.
- Mondo, tablet, permessi: chi costruisce la stanza. Il tablet nuovo si aggancia a quello che esiste già.
