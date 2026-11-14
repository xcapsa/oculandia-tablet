// Tablet di Oculandia: scarica il feed del sito e riempie le righe di eventi e news.
//
// Richiede VRChat Worlds SDK 3.x con UdonSharp, TextMeshPro e VRCJson (VRC.SDK3.Data).
// Scritto leggendo la documentazione: NON è stato ancora compilato né provato in Unity.
// Vedi docs/unity-setup.md per i passi di prova.

using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class OculandiaTabletFeed : UdonSharpBehaviour
{
    [Header("Feed")]
    [Tooltip("Indirizzo del file feed/tablet.json. Va scritto qui nell'editor: il mondo non può comporlo mentre gira.")]
    public VRCUrl feedUrl;

    [Tooltip("Ogni quanti minuti riscaricare. VRChat consente al massimo una richiesta ogni 5 secondi in tutto il mondo.")]
    [Range(2, 60)]
    public int refreshMinutes = 10;

    [Header("Righe eventi (stessa lunghezza)")]
    public TextMeshProUGUI[] eventTitles;
    public TextMeshProUGUI[] eventWhen;
    public TextMeshProUGUI[] eventWhere;
    public GameObject[] eventRows;

    [Header("Righe news (stessa lunghezza)")]
    public TextMeshProUGUI[] newsTitles;
    public TextMeshProUGUI[] newsDates;
    public TextMeshProUGUI[] newsLines;
    public GameObject[] newsRows;

    [Header("Stato")]
    public TextMeshProUGUI statusText;

    private const int SupportedVersion = 1;
    private bool _loading;
    private bool _hasData;

    private void Start()
    {
        SetStatus("Carico eventi e news…");
        Load();
    }

    // Si può collegare anche a un pulsante "Aggiorna" del tablet.
    public void Load()
    {
        if (_loading) return;
        if (feedUrl == null || string.IsNullOrEmpty(feedUrl.Get()))
        {
            SetStatus("Manca l'indirizzo del feed.");
            return;
        }
        _loading = true;
        VRCStringDownloader.LoadUrl(feedUrl, (IUdonEventReceiver)this);
    }

    public override void OnStringLoadSuccess(IVRCStringDownload result)
    {
        _loading = false;
        if (Apply(result.Result))
        {
            _hasData = true;
        }
        else if (!_hasData)
        {
            SetStatus("Il sito ha risposto in modo inatteso. Riprovo tra poco.");
        }
        ScheduleNext();
    }

    public override void OnStringLoadError(IVRCStringDownload result)
    {
        _loading = false;
        // Se abbiamo già dei dati restano a schermo: meglio una bacheca di dieci minuti fa che vuota.
        if (!_hasData)
        {
            SetStatus("Non riesco a leggere il sito. Se l'indirizzo non è tra quelli fidati, attiva «Allow Untrusted URLs» nelle impostazioni di VRChat.");
        }
        ScheduleNext();
    }

    private void ScheduleNext()
    {
        SendCustomEventDelayedSeconds(nameof(Load), Mathf.Max(2, refreshMinutes) * 60f);
    }

    private bool Apply(string json)
    {
        if (string.IsNullOrEmpty(json)) return false;
        if (!VRCJson.TryDeserializeFromJson(json, out DataToken root)) return false;
        if (root.TokenType != TokenType.DataDictionary) return false;
        DataDictionary feed = root.DataDictionary;

        // I numeri JSON arrivano come Double.
        if (!feed.TryGetValue("v", TokenType.Double, out DataToken version)) return false;
        if ((int)version.Double != SupportedVersion) return false;

        if (feed.TryGetValue("eventi", TokenType.DataList, out DataToken events))
        {
            FillEvents(events.DataList);
        }
        if (feed.TryGetValue("news", TokenType.DataList, out DataToken news))
        {
            FillNews(news.DataList);
        }
        SetStatus("Aggiornato: " + Text(feed, "aggiornato"));
        return true;
    }

    private void FillEvents(DataList list)
    {
        int rows = eventTitles == null ? 0 : eventTitles.Length;
        for (int i = 0; i < rows; i++)
        {
            bool has = i < list.Count && list[i].TokenType == TokenType.DataDictionary;
            Show(eventRows, i, has);
            if (!has) continue;
            DataDictionary item = list[i].DataDictionary;
            string title = Text(item, "titolo");
            if (Flag(item, "in_corso")) title = "● " + title;
            Set(eventTitles, i, title);
            Set(eventWhen, i, Text(item, "tipo") + " · " + Text(item, "quando"));
            Set(eventWhere, i, Text(item, "dove"));
        }
    }

    private void FillNews(DataList list)
    {
        int rows = newsTitles == null ? 0 : newsTitles.Length;
        for (int i = 0; i < rows; i++)
        {
            bool has = i < list.Count && list[i].TokenType == TokenType.DataDictionary;
            Show(newsRows, i, has);
            if (!has) continue;
            DataDictionary item = list[i].DataDictionary;
            Set(newsTitles, i, Text(item, "titolo"));
            Set(newsDates, i, Text(item, "data"));
            Set(newsLines, i, Text(item, "riga"));
        }
    }

    private string Text(DataDictionary item, string key)
    {
        if (item.TryGetValue(key, TokenType.String, out DataToken value)) return value.String;
        return "";
    }

    private bool Flag(DataDictionary item, string key)
    {
        if (item.TryGetValue(key, TokenType.Boolean, out DataToken value)) return value.Boolean;
        return false;
    }

    private void Set(TextMeshProUGUI[] labels, int index, string text)
    {
        if (labels == null || index >= labels.Length || labels[index] == null) return;
        labels[index].text = text;
    }

    private void Show(GameObject[] rows, int index, bool visible)
    {
        if (rows == null || index >= rows.Length || rows[index] == null) return;
        rows[index].SetActive(visible);
    }

    private void SetStatus(string text)
    {
        if (statusText != null) statusText.text = text;
    }
}
