# Come funziona

```
 oculandiavr.it                     GitHub (questo repo)                    VRChat
 --------------                     --------------------                    ------

 data/events.json  ---+
                      |   ogni 30 minuti
 api/ocunews-feed.php +-->  workflow «feed»  -->  feed/tablet.json   --+
                      |     tools/build_feed.py   feed/img/*.jpg       |
 locandine e          |     (test, poi                                 |
 copertine (https) ---+      costruzione)                              |
                                   |                                   |
                                   | commit solo se qualcosa           |
                                   | è cambiato davvero                |
                                   v                                   |
                            ramo main  -->  GitHub Pages               |
                                            xcapsa.github.io/          |
                                            oculandia-tablet/feed/ <---+
                                                     |
                                                     | testo: String Loading
                                                     | immagini: Image Loading
                                                     v
                                            tablet nel mondo
                                            (ogni giocatore scarica per sé)
```

## I passaggi

1. **Il sito** pubblica il calendario eventi e il feed delle news.
2. **Il workflow `feed`** gira ogni 30 minuti. Esegue i test, poi `tools/build_feed.py`:
   - prende i prossimi 12 eventi e le ultime 10 news;
   - scrive testi corti, già pronti da mostrare;
   - scarica locandine e copertine, le ritaglia e le salva con nomi fissi (`evento-1.jpg` … `news-10.jpg`);
   - riscrive un file solo se il contenuto cambia.
3. **Se è cambiato qualcosa**, il workflow fa un commit su `main`. GitHub Pages lo pubblica.
4. **Il tablet nel mondo** scarica `tablet.json` all'avvio e poi ogni 10 minuti. Scarica le immagini solo per le righe a schermo, una ogni 5,5 secondi.

## Perché è fatto così

| Limite di VRChat | Scelta |
| --- | --- |
| Il mondo può solo leggere testo e immagini da internet | Tutto è già pronto nel feed: il tablet non calcola niente. |
| Un indirizzo (`VRCUrl`) non si può comporre mentre il mondo gira | Le immagini hanno nomi fissi. Il feed dice quale casella usa ogni voce (`img`) e la sua impronta (`img_v`). |
| Una richiesta di testo ogni 5 secondi, una di immagini ogni 5 secondi, in tutto il mondo | Feed ogni 10 minuti. Immagini in coda, una alla volta, solo quelle visibili. |
| Solo alcuni domini funzionano senza «Allow Untrusted URLs» | GitHub Pages (`*.github.io`) è fidato sia per il testo sia per le immagini. |
| VRChat non apre i link nel browser del giocatore | Il dettaglio mostra l'indirizzo corto, ben leggibile, e un campo con l'indirizzo intero. |
| Niente login VRChat | Il tablet è uguale per tutti. Nessuna sincronizzazione: ogni giocatore sfoglia per conto suo. |

## Quanto ci mette una novità ad arrivare

Al massimo circa 50 minuti: fino a 30 per il workflow, fino a 10 di cache di GitHub Pages, fino a 10 per il giro del tablet. Il pulsante **Aggiorna** salta l'ultima attesa.

## Dove guardare

| Cosa | File |
| --- | --- |
| Campi del feed | [`formato-feed.md`](formato-feed.md) |
| Montaggio in Unity | [`unity-setup.md`](unity-setup.md) |
| Punti non verificati | [`da-verificare-in-unity.md`](da-verificare-in-unity.md) |
| Anteprima | `anteprima/index.html` |
