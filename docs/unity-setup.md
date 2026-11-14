# Montare il tablet nel mondo

Stessi numeri di [`da-dove-partire.md`](da-dove-partire.md). Il codice non è mai stato compilato in Unity: se trovi un errore, guarda [`da-verificare-in-unity.md`](da-verificare-in-unity.md).

## 1. Installa

- **Unity 2022.3.22f1.** È la versione indicata oggi da VRChat. Non aggiornarla, anche se Unity Hub lo propone.
- **VRChat Creator Companion**, progetto di tipo World.
- **VRChat SDK - Worlds**, l'ultima versione dalla Creator Companion. Dalla 3.4.0 contiene già UdonSharp e ClientSim.

Fonti: creators.vrchat.com, pagine «Current Unity Version» e «Release 3.4.0».

## 2. Importa TextMeshPro

Menu **Window, TextMeshPro, Import TMP Essential Resources**. Se lo hai già fatto, salta.

## 3. Copia la cartella

Copia `unity/Assets/OculandiaTablet` di questo repo dentro `Assets` del progetto. Dentro ci sono:

| File | Cosa fa |
| --- | --- |
| `Scripts/OculandiaTabletFeed.cs` | Scarica il feed, riprova se fallisce, si riaggiorna ogni 10 minuti. |
| `Scripts/OculandiaTabletImmagini.cs` | Scarica le immagini, una ogni 5,5 secondi, solo quelle a schermo. |
| `Scripts/OculandiaTabletSchermo.cs` | Schede, pagine, dettaglio, info. |
| `Editor/OculandiaTabletBuilder.cs` | Il menu **Oculandia**. Non finisce nel mondo caricato. |

Aspetta che Unity finisca di compilare.

## 4. Seleziona il tablet che esiste già

Nella Hierarchy clicca l'oggetto del tablet vecchio su cui vuoi lo schermo. Il tablet nuovo nascerà dentro di lui, nello stesso punto.

Se non selezioni niente, nasce al centro della scena, a 1,3 metri da terra.

## 5. Premi Oculandia, Crea tablet

**La prima volta** compare il messaggio «Primo passo fatto». Il menu ha creato i tre program asset di UdonSharp (file `.asset` accanto agli script). Aspetta che la rotellina in basso a destra si fermi e premi di nuovo **Oculandia, Crea tablet**.

**La seconda volta** il menu:

- crea l'oggetto «Tablet Oculandia»: Canvas in World Space, Graphic Raycaster, VRC Ui Shape, Box Collider, livello Default;
- crea intestazione, schede, quattro righe, pagina di dettaglio, pagina Info, testo di stato;
- aggiunge i tre script sotto «Logica» e li collega tra loro;
- scrive l'indirizzo del feed e i 22 indirizzi delle immagini;
- collega ogni pulsante allo script, con Navigation su None;
- salva il prefab in `Assets/OculandiaTablet/Prefabs/TabletOculandia.prefab`. Se esiste già, ti chiede se sostituirlo.

Misure: tela da 1200 × 800 punti, scala 0,0004. Il tablet misura 48 × 32 cm.

## 6. Sistema posizione e misura

Seleziona «Tablet Oculandia».

- **Misura**: cambia la Scale (tutti e tre i valori uguali) finché copre lo schermo del tablet vecchio. Se il tablet vecchio è già scalato, il nuovo eredita quella scala.
- **Posizione**: spostalo di 1 o 2 mm davanti allo schermo vecchio, così non si sovrappongono.
- **Verso**: chi guarda deve stare dal lato opposto all'asse Z blu. Se il testo appare a specchio, ruota di 180 gradi su Y.
- **Collider**: se il tablet vecchio ha un collider davanti allo schermo, toglilo o rimpiccioliscilo. Copre i pulsanti.

## 7. Premi Oculandia, Controlla collegamenti

Nella Console compare una riga per ogni cosa che manca. Se è tutto a posto: «collegamenti a posto».

Il controllo guarda: riferimenti degli script, indirizzo del feed, 22 indirizzi delle immagini, Canvas in World Space, livello non UI, VRC Ui Shape, Graphic Raycaster, pulsanti collegati, Navigation su None.

## 8. Prova in ClientSim

Premi **Play**. ClientSim parte da solo.

