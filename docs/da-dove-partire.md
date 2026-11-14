# Da dove partire

Ciao Kill. Questa è la strada intera, dall'inizio alla prova. I dettagli di ogni passo sono in [`unity-setup.md`](unity-setup.md), con gli stessi numeri.

## Cosa fa il tablet

Mostra nel mondo VRChat i prossimi eventi e le ultime news di [oculandiavr.it](https://oculandiavr.it/).
Tre schede: **Eventi**, **News**, **Info**. Quattro righe per pagina, con immagine. Tocchi una riga e si apre il dettaglio con il testo e l'indirizzo da scrivere nel browser.

Vuoi vederlo prima di aprire Unity? Apri l'anteprima: <https://xcapsa.github.io/oculandia-tablet/anteprima/> (sarà attiva quando questo ramo arriva su `main`; prima la apri in locale, vedi sotto).

## Il percorso

1. **Installa** Unity 2022.3.22f1 e il pacchetto VRChat SDK - Worlds dalla Creator Companion. UdonSharp è già dentro l'SDK.
2. **Importa TextMeshPro**: menu Window, TextMeshPro, Import TMP Essential Resources.
3. **Copia la cartella** `unity/Assets/OculandiaTablet` di questo repo dentro `Assets` del progetto del mondo.
4. **Seleziona il tablet che esiste già** nella Hierarchy (l'oggetto dello schermo). Se non selezioni niente, il tablet nasce al centro della scena.
5. **Premi Oculandia, Crea tablet.** La prima volta prepara i programmi UdonSharp e ti chiede di premere di nuovo dopo la compilazione. La seconda volta crea tutto: grafica, script, collegamenti, indirizzi, prefab.
6. **Sistema posizione e misura** sopra lo schermo del tablet vecchio.
7. **Premi Oculandia, Controlla collegamenti.** Nella Console deve comparire «collegamenti a posto».
8. **Prova in ClientSim**: premi Play.
9. **Prova in VRChat** con Build & Test.
10. **Spunta la lista di controllo** in `unity-setup.md` e dicci com'è andata.

Non devi scrivere codice né trascinare riferimenti. Se qualcosa non va, guarda la tabella «Se succede questo» in `unity-setup.md`.

## Cosa è pronto e cosa no

| Pezzo | Stato |
| --- | --- |
| Feed con eventi, news e immagini | Funziona. Si aggiorna da solo ogni 30 minuti. |
| Anteprima nel browser | Funziona. |
| Script del tablet e menu Oculandia | Scritti dalla documentazione di VRChat. **Mai compilati in Unity**: nessuno di noi aveva Unity a disposizione. |

Per questo esiste [`da-verificare-in-unity.md`](da-verificare-in-unity.md): l'elenco dei punti in cui potresti trovare un errore e cosa fare.

## Anteprima in locale

```sh
python3 -m http.server 8080
```

Dalla cartella del repo. Poi apri `http://localhost:8080/anteprima/`.

## Le idee per dopo

1. **Bacheca** (questo lavoro): eventi e news uguali per tutti.
2. **Tablet personale**: un link personale creato sul sito e incollato una volta nel tablet.
3. **Azioni**: il tablet mostra un codice o un QR e si conferma dal telefono.

Le tue idee contano: scrivile in una issue.
