// Costruttore del tablet di Oculandia. Solo editor: non finisce nel mondo caricato.
//
// Menu Oculandia, Crea tablet:
//   crea da zero il tablet nella scena (Canvas world space con VRC Ui Shape),
//   aggiunge i tre script, collega tutti i riferimenti, scrive gli indirizzi
//   del feed e delle immagini, collega i pulsanti e salva il prefab.
// Menu Oculandia, Controlla collegamenti:
//   elenca nella Console cosa manca nei tablet della scena.
// Menu Oculandia, Apri anteprima nel browser.
//
// Le misure sono le stesse di anteprima/index.html: tela 1200 x 800 punti.
// Scritto dalla documentazione di VRChat e UdonSharp: vedi docs/da-verificare-in-unity.md.

using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

public static class OculandiaTabletBuilder
{
    public const string FeedUrl = "https://xcapsa.github.io/oculandia-tablet/feed/tablet.json";
    public const string ImgBase = "https://xcapsa.github.io/oculandia-tablet/feed/img/";
    public const string AnteprimaUrl = "https://xcapsa.github.io/oculandia-tablet/anteprima/";
    public const int CaselleEventi = 12;
    public const int CaselleNews = 10;
    public const int Righe = 4;

    private const string Cartella = "Assets/OculandiaTablet";
    private const string CartellaPrefab = Cartella + "/Prefabs";
    private const string PercorsoPrefab = CartellaPrefab + "/TabletOculandia.prefab";

    // Tela: 1200 x 800 punti. Con scala 0,0004 il tablet misura 48 x 32 cm.
    private const float Larghezza = 1200f;
    private const float Altezza = 800f;
    private const float Scala = 0.0004f;

    // Colori di Oculandia.
    private static readonly Color Fondo = Hex("0e1712");
    private static readonly Color Superficie = Hex("102016");
    private static readonly Color Bordo = Hex("1d3526");
    private static readonly Color Verde = Hex("8ce596");
    private static readonly Color Chiaro = Hex("eef6f0");
    private static readonly Color Spento = Hex("9db5a4");
    private static readonly Color Azzurro = Hex("77d6ed");
    private static readonly Color Rosso = Hex("ff4d5d");
    private static readonly Color ColoreVuoto = Hex("162a1d");

    // ------------------------------------------------------------------ menu

    [MenuItem("Oculandia/Crea tablet", false, 1)]
    public static void CreaTablet()
    {
        if (!ProgrammiPronti()) return;

        Transform genitore = Selection.activeTransform;
        GameObject radice = CostruisciTablet(genitore);
        Undo.RegisterCreatedObjectUndo(radice, "Crea tablet Oculandia");
        EditorSceneManager.MarkSceneDirty(radice.scene);

        string prefab = SalvaPrefab(radice);
        Selection.activeGameObject = radice;
        EditorGUIUtility.PingObject(radice);

        int problemi = Controlla(radice.GetComponentInChildren<OculandiaTabletSchermo>(true));
        string dove = genitore != null ? "dentro «" + genitore.name + "»" : "al centro della scena";
        EditorUtility.DisplayDialog(
            "Tablet di Oculandia",
            "Tablet creato " + dove + ".\n\n" +
            (prefab != "" ? "Prefab salvato in " + prefab + ".\n\n" : "Prefab non salvato: guarda la Console.\n\n") +
            (problemi == 0
                ? "Tutti i collegamenti sono a posto. Premi Play per provarlo in ClientSim."
                : "Ci sono " + problemi + " collegamenti da controllare: guarda la Console."),
            "Va bene");
    }

    [MenuItem("Oculandia/Controlla collegamenti", false, 2)]
    public static void ControllaCollegamenti()
    {
        OculandiaTabletSchermo[] schermi = UnityEngine.Object.FindObjectsOfType<OculandiaTabletSchermo>(true);
        if (schermi.Length == 0)
        {
            Debug.LogWarning("[Oculandia] Nessun tablet nella scena. Usa il menu Oculandia, Crea tablet.");
            return;
        }
        int totale = 0;
        foreach (OculandiaTabletSchermo schermo in schermi) totale += Controlla(schermo);
        if (totale == 0) Debug.Log("[Oculandia] Controllo finito: tutto a posto (" + schermi.Length + " tablet).");
        else Debug.LogWarning("[Oculandia] Controllo finito: " + totale + " problemi. Leggi le righe sopra.");
    }