- Nello stato in basso compare «Carico eventi e news…», poi «Dati del …».
- Le righe si riempiono. Le immagini arrivano una alla volta, circa ogni 5 secondi.
- Clicca le schede, Avanti, Indietro, una riga, Torna all'elenco, Aggiorna.

## 9. Prova in VRChat

Nel VRChat SDK Control Panel, scheda Builder: **Build & Test**.

Non serve attivare «Allow Untrusted URLs»: il feed e le immagini stanno su `*.github.io`, che VRChat considera fidato sia per il testo sia per le immagini.

Se puoi, prova anche su Quest con una build Android.

## 10. Lista di controllo

- [ ] Gli script compilano: nessun errore rosso di UdonSharp nella Console.
- [ ] **Crea tablet** finisce senza errori e salva il prefab.
- [ ] **Controlla collegamenti** dice «collegamenti a posto».
- [ ] In ClientSim compaiono eventi e news.
- [ ] Le immagini arrivano, una alla volta.
- [ ] Gli eventi in corso hanno l'etichetta rossa IN CORSO.
- [ ] Schede, Avanti, Indietro e righe rispondono al clic.
- [ ] Il dettaglio mostra testo e indirizzo leggibili.
- [ ] Le lettere accentate (à, è, ì, ò, ù) e i simboli · e … si vedono bene.
- [ ] Con un indirizzo del feed sbagliato compare un messaggio chiaro e, dopo, i dati vecchi restano a schermo.
- [ ] In VRChat (Build & Test) funziona senza «Allow Untrusted URLs».
- [ ] Il tablet si usa anche tenendolo in mano, se è un oggetto da raccogliere.
- [ ] Su Quest il testo è leggibile e non ci sono cali evidenti.

## Se succede questo

| Se succede | Fai |
| --- | --- |
| Il menu **Oculandia** non c'è | C'è un errore di compilazione. Guarda la Console, prima riga rossa. Mandacela. |
| Errore rosso di UdonSharp su uno degli script | Copia il messaggio e mandacelo. Guarda anche `da-verificare-in-unity.md`. |
| «Primo passo fatto» compare a ogni clic | I program asset non vengono riconosciuti. Seleziona ogni script, nell'Inspector premi il pulsante per creare il program asset, poi riprova. |
| «Non trovo il componente VRC Ui Shape» | Aggiungi a mano il componente VRC Ui Shape all'oggetto «Tablet Oculandia». |
| I pulsanti non rispondono in ClientSim | Lancia Controlla collegamenti. Controlla che non ci sia un altro collider davanti alla tela e che la tela non sia sul livello UI. |
| I pulsanti non rispondono solo in VRChat | Stessi controlli. Poi verifica che la scena abbia un EventSystem (il menu lo crea se manca). |
| Stato: «Non riesco a leggere il feed (errore …)» | Apri l'indirizzo del feed nel browser. Se non si apre, il problema è nel feed, non nel mondo: avvisa Rolf. |
| Stato: «Manca l'indirizzo del feed» | Lancia Controlla collegamenti, oppure rilancia Crea tablet. |
| Le righe ci sono ma le immagini restano scure | Normale nei primi secondi. Se dopo un minuto sono ancora scure, guarda la Console: c'è una riga «Immagine non caricata» con il motivo. |
| Lettere accentate come quadratini | Il font di TextMeshPro non le ha. Vedi `da-verificare-in-unity.md`, punto font. |
| Il testo è a specchio | Ruota «Tablet Oculandia» di 180 gradi su Y. |
| Il tablet è enorme o minuscolo | Cambia la Scale di «Tablet Oculandia». Parti da 0,0004. |
| Quando tocchi il campo dell'indirizzo si apre la tastiera | Togli l'oggetto «Campo link» o aggiungigli il componente VRC Input Field Keyboard Override. Il campo è facoltativo. |

## Come segnalare un problema

Apri una issue in questo repo: <https://github.com/xcapsa/oculandia-tablet/issues>. Scrivi:

1. il passo a cui sei arrivato (il numero qui sopra);
2. il messaggio esatto della Console (copia e incolla, non riassumere);
3. una schermata del tablet o della Hierarchy, se aiuta;
4. la versione dell'SDK (Creator Companion, scheda del progetto).

Se preferisci correggere da solo, hai i permessi di scrittura: lavora su un ramo e apri una pull request.
