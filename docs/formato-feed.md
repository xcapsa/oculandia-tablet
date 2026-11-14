# Formato del feed

File: `feed/tablet.json`. UTF-8, un campo per riga, sotto i 60 kB (oggi circa 7 kB).

```json
{
  "v": 1,
  "generato": "2026-11-14T14:27:00+0100",
  "aggiornato": "14 nov · 14:27",
  "eventi": [
    {
      "titolo": "Population: One",
      "quando": "lun 9 nov – 22 nov",
      "giorno": "2026-11-09",
      "ora": "",
      "tipo": "Torneo",
      "dove": "AtlasGate",
      "in_corso": true,
      "link": "https://oculandiavr.it/atlasgate"
    }
  ],
  "news": [
    {
      "titolo": "Sol Protocol arriva in accesso anticipato su PC VR",
      "data": "14 nov",
      "riga": "Prima riga della notizia, al massimo 140 caratteri…",
      "link": "https://oculandiavr.it/n/sol-protocol-accesso-6826c"
    }
  ]
}
```

## Regole

- `v` cambia solo se il formato diventa incompatibile. Lo script del tablet rifiuta le versioni che non conosce.
- `eventi`: al massimo 12, dal più vicino. Quelli già finiti non ci sono. `in_corso` è vero se l'evento è iniziato e non è finito.
- `news`: al massimo 10, dalla più recente. Solo articoli confermati con indirizzo `https://oculandiavr.it/n/...`.
- Tutti i testi sono già pronti da mostrare: una riga, senza tag, in italiano. Il tablet non deve formattare date.
- `link` è sempre `https://`. Serve per il QR o per il codice da mostrare accanto alla riga.

## Da dove arrivano i dati

| Campo | Fonte |
| --- | --- |
| `eventi` | `https://oculandiavr.it/data/events.json` |
| `news` | `https://oculandiavr.it/api/ocunews-feed.php` |

## Limite noto

Le serate film oggi non sono nel calendario del sito, quindi non compaiono nemmeno qui. Compariranno quando entreranno nel calendario.
