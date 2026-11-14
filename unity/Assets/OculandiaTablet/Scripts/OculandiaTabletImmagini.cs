// Tablet di Oculandia, parte 2 di 3: le immagini.
// Il mondo può scaricare solo indirizzi scritti nell'editor: per questo ci sono
// caselle fisse (evento-1.jpg … evento-12.jpg, news-1.jpg … news-10.jpg).
// Il feed dice quale casella usa ogni voce e con quale versione.
//
// VRChat consente una immagine ogni 5 secondi in tutto il mondo: qui c'è una coda
// semplice che ne scarica una alla volta, solo quelle che servono a schermo.
//
// Scritto dalla documentazione di VRChat: vedi docs/da-verificare-in-unity.md.

using UdonSharp;
using UnityEngine;
using VRC.SDK3.Image;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class OculandiaTabletImmagini : UdonSharpBehaviour
{
    [Tooltip("Indirizzi fissi delle locandine: evento-1.jpg, evento-2.jpg… nello stesso ordine.")]
    public VRCUrl[] urlEventi;

    [Tooltip("Indirizzi fissi delle copertine: news-1.jpg, news-2.jpg… nello stesso ordine.")]
    public VRCUrl[] urlNews;

    [Tooltip("Lo schermo da avvisare quando arriva un'immagine.")]
    public OculandiaTabletSchermo schermo;

    private const float Pausa = 5.5f;      // poco più del limite di VRChat
    private const float Scadenza = 30f;    // se dopo 30 secondi non arriva niente, passo alla prossima

    private VRCImageDownloader _scaricatore;
    private TextureInfo _opzioni;
    private Texture2D[] _texture;   // una per casella: prima gli eventi, poi le news
    private object[] _download;     // lo scaricamento che possiede la texture: serve per liberarla
    private string[] _caricate;     // versione dell'immagine che ho in memoria
    private string[] _volute;       // versione chiesta dallo schermo
    private int[] _coda;
    private int _inCoda;
    private int _inArrivo = -1;
    private float _ultimoAvvio = -100f;
    private bool _giro;
    private bool _pronto;

    private void Prepara()
    {
        if (_pronto) return;
        _pronto = true;
        int totale = Lunghezza(urlEventi) + Lunghezza(urlNews);
        _texture = new Texture2D[totale];
        _download = new object[totale];
        _caricate = new string[totale];
        _volute = new string[totale];
        _coda = new int[totale];
        // Va tenuto in una variabile: altrimenti VRChat lo butta via e le immagini spariscono.
        _scaricatore = new VRCImageDownloader();
        _opzioni = new TextureInfo();
        _opzioni.GenerateMipMaps = true;
        _opzioni.WrapModeU = TextureWrapMode.Clamp;
        _opzioni.WrapModeV = TextureWrapMode.Clamp;
    }

    // Chiamato dallo schermo per ogni immagine visibile. tipo: 0 = eventi, 1 = news.
    // Restituisce l'immagine se è già in memoria con quella versione, altrimenti
    // la mette in coda e restituisce null: lo schermo verrà avvisato quando arriva.
    public Texture2D Chiedi(int tipo, int casella, string versione)
    {
        Prepara();
        int indice = Indice(tipo, casella);
        if (indice < 0) return null;
        if (_texture[indice] != null && _caricate[indice] == versione) return _texture[indice];
        _volute[indice] = versione;
        Accoda(indice);
        return null;
    }

    // Come Chiedi, ma non mette niente in coda. Serve quando arriva un'immagine
    // e lo schermo ridisegna: così un'immagine rotta non viene richiesta in continuo.
    public Texture2D Pronta(int tipo, int casella, string versione)
    {
        Prepara();
        int indice = Indice(tipo, casella);
        if (indice < 0) return null;
        if (_texture[indice] != null && _caricate[indice] == versione) return _texture[indice];
        return null;
    }

    // Lo schermo svuota la coda prima di chiedere le immagini della pagina nuova:
    // così chi sfoglia in fretta non aspetta le immagini delle pagine già passate.
    public void SvuotaCoda()
    {
        _inCoda = 0;
    }

    private void Accoda(int indice)
    {
        if (indice == _inArrivo) return;
        for (int i = 0; i < _inCoda; i++)
        {
            if (_coda[i] == indice) return;
        }
        if (_inCoda >= _coda.Length) return;
        _coda[_inCoda] = indice;
        _inCoda++;
        if (!_giro)
        {
            _giro = true;
            Avanza();
        }
    }

    // Scarica la prossima immagine in coda, una alla volta, rispettando la pausa.
    public void Avanza()
    {
        if (_inArrivo >= 0)
        {
            if (Time.time - _ultimoAvvio < Scadenza)
            {
                SendCustomEventDelayedSeconds(nameof(Avanza), 1f);
                return;
            }
            _inArrivo = -1;
        }
        if (_inCoda == 0)
        {
            _giro = false;
            return;
        }
        float attesa = _ultimoAvvio + Pausa - Time.time;
        if (attesa > 0f)
        {
            SendCustomEventDelayedSeconds(nameof(Avanza), attesa);
            return;
        }
        int indice = _coda[0];
        _inCoda--;
        for (int i = 0; i < _inCoda; i++) _coda[i] = _coda[i + 1];

        VRCUrl indirizzo = Indirizzo(indice);
        if (indirizzo != null && !string.IsNullOrEmpty(indirizzo.Get()))
        {
            _inArrivo = indice;
            _ultimoAvvio = Time.time;
            _scaricatore.DownloadImage(indirizzo, null, (IUdonEventReceiver)this, _opzioni);
        }
        SendCustomEventDelayedSeconds(nameof(Avanza), Pausa);
    }

    public override void OnImageLoadSuccess(IVRCImageDownload result)
    {
        int indice = CasellaDi(result.Url);
        if (indice == _inArrivo) _inArrivo = -1;
        if (indice < 0) return;
        // VRChat chiede di liberare la vecchia immagine prima di sostituirla:
        // altrimenti in una serata lunga la memoria del visore si riempie.
        if (_download[indice] != null) ((IVRCImageDownload)_download[indice]).Dispose();
        _download[indice] = result;
        _texture[indice] = result.Result;
        _caricate[indice] = _volute[indice];
        if (schermo != null) schermo.ImmaginePronta();
    }

    public override void OnImageLoadError(IVRCImageDownload result)
    {
        int indice = CasellaDi(result.Url);
        if (indice == _inArrivo) _inArrivo = -1;
        // Resta il riquadro scuro. Si riprova alla prossima volta che la riga torna a schermo.
        Debug.LogWarning("[OculandiaTablet] Immagine non caricata: " + result.Error.ToString() + " " + result.ErrorMessage);
    }

    private void OnDestroy()
    {
        if (_scaricatore != null) _scaricatore.Dispose();
    }

    // ------------------------------------------------------------------

    private int Lunghezza(VRCUrl[] elenco)
    {
        return elenco == null ? 0 : elenco.Length;
    }

    private int Indice(int tipo, int casella)
    {
        int eventi = Lunghezza(urlEventi);
        if (tipo == 0) return casella >= 1 && casella <= eventi ? casella - 1 : -1;
        return casella >= 1 && casella <= Lunghezza(urlNews) ? eventi + casella - 1 : -1;
    }

    private VRCUrl Indirizzo(int indice)
    {
        int eventi = Lunghezza(urlEventi);
        return indice < eventi ? urlEventi[indice] : urlNews[indice - eventi];
    }

    // Ritrova la casella dall'indirizzo: così una risposta in ritardo non finisce nel posto sbagliato.
    private int CasellaDi(VRCUrl indirizzo)
    {
        if (indirizzo == null || !_pronto) return -1;
        string cercato = indirizzo.Get();
        for (int i = 0; i < _texture.Length; i++)
        {
            VRCUrl mio = Indirizzo(i);
            if (mio != null && mio.Get() == cercato) return i;
        }
        return -1;
    }
}
