# Formato del feed

Il feed sono due cose, pubblicate insieme con GitHub Pages:

| Cosa | Indirizzo |
| --- | --- |
| Il testo | `https://xcapsa.github.io/oculandia-tablet/feed/tablet.json` |
| Le immagini | `https://xcapsa.github.io/oculandia-tablet/feed/img/evento-1.jpg` … `evento-12.jpg`, `news-1.jpg` … `news-10.jpg` |

Li costruisce `tools/build_feed.py`.

## Il testo: `feed/tablet.json`

UTF-8, un campo per riga. Limite 100 kB, oggi circa 15 kB.

```json
{
  "v": 1,
  "generato": "2026-11-14T19:04:00+0100",
  "aggiornato": "14 nov · 19:04",
  "img_base": "https://xcapsa.github.io/oculandia-tablet/feed/img/",
  "caselle": { "eventi": 12, "news": 10 },
  "eventi": [
    {
      "id": "atlasgate-population-one-2026-11",
      "titolo": "Population: One",
      "quando": "lun 9 nov – 22 nov",
      "giorno": "2026-11-09",
      "ora": "",
      "tipo": "Torneo",
      "dove": "Squadre",
      "testo": "Torneo della stagione AtlasGate 2026–2027, in collaborazione con Oculandia.",
      "in_corso": true,
      "link": "https://discord.gg/MNbn9usjm9",
      "link_corto": "discord.gg/MNbn9usjm9",
      "img": 2,
      "img_v": "5f0c9a1e"
    }
  ],
  "news": [
    {
      "id": "sol-protocol-accesso-6826c",
      "titolo": "Sol Protocol arriva in accesso anticipato su PC VR",
      "data": "14 nov",
      "cat": "Hardware",
      "riga": "Prima riga della notizia, al massimo 140 caratteri…",
      "testo": "Inizio della notizia, al massimo 420 caratteri…",
      "link": "https://oculandiavr.it/n/sol-protocol-accesso-6826c",
      "link_corto": "oculandiavr.it/n/sol-protocol-accesso-6826c",
      "img": 1,
      "img_v": "290d71c4"
    }
  ]
}
```

### Campi generali

| Campo | Significato |
| --- | --- |
| `v` | Versione del formato. Cambia solo se il formato diventa incompatibile. Il tablet rifiuta le versioni che non conosce. |
| `generato`, `aggiornato` | Quando il contenuto è cambiato l'ultima volta (ora di Roma). Se a un giro non cambia niente, il file resta com'è e questi orari non si muovono. |
| `img_base` | Cartella pubblica delle immagini. Serve all'anteprima e a chi legge: il mondo VRChat non può usarlo, i suoi indirizzi sono scritti nell'editor. |
| `caselle` | Quante caselle immagine esistono per tipo. |

### Campi di eventi e news

| Campo | Dove | Significato |
| --- | --- | --- |
| `id` | tutti | Nome stabile della voce. Serve a ridarle la stessa casella immagine al giro dopo. |
| `titolo` | tutti | Una riga. Eventi fino a 70 caratteri, news fino a 90. |
| `quando`, `giorno`, `ora`, `tipo`, `dove` | eventi | Data già scritta in italiano, giorno ISO, ora `HH:MM` o vuota, tipo (Torneo, Serata, Film…), piattaforma o formato. |
| `in_corso` | eventi | Vero se l'evento è iniziato e non è finito. |
| `data`, `cat` | news | Giorno già scritto («14 nov») e categoria (può essere vuota). |
| `riga` | news | Inizio della notizia, fino a 140 caratteri. Per l'elenco. |
| `testo` | tutti | Testo più lungo per la scheda di dettaglio. Eventi fino a 240 caratteri, news fino a 420. |
| `link` | tutti | Sempre `https://`. |
| `link_corto` | tutti | Lo stesso indirizzo senza `https://`: è quello che il tablet mostra, perché VRChat non apre i link. |
| `img` | tutti | Numero della casella immagine. Evento con `img: 2` usa `evento-2.jpg`. |
| `img_v` | tutti | Impronta di 8 caratteri del file immagine. Se cambia, il tablet riscarica quella casella. Vuota solo con `--no-img` e nessun file. |

### Regole

- `eventi`: al massimo 12, dal più vicino. Quelli già finiti non ci sono.
- `news`: al massimo 10, dalla più recente. Solo articoli confermati con indirizzo `https://oculandiavr.it/n/...`.
- Tutti i testi sono pronti da mostrare: una riga, senza tag, senza emoji, in italiano. Il tablet non formatta niente.
- Aggiungere campi nuovi non cambia `v`. Il tablet ignora i campi che non conosce.

## Le immagini: `feed/img/`

| File | Misura | Contenuto |
| --- | --- | --- |
| `evento-1.jpg` … `evento-12.jpg` | 640 × 800 (in piedi, 4:5) | Locandina dell'evento |
| `news-1.jpg` … `news-10.jpg` | 768 × 432 (sdraiata, 16:9) | Copertina della news |
| `fonti.json` | | Da quale indirizzo arriva ogni file. Serve solo allo script. |

Perché nomi fissi: un mondo VRChat può scaricare solo indirizzi scritti nell'editor. Il tablet conosce 22 indirizzi e il feed gli dice quale usare per ogni voce.

Regole:

- **Ci sono sempre tutti i 22 file.** Se un evento non ha locandina, o lo scaricamento fallisce, il file è un'immagine di riserva: tinta unita con il titolo (senza accenti, il font incluso non li ha). Le caselle libere hanno la scritta «Oculandia».
- **Le caselle sono stabili.** Una voce tiene la sua casella finché resta nel feed. Quando esce una news, quella nuova prende la casella liberata: cambia un file solo, non dieci.
- **Un file si riscrive solo se cambia.** Stessa immagine di partenza, stessi byte.
- Se uno scaricamento fallisce ma c'era già l'immagine buona dello stesso indirizzo, resta quella.
- Si scaricano solo immagini `https` da `oculandiavr.it` e `minecraft.oculandiavr.it`.
- Limite VRChat: 2048 × 2048. Qui il lato più lungo è 800: meno memoria nel visore.

## Da dove arrivano i dati

| Cosa | Fonte |
| --- | --- |
| `eventi` | `https://oculandiavr.it/data/events.json` (locandina: campo `image_url`) |
| `news` | `https://oculandiavr.it/api/ocunews-feed.php` (copertina: campo `image_url`) |

## Comandi

```sh
pip install -r requirements.txt
cd tools && python3 -m unittest test_build_feed    # 27 test, senza rete
python3 tools/build_feed.py --out feed/tablet.json  # feed e immagini dal sito
python3 tools/build_feed.py --out feed/tablet.json --no-img   # solo il testo
```

## Limiti noti

- Le serate film oggi non sono nel calendario del sito, quindi non compaiono nemmeno qui.
- Il feed non ha un campo «offerta»: il colore oro dello stile di Oculandia nel tablet non è ancora usato.
