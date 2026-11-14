# Da dove partire (per chi monta il tablet nel mondo)

Ciao Kill. Qui trovi cosa è già pronto e cosa serve da te.

## Cosa è pronto

- **Il feed**: `feed/tablet.json`, un file di testo con i prossimi 12 eventi e le ultime 10 news del sito. Pesa circa 6 kB. I campi sono spiegati in `docs/formato-feed.md`.
- **Lo script**: `unity/Assets/OculandiaTablet/Scripts/OculandiaTabletFeed.cs` (UdonSharp). Scarica il feed, lo legge e riempie i testi del tablet. Si riaggiorna da solo.
- **I test del feed**: 10, passano tutti.

## Cosa non è pronto

- Lo script **non è mai stato compilato né provato in Unity**. È scritto dalla documentazione di VRChat: aspettati qualche correzione.
- Il feed non ha ancora un indirizzo pubblico fisso (vedi «L'indirizzo del feed»).
- Non c'è nessuna grafica: lo script riempie i testi che gli colleghi tu.

## Cosa serve da te

1. Copia `unity/Assets/OculandiaTablet` nel progetto del mondo.
2. Segui `docs/unity-setup.md`: è la lista dei passi e delle prove.
3. Dicci cosa non compila o non funziona: apri una issue qui, con l'errore di UdonSharp o uno screenshot.

Se preferisci cambiare lo script a modo tuo, fallo pure: hai i permessi di scrittura. Lavora su un ramo e apri una pull request.

## L'indirizzo del feed

Per provare subito in Unity:

1. Copia il contenuto di `feed/tablet.json` in un posto che puoi raggiungere con un indirizzo web (per esempio un Gist pubblico di GitHub, pulsante «Raw»).
2. Metti quell'indirizzo in `feedUrl`.
3. In VRChat attiva «Allow Untrusted URLs» se l'indirizzo non è tra quelli fidati.

L'indirizzo definitivo lo decide Rolf: GitHub Pages di questo repo (fidato da VRChat, ma il repo deve diventare pubblico) oppure il sito `oculandiavr.it` (ogni giocatore deve attivare «Allow Untrusted URLs»).

## I limiti di VRChat da ricordare

- Il mondo può solo **leggere** testo e immagini. Non può inviare dati al sito.
- Al massimo una richiesta ogni 5 secondi, contando tutti gli script del mondo.
- L'indirizzo va scritto nell'editor: non si può comporre mentre il mondo gira.
- Non esiste un login «Accedi con VRChat».

## Le idee per dopo

1. **Bacheca** (questo lavoro): eventi e news uguali per tutti.
2. **Tablet personale**: un link personale creato sul sito e incollato una volta nel tablet.
3. **Azioni**: il tablet mostra un codice o un QR e si conferma dal telefono.

Le tue idee contano: scrivile in una issue.
