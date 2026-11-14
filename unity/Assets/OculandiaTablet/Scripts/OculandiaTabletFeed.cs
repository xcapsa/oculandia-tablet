// Tablet di Oculandia, parte 1 di 3: i dati.
// Scarica feed/tablet.json, lo legge e lo tiene in memoria per lo schermo.
// Si riaggiorna da solo e riprova se qualcosa va storto.
//
// Tutto è locale al giocatore: niente sincronizzazione.
// Scritto dalla documentazione di VRChat: vedi docs/da-verificare-in-unity.md.

using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class OculandiaTabletFeed : UdonSharpBehaviour
{
    [Tooltip("Indirizzo di feed/tablet.json. Va scritto qui nell'editor: il mondo non può comporlo mentre gira.")]
    public VRCUrl feedUrl;

    [Tooltip("Ogni quanti minuti riscaricare il feed.")]
    [Range(2, 60)]
    public int minutiAggiornamento = 10;

    [Tooltip("Lo schermo da avvisare quando arrivano i dati.")]
    public OculandiaTabletSchermo schermo;

    private const int VersioneNota = 1;
    private const float Passo = 5f;            // ogni quanti secondi controllo se è ora di scaricare
    private const float PausaMinima = 10f;     // VRChat consente una richiesta di testo ogni 5 secondi: resto largo

    private DataList _eventi;
    private DataList _news;
    private string _aggiornato = "";
    private bool _scaricando;
    private bool _haDati;
    private int _falliti;
    private float _prossimo;
    private float _ultima = -100f;

    private void Start()
    {
        Stato("Carico eventi e news…");
        Tic();
    }

    // Un solo orologio per tutto: così non restano richieste doppie in giro.
    public void Tic()
    {
        // Se VRChat non ha mai risposto, dopo un minuto mi considero libero.
        if (_scaricando && Time.time - _ultima > 60f) _scaricando = false;
        if (!_scaricando && Time.time >= _prossimo) Scarica();
        SendCustomEventDelayedSeconds(nameof(Tic), Passo);
    }

    // Collegato al pulsante «Aggiorna».
    public void AggiornaOra()
    {
        if (_scaricando)
        {
            Stato("Sto già caricando…");
            return;
        }
        if (Time.time - _ultima < PausaMinima)
        {
            Stato("Appena controllato. Riprova tra qualche secondo.");
            return;
        }
        Stato("Carico eventi e news…");
        Scarica();
    }

    private void Scarica()
    {
        if (feedUrl == null || string.IsNullOrEmpty(feedUrl.Get()))
        {
            Stato("Manca l'indirizzo del feed. In Unity usa il menu Oculandia, Controlla collegamenti.");
            _prossimo = Time.time + 60f;
            return;
        }
        _scaricando = true;
        _ultima = Time.time;
        VRCStringDownloader.LoadUrl(feedUrl, (IUdonEventReceiver)this);
    }

    public override void OnStringLoadSuccess(IVRCStringDownload result)
    {
        _scaricando = false;
        if (!Leggi(result.Result))
        {
            Fallito("Il feed ha un formato che non conosco.", "");
            return;
        }
        _haDati = true;
        _falliti = 0;
        _prossimo = Time.time + Mathf.Max(2, minutiAggiornamento) * 60f;
        Stato("Dati del " + _aggiornato);
        if (schermo != null) schermo.DatiPronti();
    }

    public override void OnStringLoadError(IVRCStringDownload result)
    {
        _scaricando = false;
        Fallito("Non riesco a leggere il feed (errore " + result.ErrorCode + ").", result.Error);
    }

    // Le prime tre volte riprovo presto (20, 40, 60 secondi), poi torno al ritmo normale.
    private void Fallito(string perche, string dettaglio)
    {
        _falliti++;
        float attesa = _falliti <= 3 ? 20f * _falliti : Mathf.Max(2, minutiAggiornamento) * 60f;
        _prossimo = Time.time + attesa;
        Debug.LogWarning("[OculandiaTablet] " + perche + " " + dettaglio);
        if (_haDati)
        {
            // Meglio una bacheca di dieci minuti fa che una vuota.
            Stato("Dati del " + _aggiornato + ". Ultimo controllo non riuscito.");
        }
        else
        {
            Stato(perche + " Riprovo tra " + (int)attesa + " secondi.");
        }
    }

    private bool Leggi(string json)
    {
        if (string.IsNullOrEmpty(json)) return false;
        if (!VRCJson.TryDeserializeFromJson(json, out DataToken radice)) return false;
        if (radice.TokenType != TokenType.DataDictionary) return false;
        DataDictionary feed = radice.DataDictionary;

        // I numeri JSON arrivano sempre come Double.
        if (!feed.TryGetValue("v", TokenType.Double, out DataToken versione)) return false;
        if ((int)versione.Double != VersioneNota) return false;
        if (!feed.TryGetValue("eventi", TokenType.DataList, out DataToken eventi)) return false;
        if (!feed.TryGetValue("news", TokenType.DataList, out DataToken news)) return false;

        _eventi = eventi.DataList;
        _news = news.DataList;
        if (feed.TryGetValue("aggiornato", TokenType.String, out DataToken quando)) _aggiornato = quando.String;
        return true;
    }

    // ------------------------------------------------------------------
    // Letture per lo schermo. tipo: 0 = eventi, 1 = news.

    public bool HaDati()
    {
        return _haDati;
    }

    public string Aggiornato()
    {
        return _aggiornato;
    }

    public int Conta(int tipo)
    {
        DataList lista = tipo == 0 ? _eventi : _news;
        return lista == null ? 0 : lista.Count;
    }

    public string Campo(int tipo, int indice, string chiave)
    {
        DataList lista = tipo == 0 ? _eventi : _news;
        if (lista == null || indice < 0 || indice >= lista.Count) return "";
        if (!lista.TryGetValue(indice, TokenType.DataDictionary, out DataToken voce)) return "";
        if (voce.DataDictionary.TryGetValue(chiave, TokenType.String, out DataToken valore)) return valore.String;
        return "";
    }

    public bool Vero(int tipo, int indice, string chiave)
    {
        DataList lista = tipo == 0 ? _eventi : _news;
        if (lista == null || indice < 0 || indice >= lista.Count) return false;
        if (!lista.TryGetValue(indice, TokenType.DataDictionary, out DataToken voce)) return false;
        if (voce.DataDictionary.TryGetValue(chiave, TokenType.Boolean, out DataToken valore)) return valore.Boolean;
        return false;
    }

    public int Numero(int tipo, int indice, string chiave)
    {
        DataList lista = tipo == 0 ? _eventi : _news;
        if (lista == null || indice < 0 || indice >= lista.Count) return 0;
        if (!lista.TryGetValue(indice, TokenType.DataDictionary, out DataToken voce)) return 0;
        if (voce.DataDictionary.TryGetValue(chiave, TokenType.Double, out DataToken valore)) return (int)valore.Double;
        return 0;
    }

    private void Stato(string testo)
    {
        if (schermo != null) schermo.Stato(testo);
    }
}
