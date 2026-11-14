# Montare lo script nel mondo

Lo script `OculandiaTabletFeed.cs` è stato scritto dalla documentazione di VRChat. **Non è stato compilato né provato in Unity.** Questa pagina è la lista di cosa provare.

## Cosa serve

- Progetto Unity del mondo con VRChat Worlds SDK 3.x e UdonSharp.
- TextMeshPro (di solito già presente).

## Passi

1. Copia la cartella `unity/Assets/OculandiaTablet` dentro `Assets` del progetto.
2. Nel tablet crea una schermata con:
   - da 4 a 6 righe evento, ognuna con tre testi: titolo, quando, dove;
   - da 4 a 6 righe news, ognuna con tre testi: titolo, data, riga;
   - un testo piccolo per lo stato («Aggiornato: …»).
3. Aggiungi a un oggetto del tablet il componente `OculandiaTabletFeed`.
4. Trascina i testi negli elenchi, nello stesso ordine delle righe. Gli elenchi `eventRows` e `newsRows` contengono l'oggetto di ogni riga: lo script nasconde le righe vuote.
5. In `feedUrl` scrivi l'indirizzo del feed.
6. (Facoltativo) Collega un pulsante «Aggiorna» all'evento `Load`.

## Cosa provare

- [ ] Lo script compila senza errori in UdonSharp.
- [ ] In ClientSim o in una build di prova le righe si riempiono.
- [ ] Con un indirizzo sbagliato compare il messaggio di errore e, dopo il primo caricamento riuscito, i dati vecchi restano a schermo.
- [ ] Con «Allow Untrusted URLs» spento: funziona se il feed è su `*.github.io`, non funziona se è su `oculandiavr.it`.
- [ ] Dopo `refreshMinutes` il feed viene riscaricato.
- [ ] Su Quest (Android) il testo è leggibile e non ci sono cali evidenti.

## Punti da controllare nel codice

- `VRCJson` restituisce i numeri come `Double`: per questo `v` viene letto così.
- Se nel mondo ci sono altri script che scaricano testo o immagini, tutti insieme non devono superare una richiesta ogni 5 secondi.
- Il tablet è personale: lo script è locale (`BehaviourSyncMode.None`), ogni giocatore scarica per sé.

## Segnalare un problema

Apri una issue in questo repo con il messaggio d'errore di UdonSharp o uno screenshot del tablet.