    [MenuItem("Oculandia/Apri anteprima nel browser", false, 20)]
    public static void ApriAnteprima()
    {
        Application.OpenURL(AnteprimaUrl);
    }

    // ------------------------------------------------------------------ programmi UdonSharp

    // UdonSharp vuole un «program asset» accanto a ogni script. Se manca, lo creo io.
    // Uso la riflessione: se il nome della classe cambiasse, questo file compila lo stesso
    // e la Console spiega cosa fare a mano.
    private static bool ProgrammiPronti()
    {
        Type tipoProgramma = CercaTipo("UdonSharp.UdonSharpProgramAsset");
        if (tipoProgramma == null)
        {
            Debug.LogError("[Oculandia] Non trovo UdonSharp. Serve il pacchetto VRChat SDK - Worlds (contiene UdonSharp).");
            EditorUtility.DisplayDialog("Tablet di Oculandia", "Non trovo UdonSharp nel progetto. Serve VRChat SDK - Worlds dalla Creator Companion.", "Va bene");
            return false;
        }
        FieldInfo campoScript = tipoProgramma.GetField("sourceCsScript", BindingFlags.Public | BindingFlags.Instance);

        var esistenti = new HashSet<MonoScript>();
        foreach (string guid in AssetDatabase.FindAssets("t:" + tipoProgramma.Name))
        {
            UnityEngine.Object programma = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid), tipoProgramma);
            if (programma != null && campoScript != null && campoScript.GetValue(programma) is MonoScript script) esistenti.Add(script);
        }

        int creati = 0;
        foreach (Type tipo in new[] { typeof(OculandiaTabletFeed), typeof(OculandiaTabletImmagini), typeof(OculandiaTabletSchermo) })
        {
            MonoScript script = TrovaScript(tipo);
            if (script == null)
            {
                Debug.LogError("[Oculandia] Non trovo il file " + tipo.Name + ".cs. Hai importato tutta la cartella OculandiaTablet?");
                return false;
            }
            if (esistenti.Contains(script)) continue;
            if (campoScript == null)
            {
                Debug.LogError("[Oculandia] Non riesco a creare il program asset di " + tipo.Name + ". Fallo a mano: tasto destro sul file, Create, U# Script... oppure dal pulsante nell'Inspector.");
                return false;
            }
            ScriptableObject programma = ScriptableObject.CreateInstance(tipoProgramma);
            campoScript.SetValue(programma, script);
            string percorso = AssetDatabase.GetAssetPath(script);
            percorso = percorso.Substring(0, percorso.Length - 3) + ".asset";
            AssetDatabase.CreateAsset(programma, percorso);
            Debug.Log("[Oculandia] Creato " + percorso);
            creati++;
        }
        if (creati == 0) return true;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        MethodInfo compila = null;
        foreach (MethodInfo metodo in tipoProgramma.GetMethods(BindingFlags.Public | BindingFlags.Static))
            if (metodo.Name == "CompileAllCsPrograms" && compila == null) compila = metodo;
        if (compila != null)
        {
            var argomenti = new object[compila.GetParameters().Length];
            for (int i = 0; i < argomenti.Length; i++)
            {
                ParameterInfo p = compila.GetParameters()[i];
                argomenti[i] = p.HasDefaultValue ? p.DefaultValue : (p.ParameterType == typeof(bool) ? (object)false : null);
            }
            try { compila.Invoke(null, argomenti); }
            catch (Exception errore) { Debug.LogWarning("[Oculandia] Compilazione automatica non riuscita: " + errore.Message); }
        }
        EditorUtility.DisplayDialog(
            "Tablet di Oculandia",
            "Primo passo fatto: ho preparato " + creati + " programmi UdonSharp.\n\n" +
            "Aspetta che Unity finisca di compilare (la rotellina in basso a destra si ferma), " +
            "poi premi di nuovo Oculandia, Crea tablet.",
            "Va bene");
        return false;
    }

    private static MonoScript TrovaScript(Type tipo)
    {
        foreach (string guid in AssetDatabase.FindAssets(tipo.Name + " t:MonoScript"))
        {
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
            if (script != null && script.GetClass() == tipo) return script;
        }
        return null;
    }

    private static Type CercaTipo(string nomeCompleto)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type tipo = assembly.GetType(nomeCompleto, false);
            if (tipo != null) return tipo;
        }
        return null;
    }

    // ------------------------------------------------------------------ costruzione

    private static GameObject CostruisciTablet(Transform genitore)
    {
        // La radice è la tela. Livello Default: sul livello UI non si potrebbe toccare.
        var radice = new GameObject("Tablet Oculandia", typeof(RectTransform));
        radice.layer = 0;
        if (genitore != null)
        {
            radice.transform.SetParent(genitore, false);
            radice.layer = genitore.gameObject.layer == LayerMask.NameToLayer("UI") ? 0 : genitore.gameObject.layer;
        }
        else
        {
            radice.transform.position = new Vector3(0f, 1.3f, 0f);
        }
        radice.transform.localRotation = Quaternion.identity;
        radice.transform.localScale = Vector3.one * Scala;

        var tela = radice.AddComponent<Canvas>();
        tela.renderMode = RenderMode.WorldSpace;
        var rt = (RectTransform)radice.transform;
        rt.sizeDelta = new Vector2(Larghezza, Altezza);
        radice.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;
        radice.AddComponent<GraphicRaycaster>();
        AggiungiUiShape(radice);
        var collider = radice.AddComponent<BoxCollider>();
        collider.size = new Vector3(Larghezza, Altezza, 4f);
        AssicuraEventSystem();

        // Logica: tre oggetti, uno script ciascuno. Così ogni pulsante trova il suo UdonBehaviour.
        var logica = Vuoto("Logica", radice.transform);
        var feed = UdonSharpUndo.AddComponent<OculandiaTabletFeed>(Vuoto("Feed", logica.transform));
        var immagini = UdonSharpUndo.AddComponent<OculandiaTabletImmagini>(Vuoto("Immagini", logica.transform));
        var schermo = UdonSharpUndo.AddComponent<OculandiaTabletSchermo>(Vuoto("Schermo", logica.transform));
        UdonBehaviour udonSchermo = schermo.GetComponent<UdonBehaviour>();
        if (udonSchermo == null)
            Debug.LogError("[Oculandia] Lo schermo non ha un UdonBehaviour: i pulsanti resteranno scollegati. Guarda docs/da-verificare-in-unity.md.");

        // Dati e indirizzi.
        feed.feedUrl = new VRCUrl(FeedUrl);
        feed.minutiAggiornamento = 10;
        feed.schermo = schermo;
        immagini.urlEventi = Indirizzi("evento-", CaselleEventi);
        immagini.urlNews = Indirizzi("news-", CaselleNews);
        immagini.schermo = schermo;
        schermo.feed = feed;
        schermo.immagini = immagini;

        Transform t = radice.transform;
        Pannello("Fondo", t, 0, 0, Larghezza, Altezza, Fondo, false);

        // Intestazione.
        Pannello("Intestazione", t, 0, 0, Larghezza, 96, Superficie, false);
        Testo("Marchio", t, 32, 14, 420, 44, "OCULANDIA", 34, Verde, true);
        Testo("Sottotitolo", t, 32, 56, 420, 28, "Eventi e news dalla community VR italiana", 18, Spento, false);
        string[] nomi = { "Eventi", "News", "Info" };
        string[] eventi = { "SchedaEventi", "SchedaNews", "SchedaInfo" };
        schermo.sfondiSchede = new Image[3];
        schermo.testiSchede = new TextMeshProUGUI[3];
        for (int i = 0; i < 3; i++)
        {
            Button scheda = Pulsante("Scheda " + nomi[i], t, 560 + i * 162, 20, 150, 56, nomi[i], Superficie, Chiaro, udonSchermo, eventi[i]);
            schermo.sfondiSchede[i] = scheda.GetComponent<Image>();
            schermo.testiSchede[i] = scheda.GetComponentInChildren<TextMeshProUGUI>(true);
        }
        Pulsante("Aggiorna", t, 1050, 20, 126, 56, "Aggiorna", Bordo, Chiaro, udonSchermo, "Aggiorna");

        // Stato: sempre visibile in basso.
        schermo.testoStato = Testo("Stato", t, 220, 764, 760, 26, "Carico eventi e news…", 16, Spento, false, TextAlignmentOptions.Center);

        CostruisciElenco(t, schermo, udonSchermo);
        CostruisciDettaglio(t, schermo, udonSchermo);
        CostruisciInfo(t, schermo);
        schermo.paginaDettaglio.SetActive(false);
        schermo.paginaInfo.SetActive(false);

        // In UdonSharp 1.x i valori stanno nel componente C#: basta segnarli come cambiati.
        EditorUtility.SetDirty(feed);
        EditorUtility.SetDirty(immagini);
        EditorUtility.SetDirty(schermo);
        return radice;
    }

    private static void CostruisciElenco(Transform t, OculandiaTabletSchermo s, UdonBehaviour udon)
    {
        GameObject pagina = Vuoto("Pagina elenco", t);
        s.paginaElenco = pagina;
        Transform p = pagina.transform;

        s.righe = new GameObject[Righe];
        s.vistaEvento = new GameObject[Righe];
        s.locandine = new RawImage[Righe];
        s.titoliEvento = new TextMeshProUGUI[Righe];
        s.quandoEvento = new TextMeshProUGUI[Righe];
        s.tipoEvento = new TextMeshProUGUI[Righe];
        s.badgeInCorso = new GameObject[Righe];
        s.vistaNews = new GameObject[Righe];
        s.copertine = new RawImage[Righe];
        s.titoliNews = new TextMeshProUGUI[Righe];
        s.dateNews = new TextMeshProUGUI[Righe];
        s.righeNews = new TextMeshProUGUI[Righe];

        for (int r = 0; r < Righe; r++)
        {
            // La riga intera è un pulsante: apre il dettaglio.
            Button riga = Pulsante("Riga " + (r + 1), p, 24, 112 + r * 148, 1152, 140, "", Superficie, Chiaro, udon, "Riga" + r);
            s.righe[r] = riga.gameObject;
            Transform rt = riga.transform;

            // Vista evento: locandina 4:5 a sinistra.
            GameObject ve = Vuoto("Vista evento", rt, 0, 0, 1152, 140);
            s.vistaEvento[r] = ve;
            s.locandine[r] = Riquadro("Locandina", ve.transform, 0, 0, 112, 140);
            s.titoliEvento[r] = Testo("Titolo", ve.transform, 132, 12, 860, 42, "Titolo evento", 30, Chiaro, true);
            s.quandoEvento[r] = Testo("Quando", ve.transform, 132, 58, 860, 30, "sab 21 nov · 21:30", 22, Azzurro, false);
            s.tipoEvento[r] = Testo("Tipo", ve.transform, 132, 92, 860, 28, "Serata · VRChat", 20, Spento, false);
            s.badgeInCorso[r] = Etichetta("In corso", ve.transform, 1006, 14, 130, 34);

            // Vista news: copertina 16:9 a sinistra.
            GameObject vn = Vuoto("Vista news", rt, 0, 0, 1152, 140);
            s.vistaNews[r] = vn;
            s.copertine[r] = Riquadro("Copertina", vn.transform, 0, 0, 248, 140);
            s.titoliNews[r] = Testo("Titolo", vn.transform, 268, 10, 864, 64, "Titolo della news", 25, Chiaro, true);
            s.titoliNews[r].enableWordWrapping = true;
            s.dateNews[r] = Testo("Data", vn.transform, 268, 76, 864, 26, "14 nov · Hardware", 19, Azzurro, false);
            s.righeNews[r] = Testo("Riga", vn.transform, 268, 104, 864, 28, "Prima riga della notizia", 18, Spento, false);
            vn.SetActive(false);
        }

        s.testoVuoto = Testo("Vuoto", p, 24, 360, 1152, 60, "Carico eventi e news…", 26, Spento, false, TextAlignmentOptions.Center);
        s.pulsanteIndietro = Pulsante("Pagina indietro", p, 24, 708, 190, 50, "Indietro", Bordo, Chiaro, udon, "Indietro").gameObject;
        s.pulsanteAvanti = Pulsante("Pagina avanti", p, 986, 708, 190, 50, "Avanti", Bordo, Chiaro, udon, "Avanti").gameObject;
        s.testoPagina = Testo("Numero pagina", p, 400, 716, 400, 34, "Pagina 1 di 1", 22, Chiaro, false, TextAlignmentOptions.Center);
    }

    private static void CostruisciDettaglio(Transform t, OculandiaTabletSchermo s, UdonBehaviour udon)
    {
        GameObject pagina = Vuoto("Pagina dettaglio", t);
        s.paginaDettaglio = pagina;
        Transform p = pagina.transform;

        Pannello("Sfondo", p, 24, 112, 1152, 590, Superficie, false);
        s.dettaglioLocandina = Riquadro("Locandina", p, 44, 132, 440, 550);
        s.dettaglioCopertina = Riquadro("Copertina", p, 44, 132, 440, 248);
        s.dettaglioTitolo = Testo("Titolo", p, 512, 128, 640, 96, "Titolo", 32, Chiaro, true);
        s.dettaglioTitolo.enableWordWrapping = true;
        s.dettaglioQuando = Testo("Quando", p, 512, 228, 640, 30, "Quando", 22, Azzurro, false);
        s.dettaglioInCorso = Etichetta("In corso", p, 512, 264, 130, 34);
        s.dettaglioTesto = Testo("Testo", p, 512, 308, 640, 230, "Testo", 20, Chiaro, false);
        s.dettaglioTesto.enableWordWrapping = true;
        s.dettaglioTesto.overflowMode = TextOverflowModes.Ellipsis;
        Testo("Avviso link", p, 512, 548, 640, 24, "VRChat non apre i link. Scrivi questo indirizzo nel browser:", 16, Spento, false);
        s.dettaglioLink = Testo("Link", p, 512, 574, 640, 34, "oculandiavr.it", 24, Verde, true);
        s.dettaglioCampoLink = CampoLink(p, 512, 616, 640, 44);
        Pulsante("Torna all'elenco", p, 24, 708, 260, 50, "Torna all'elenco", Bordo, Chiaro, udon, "ChiudiDettaglio");
    }

    private static void CostruisciInfo(Transform t, OculandiaTabletSchermo s)
    {
        GameObject pagina = Vuoto("Pagina info", t);
        s.paginaInfo = pagina;
        Transform p = pagina.transform;

        Pannello("Sfondo", p, 24, 112, 1152, 590, Superficie, false);
        Testo("Titolo", p, 56, 136, 1088, 44, "Oculandia, la community italiana della realtà virtuale", 30, Chiaro, true);
        var testo = Testo("Testo", p, 56, 196, 1088, 380,
            "Eventi, serate, tornei e news VR in italiano.\n\n" +
            "Sito: oculandiavr.it\n" +
            "Calendario: oculandiavr.it/events\n\n" +
            "Come funziona questo tablet\n" +
            "Eventi e news arrivano dal sito. Il tablet li controlla da solo ogni 10 minuti.\n" +
            "Le immagini arrivano una alla volta: ci vuole qualche secondo.\n" +
            "Tocca un evento o una news per leggere tutto.\n\n" +
            "VRChat non apre i link: scrivi l'indirizzo nel browser del telefono o del PC.",
            21, Chiaro, false);
        testo.enableWordWrapping = true;
        s.infoAggiornato = Testo("Aggiornato", p, 56, 640, 1088, 30, "Dati non ancora arrivati.", 18, Azzurro, false);
    }

    // ------------------------------------------------------------------ mattoni

    private static VRCUrl[] Indirizzi(string prefisso, int quanti)
    {
        var elenco = new VRCUrl[quanti];
        for (int i = 0; i < quanti; i++) elenco[i] = new VRCUrl(ImgBase + prefisso + (i + 1) + ".jpg");
        return elenco;
    }

    // Posizione in punti dalla cima a sinistra del genitore, come nell'anteprima.
    private static RectTransform Posiziona(GameObject oggetto, Transform genitore, float x, float y, float l, float a)
    {
        oggetto.transform.SetParent(genitore, false);
        oggetto.layer = genitore.gameObject.layer;
        var rt = oggetto.GetComponent<RectTransform>();
        if (rt == null) rt = oggetto.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(l, a);
        rt.localScale = Vector3.one;
        return rt;
    }

    private static GameObject Vuoto(string nome, Transform genitore)
    {
        RectTransform g = genitore as RectTransform;
        float l = g != null ? g.sizeDelta.x : Larghezza;
        float a = g != null ? g.sizeDelta.y : Altezza;
        return Vuoto(nome, genitore, 0, 0, l, a);
    }

    private static GameObject Vuoto(string nome, Transform genitore, float x, float y, float l, float a)
    {
        var oggetto = new GameObject(nome, typeof(RectTransform));
        Posiziona(oggetto, genitore, x, y, l, a);
        return oggetto;
    }

    private static Image Pannello(string nome, Transform genitore, float x, float y, float l, float a, Color colore, bool tocco)
    {
        var oggetto = new GameObject(nome, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Posiziona(oggetto, genitore, x, y, l, a);
        var immagine = oggetto.GetComponent<Image>();
        immagine.color = colore;
        immagine.raycastTarget = tocco;
        return immagine;
    }

    private static RawImage Riquadro(string nome, Transform genitore, float x, float y, float l, float a)
    {
        var oggetto = new GameObject(nome, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        Posiziona(oggetto, genitore, x, y, l, a);
        var riquadro = oggetto.GetComponent<RawImage>();
        riquadro.color = ColoreVuoto;
        riquadro.raycastTarget = false;
        return riquadro;
    }

    private static TextMeshProUGUI Testo(string nome, Transform genitore, float x, float y, float l, float a,
        string valore, float misura, Color colore, bool grassetto,
        TextAlignmentOptions allineamento = TextAlignmentOptions.TopLeft)
    {
        var oggetto = new GameObject(nome, typeof(RectTransform), typeof(CanvasRenderer));
        Posiziona(oggetto, genitore, x, y, l, a);
        var testo = oggetto.AddComponent<TextMeshProUGUI>();
        testo.text = valore;
        testo.fontSize = misura;
        testo.color = colore;
        testo.fontStyle = grassetto ? FontStyles.Bold : FontStyles.Normal;
        testo.alignment = allineamento;
        testo.enableWordWrapping = false;
        testo.overflowMode = TextOverflowModes.Ellipsis;
        testo.raycastTarget = false;  // un testo non deve coprire i pulsanti
        return testo;
    }

    private static Button Pulsante(string nome, Transform genitore, float x, float y, float l, float a,
        string etichetta, Color sfondo, Color colore, UdonBehaviour udon, string evento)
    {
        Image immagine = Pannello(nome, genitore, x, y, l, a, sfondo, true);
        var pulsante = immagine.gameObject.AddComponent<Button>();
        pulsante.targetGraphic = immagine;
        pulsante.navigation = new Navigation { mode = Navigation.Mode.None };
        var colori = pulsante.colors;
        colori.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
        colori.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        pulsante.colors = colori;
        if (etichetta != "")
            Testo("Etichetta", immagine.transform, 0, 0, l, a, etichetta, 22, colore, true, TextAlignmentOptions.Center);
        // Il pulsante chiama SendCustomEvent sull'UdonBehaviour dello schermo.
        if (udon != null)
            UnityEventTools.AddStringPersistentListener(pulsante.onClick, new UnityAction<string>(udon.SendCustomEvent), evento);
        return pulsante;
    }

    private static GameObject Etichetta(string nome, Transform genitore, float x, float y, float l, float a)
    {
        Image fondo = Pannello(nome, genitore, x, y, l, a, Rosso, false);
        Testo("Testo", fondo.transform, 0, 0, l, a, "IN CORSO", 18, Color.white, true, TextAlignmentOptions.Center);
        fondo.gameObject.SetActive(false);
        return fondo.gameObject;
    }

    // Campo di sola lettura con l'indirizzo intero. Usa il testo classico di Unity:
    // InputField è il campo che Udon conosce di sicuro.
    private static InputField CampoLink(Transform genitore, float x, float y, float l, float a)
    {
        Image fondo = Pannello("Campo link", genitore, x, y, l, a, Fondo, true);
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var oggetto = new GameObject("Testo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        Posiziona(oggetto, fondo.transform, 12, 6, l - 24, a - 12);
        var testo = oggetto.GetComponent<Text>();
        testo.font = font;
        testo.fontSize = 18;
        testo.color = Chiaro;
        testo.alignment = TextAnchor.MiddleLeft;
        testo.supportRichText = false;
        testo.raycastTarget = false;
        var campo = fondo.gameObject.AddComponent<InputField>();
        campo.textComponent = testo;
        campo.targetGraphic = fondo;
        campo.readOnly = true;
        campo.lineType = InputField.LineType.SingleLine;
        campo.navigation = new Navigation { mode = Navigation.Mode.None };
        return campo;
    }

    private static void AggiungiUiShape(GameObject radice)
    {
        // Il nome esatto della classe cambia tra le versioni dell'SDK: la cerco per nome.
        Type tipo = CercaTipo("VRC.SDK3.Components.VRCUiShape") ?? CercaTipo("VRC.SDKBase.VRC_UiShape");
        if (tipo == null)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] tipi;
                try { tipi = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException errore) { tipi = errore.Types; }
                foreach (Type candidato in tipi)
                {
                    if (candidato != null && typeof(Component).IsAssignableFrom(candidato) && !candidato.IsAbstract &&
                        (candidato.Name == "VRCUiShape" || candidato.Name == "VRC_UiShape"))
                    {
                        tipo = candidato;
                        break;
                    }
                }
                if (tipo != null) break;
            }
        }
        if (tipo == null)
        {
            Debug.LogWarning("[Oculandia] Non trovo il componente VRC Ui Shape. Aggiungilo a mano all'oggetto «Tablet Oculandia».");
            return;
        }
        radice.AddComponent(tipo);
    }

    private static void AssicuraEventSystem()
    {
        if (UnityEngine.Object.FindObjectOfType<EventSystem>(true) != null) return;
        var sistema = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Undo.RegisterCreatedObjectUndo(sistema, "EventSystem");
    }

    private static string SalvaPrefab(GameObject radice)
    {
        if (!AssetDatabase.IsValidFolder(CartellaPrefab))
        {
            if (!AssetDatabase.IsValidFolder(Cartella)) AssetDatabase.CreateFolder("Assets", "OculandiaTablet");
            AssetDatabase.CreateFolder(Cartella, "Prefabs");
        }
        string percorso = PercorsoPrefab;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(percorso) != null &&
            !EditorUtility.DisplayDialog("Tablet di Oculandia",
                "Esiste già " + percorso + ".\nLo sostituisco con questo tablet nuovo?", "Sostituisci", "Salva una copia"))
        {
            percorso = AssetDatabase.GenerateUniqueAssetPath(percorso);
        }
        GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(radice, percorso, InteractionMode.UserAction, out bool riuscito);
        if (!riuscito || prefab == null)
        {
            Debug.LogWarning("[Oculandia] Prefab non salvato. Il tablet nella scena funziona lo stesso: trascinalo a mano in " + CartellaPrefab + ".");
            return "";
        }
        return percorso;
    }

    // ------------------------------------------------------------------ controllo

    // Conta e scrive nella Console ogni riferimento mancante. 0 vuol dire tutto a posto.
    private static int Controlla(OculandiaTabletSchermo schermo)
    {
        if (schermo == null)
        {
            Debug.LogWarning("[Oculandia] Schermo non trovato.");
            return 1;
        }
        string nome = "«" + schermo.transform.root.name + "»";
        var problemi = new List<string>();

        ControllaCampi(schermo, "Schermo", problemi);
        OculandiaTabletFeed feed = schermo.feed;
        OculandiaTabletImmagini immagini = schermo.immagini;
        if (feed != null)
        {
            ControllaCampi(feed, "Feed", problemi);
            if (feed.feedUrl == null || string.IsNullOrEmpty(feed.feedUrl.Get()))
                problemi.Add("Feed: manca feedUrl. Scrivi " + FeedUrl);
            else if (feed.feedUrl.Get() != FeedUrl)
                Debug.Log("[Oculandia] " + nome + " Feed: indirizzo diverso dal solito: " + feed.feedUrl.Get());
            if (feed.schermo != schermo) problemi.Add("Feed: il campo schermo non punta a questo schermo.");
        }
        if (immagini != null)
        {
            ControllaCampi(immagini, "Immagini", problemi);
            ControllaIndirizzi(immagini.urlEventi, "evento-", CaselleEventi, problemi);
            ControllaIndirizzi(immagini.urlNews, "news-", CaselleNews, problemi);
            if (immagini.schermo != schermo) problemi.Add("Immagini: il campo schermo non punta a questo schermo.");
        }
        int righe = schermo.righe != null ? schermo.righe.Length : 0;
        if (righe > 6) problemi.Add("Schermo: al massimo 6 righe (esistono solo Riga0 … Riga5).");

        // La tela.
        Canvas tela = schermo.GetComponentInParent<Canvas>(true);
        if (tela == null) problemi.Add("Lo schermo non sta dentro una Canvas.");
        else
        {
            if (tela.renderMode != RenderMode.WorldSpace) problemi.Add("Canvas: Render Mode deve essere World Space.");
            if (tela.gameObject.layer == LayerMask.NameToLayer("UI")) problemi.Add("Canvas: è sul livello UI. Mettila su Default, altrimenti non si può toccare.");
            bool forma = false;
            foreach (Component c in tela.GetComponents<Component>())
                if (c != null && (c.GetType().Name == "VRCUiShape" || c.GetType().Name == "VRC_UiShape")) forma = true;
            if (!forma) problemi.Add("Canvas: manca il componente VRC Ui Shape.");
            if (tela.GetComponent<GraphicRaycaster>() == null) problemi.Add("Canvas: manca Graphic Raycaster.");

            foreach (Button pulsante in tela.GetComponentsInChildren<Button>(true))
            {
                int collegati = 0;
                for (int i = 0; i < pulsante.onClick.GetPersistentEventCount(); i++)
                    if (pulsante.onClick.GetPersistentTarget(i) is UdonBehaviour && pulsante.onClick.GetPersistentMethodName(i) == "SendCustomEvent") collegati++;
                if (collegati == 0) problemi.Add("Pulsante «" + pulsante.name + "»: non chiama nessuno script.");
                if (pulsante.navigation.mode != Navigation.Mode.None) problemi.Add("Pulsante «" + pulsante.name + "»: Navigation deve essere None.");
            }
        }

        foreach (string problema in problemi) Debug.LogWarning("[Oculandia] " + nome + " " + problema, schermo);
        if (problemi.Count == 0) Debug.Log("[Oculandia] " + nome + ": collegamenti a posto.", schermo);
        return problemi.Count;
    }

    // Ogni campo pubblico che è un riferimento o un elenco non deve essere vuoto.
    private static void ControllaCampi(Component componente, string nome, List<string> problemi)
    {
        foreach (FieldInfo campo in componente.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            object valore = campo.GetValue(componente);
            bool facoltativo = campo.Name == "dettaglioCampoLink";
            if (typeof(UnityEngine.Object).IsAssignableFrom(campo.FieldType))
            {
                if (valore as UnityEngine.Object == null && !facoltativo) problemi.Add(nome + ": manca «" + campo.Name + "».");
            }
            else if (campo.FieldType.IsArray && typeof(UnityEngine.Object).IsAssignableFrom(campo.FieldType.GetElementType()))
            {
                var elenco = valore as Array;
                if (elenco == null || elenco.Length == 0) { problemi.Add(nome + ": l'elenco «" + campo.Name + "» è vuoto."); continue; }
                for (int i = 0; i < elenco.Length; i++)
                    if (elenco.GetValue(i) as UnityEngine.Object == null) problemi.Add(nome + ": «" + campo.Name + "» elemento " + i + " vuoto.");
            }
        }
    }

    private static void ControllaIndirizzi(VRCUrl[] elenco, string prefisso, int quanti, List<string> problemi)
    {
        if (elenco == null || elenco.Length != quanti)
        {
            problemi.Add("Immagini: servono " + quanti + " indirizzi " + prefisso + "N.jpg.");
            return;
        }
        for (int i = 0; i < quanti; i++)
        {
            string atteso = prefisso + (i + 1) + ".jpg";
            string valore = elenco[i] != null ? elenco[i].Get() : "";
            if (string.IsNullOrEmpty(valore) || !valore.EndsWith("/" + atteso))
                problemi.Add("Immagini: la casella " + (i + 1) + " deve finire con " + atteso + " (ora: «" + valore + "»).");
        }
    }

    private static Color Hex(string esadecimale)
    {
        ColorUtility.TryParseHtmlString("#" + esadecimale, out Color colore);
        return colore;
    }
}
