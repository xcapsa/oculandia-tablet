# Da verificare in Unity

Gli script sono stati scritti leggendo la documentazione ufficiale di VRChat e UdonSharp, **senza Unity**. Nessuna riga C# è stata compilata. Qui sotto ogni punto in cui non siamo sicuri, con cosa fare se non va.

Fonti lette: creators.vrchat.com (String Loading, Image Loading, External URLs, VRC Ui Shape, Current Unity Version, Release 3.4.0), udonsharp.docs.vrchat.com (Editor Scripting, Migration, News).

## Script del mondo (UdonSharp)

| # | Punto | Perché non è certo | Se non va |
| --- | --- | --- | --- |
| 1 | I tre script compilano | Mai compilati. | Mandaci il primo errore rosso della Console. |
| 2 | `OculandiaTabletImmagini` tiene gli scaricamenti in un `object[]` e li riconverte in `IVRCImageDownload` per chiamare `Dispose()` | La documentazione dice di liberare la vecchia immagine prima di sostituirla, ma non dice se un elenco di `IVRCImageDownload` è permesso in UdonSharp. Abbiamo scelto `object[]`, più sicuro. | Se la conversione dà errore: togli le due righe con `_download[indice]` in `OnImageLoadSuccess`. Funziona lo stesso, usa un po' più di memoria nelle serate lunghe. |
| 3 | `new TextureInfo()` e i suoi campi (`GenerateMipMaps`, `WrapModeU`, `WrapModeV`) | Documentati come nodi Udon, non come codice C#. | Se dà errore, passa `null` al posto di `_opzioni` in `DownloadImage`. |
| 4 | `InputField.text` (campo dell'indirizzo intero) | Il campo `InputField` classico di Unity è usato in molti mondi, ma non lo abbiamo trovato nella documentazione di VRChat. | Cancella l'oggetto «Campo link» e svuota `dettaglioCampoLink`: è facoltativo. Resta l'indirizzo corto in verde. |
| 5 | Si può selezionare e copiare il testo del campo in VRChat | Non documentato. Su Quest potrebbe aprirsi la tastiera. | Vedi punto 4, oppure aggiungi VRC Input Field Keyboard Override. |
| 6 | `nameof(...)` in `SendCustomEventDelayedSeconds` | Supportato in UdonSharp 1.x secondo l'uso comune, non letto in documentazione. | Sostituisci `nameof(Tic)` con `"Tic"` e `nameof(Avanza)` con `"Avanza"`. |
| 7 | Chiamate tra script con valore di ritorno (`feed.Campo(...)`, `immagini.Chiedi(...)`) | Supportate in UdonSharp 1.x; non provate qui. | Mandaci l'errore. |
| 8 | L'etichetta «IN CORSO» | `in_corso` è calcolato dal feed al momento della costruzione, per giorno. Un evento di stasera alle 21:30 è «in corso» già dal mattino. | Limite noto, non un errore. Si può migliorare nel feed. |
| 9 | Ritmo delle richieste | VRChat consente una richiesta di testo e una di immagini ogni 5 secondi **per tutto il mondo**. Se nel mondo ci sono altri script che scaricano testo o immagini, si mettono in coda con i nostri. | Niente da fare, solo saperlo: le immagini arriveranno più lente. |
| 10 | String Loading e Image Loading funzionano in ClientSim (Play nell'editor) | La documentazione non lo dice in modo esplicito. | Se in Play resta «Carico eventi e news…», prova con Build & Test prima di cercare l'errore. |

## Menu Oculandia (script dell'editor)

| # | Punto | Perché non è certo | Se non va |
| --- | --- | --- | --- |
| 11 | Creazione automatica dei program asset UdonSharp | Usa la classe `UdonSharp.UdonSharpProgramAsset` e il campo `sourceCsScript` trovati per nome (riflessione), più il metodo `CompileAllCsPrograms` se esiste. Non sono nella documentazione pubblica. Se mancano, il file compila lo stesso e la Console spiega cosa fare. | Crea i program asset a mano: seleziona ogni script, nell'Inspector c'è il pulsante per crearlo. Poi rilancia Crea tablet. |
| 12 | `UdonSharpUndo.AddComponent<T>()` | Documentato nella pagina Editor Scripting. In UdonSharp 1.x non serve `ApplyProxyModifications` (diventato inutile dalla 1.0.0b11): i valori si scrivono direttamente sul componente. | Se i campi risultano vuoti dopo la creazione, lancia Controlla collegamenti e mandaci il risultato. |
| 13 | Ogni script ha il suo UdonBehaviour sullo stesso oggetto, subito dopo `AddComponent` | I pulsanti si collegano a `UdonBehaviour.SendCustomEvent` di quell'oggetto. Se l'UdonBehaviour non c'è ancora, la Console lo dice e i pulsanti restano scollegati. | Rilancia Crea tablet dopo che UdonSharp ha finito di compilare. |
| 14 | Nome della classe di VRC Ui Shape | Cercata per nome (`VRCUiShape` o `VRC_UiShape`). | Se manca, aggiungi a mano il componente VRC Ui Shape a «Tablet Oculandia». |
| 15 | Salvataggio del prefab con dentro script UdonSharp | Usa `PrefabUtility.SaveAsPrefabAssetAndConnect`. | Se fallisce, il tablet nella scena funziona lo stesso. Trascinalo a mano nella cartella Prefabs. |
| 16 | EventSystem | La documentazione chiede di non cancellarlo. Il menu lo crea se manca. | Se ce ne sono due, cancella quello in più. |

## Grafica

| # | Punto | Perché non è certo | Se non va |
| --- | --- | --- | --- |
| 17 | Lettere accentate e simboli `·`, `…`, `–` | Il font predefinito di TextMeshPro (LiberationSans SDF) ha un atlante di base e uno di riserva dinamico. Le lettere accentate dovrebbero arrivare da quello di riserva. | Seleziona LiberationSans SDF, aggiungi i caratteri all'atlante (Font Asset Creator, set «Extended ASCII» più `·…–`), oppure usa un font con quei caratteri. |
| 18 | Il tablet si usa anche quando è in mano | Se il tablet vecchio è un oggetto da raccogliere (VRC Pickup), il puntatore dell'altra mano deve poter toccare la tela. | Prova in VRChat. Se non va, dicci com'è fatto il tablet vecchio. |
| 19 | Leggibilità su Quest | Misure scelte per 48 × 32 cm. | Se è troppo piccolo, aumenta la Scale di «Tablet Oculandia». |

## Pubblicazione del feed

| # | Punto | Stato |
| --- | --- | --- |
| 20 | Le immagini su GitHub Pages | Oggi `feed/img/` risponde 404: sarà online quando il ramo `claude/kit-completo` arriva su `main`. Il feed `tablet.json` è già online. |
| 21 | I commit del workflow aggiornano GitHub Pages | Il workflow fa push con il token automatico di GitHub. Dopo il primo giro programmato, controlla che il sito del feed mostri l'ora nuova. |
| 22 | Cache di GitHub Pages | Risponde con `cache-control: max-age=600`: una novità può arrivare con 10 minuti di ritardo. Verificato con curl il 14 novembre 2026. |
