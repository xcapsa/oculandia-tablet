// Tablet di Oculandia, parte 3 di 3: lo schermo.
// Tre schede (Eventi, News, Info), pagine avanti e indietro, una pagina di dettaglio.
// I pulsanti chiamano i metodi pubblici qui sotto con SendCustomEvent.
//
// Tutti i collegamenti li fa il menu Oculandia, Crea tablet: non serve trascinare niente.
// Tutto è locale al giocatore: chi sfoglia non cambia la pagina agli altri.
// Scritto dalla documentazione di VRChat: vedi docs/da-verificare-in-unity.md.

using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class OculandiaTabletSchermo : UdonSharpBehaviour
{
    [Header("Altri pezzi del tablet")]
    public OculandiaTabletFeed feed;
    public OculandiaTabletImmagini immagini;

    [Header("Schede: 0 Eventi, 1 News, 2 Info")]
    public Image[] sfondiSchede;
    public TextMeshProUGUI[] testiSchede;
    public GameObject paginaElenco;
    public GameObject paginaDettaglio;
    public GameObject paginaInfo;

    [Header("Righe dell'elenco (stessa lunghezza per tutti)")]
    public GameObject[] righe;
    public GameObject[] vistaEvento;
    public RawImage[] locandine;
    public TextMeshProUGUI[] titoliEvento;
    public TextMeshProUGUI[] quandoEvento;
    public TextMeshProUGUI[] tipoEvento;
    public GameObject[] badgeInCorso;
    public GameObject[] vistaNews;
    public RawImage[] copertine;
    public TextMeshProUGUI[] titoliNews;
    public TextMeshProUGUI[] dateNews;
    public TextMeshProUGUI[] righeNews;

    [Header("Piede")]
    public TextMeshProUGUI testoPagina;
    public GameObject pulsanteIndietro;
    public GameObject pulsanteAvanti;
    public TextMeshProUGUI testoStato;
    public TextMeshProUGUI testoVuoto;

    [Header("Dettaglio")]
    public RawImage dettaglioLocandina;
    public RawImage dettaglioCopertina;
    public TextMeshProUGUI dettaglioTitolo;
    public TextMeshProUGUI dettaglioQuando;
    public TextMeshProUGUI dettaglioTesto;
    public TextMeshProUGUI dettaglioLink;
    [Tooltip("Facoltativo. Campo di sola lettura con l'indirizzo intero: da PC si può selezionare e copiare.")]
    public InputField dettaglioCampoLink;
    public GameObject dettaglioInCorso;

    [Header("Info")]
    public TextMeshProUGUI infoAggiornato;

    private int _scheda;          // 0 eventi, 1 news, 2 info
    private int _pagina;
    private int _dettaglio = -1;  // indice della voce aperta, -1 se nessuna
    private bool _colori;
    private Color _schedaAttiva;
    private Color _schedaSpenta;
    private Color _testoAttivo;
    private Color _testoSpento;
    private Color _vuoto;

    private void Start()
    {
        Disegna();
    }

    // ------------------------------------------------------------------
    // Pulsanti

    public void SchedaEventi() { ApriScheda(0); }
    public void SchedaNews() { ApriScheda(1); }
    public void SchedaInfo() { ApriScheda(2); }

    public void Avanti()
    {
        if (_pagina + 1 < Pagine()) _pagina++;
        Disegna();
    }

    public void Indietro()
    {
        if (_pagina > 0) _pagina--;
        Disegna();
    }

    // Un metodo per riga: SendCustomEvent non passa parametri.
    public void Riga0() { ApriDettaglio(0); }
    public void Riga1() { ApriDettaglio(1); }
    public void Riga2() { ApriDettaglio(2); }
    public void Riga3() { ApriDettaglio(3); }
    public void Riga4() { ApriDettaglio(4); }
    public void Riga5() { ApriDettaglio(5); }

    public void ChiudiDettaglio()
    {
        _dettaglio = -1;
        Disegna();
    }

    public void Aggiorna()
    {
        if (feed != null) feed.AggiornaOra();
    }

    // ------------------------------------------------------------------
    // Chiamati dagli altri script

    public void Stato(string testo)
    {
        if (testoStato != null) testoStato.text = testo;
    }

    public void DatiPronti()
    {
        if (_dettaglio >= Conta()) _dettaglio = -1;
        if (_pagina >= Pagine()) _pagina = Pagine() - 1;
        Disegna();
    }

    // Arriva un'immagine: ridisegno solo le immagini, senza chiederne altre.
    public void ImmaginePronta()
    {
        if (_scheda == 2 || feed == null || immagini == null) return;
        if (_dettaglio >= 0)
        {
            Texture2D t = immagini.Pronta(_scheda, feed.Numero(_scheda, _dettaglio, "img"), feed.Campo(_scheda, _dettaglio, "img_v"));
            MettiImmagine(_scheda == 0 ? dettaglioLocandina : dettaglioCopertina, t);
            return;
        }
        int per = Righe();
        for (int r = 0; r < per; r++)
        {
            int i = _pagina * per + r;
            if (i >= Conta()) break;
            Texture2D t = immagini.Pronta(_scheda, feed.Numero(_scheda, i, "img"), feed.Campo(_scheda, i, "img_v"));
            MettiImmagine(_scheda == 0 ? Riquadro(locandine, r) : Riquadro(copertine, r), t);
        }
    }

    // ------------------------------------------------------------------
    // Disegno

    private void ApriScheda(int scheda)
    {
        _scheda = scheda;
        _pagina = 0;
        _dettaglio = -1;
        Disegna();
    }

    private void ApriDettaglio(int riga)
    {
        int i = _pagina * Righe() + riga;
        if (i >= Conta()) return;
        _dettaglio = i;
        Disegna();
    }

    private void Disegna()
    {
        Colori();
        for (int s = 0; s < 3; s++)
        {
            if (sfondiSchede != null && s < sfondiSchede.Length && sfondiSchede[s] != null)
                sfondiSchede[s].color = s == _scheda ? _schedaAttiva : _schedaSpenta;
            if (testiSchede != null && s < testiSchede.Length && testiSchede[s] != null)
                testiSchede[s].color = s == _scheda ? _testoAttivo : _testoSpento;
        }
        bool info = _scheda == 2;
        bool dettaglio = !info && _dettaglio >= 0;
        Attiva(paginaInfo, info);
        Attiva(paginaDettaglio, dettaglio);
        Attiva(paginaElenco, !info && !dettaglio);

        if (info) DisegnaInfo();
        else if (dettaglio) DisegnaDettaglio();
        else DisegnaElenco();
    }

    private void DisegnaElenco()
    {
        int n = Conta();
        int per = Righe();
        if (immagini != null) immagini.SvuotaCoda();

        for (int r = 0; r < per; r++)
        {
            int i = _pagina * per + r;
            bool c = i < n;
            Attiva(Oggetto(righe, r), c);
            if (!c) continue;
            bool evento = _scheda == 0;
            Attiva(Oggetto(vistaEvento, r), evento);
            Attiva(Oggetto(vistaNews, r), !evento);
            if (evento)
            {
                Scrivi(Testo(titoliEvento, r), feed.Campo(0, i, "titolo"));
                Scrivi(Testo(quandoEvento, r), feed.Campo(0, i, "quando"));
                Scrivi(Testo(tipoEvento, r), Unisci(feed.Campo(0, i, "tipo"), feed.Campo(0, i, "dove")));
                Attiva(Oggetto(badgeInCorso, r), feed.Vero(0, i, "in_corso"));
            }
            else
            {
                Scrivi(Testo(titoliNews, r), feed.Campo(1, i, "titolo"));
                Scrivi(Testo(dateNews, r), Unisci(feed.Campo(1, i, "data"), feed.Campo(1, i, "cat")));
                Scrivi(Testo(righeNews, r), feed.Campo(1, i, "riga"));
            }
            Texture2D t = null;
            if (immagini != null) t = immagini.Chiedi(_scheda, feed.Numero(_scheda, i, "img"), feed.Campo(_scheda, i, "img_v"));
            MettiImmagine(evento ? Riquadro(locandine, r) : Riquadro(copertine, r), t);
        }

        int pagine = Pagine();
        Scrivi(testoPagina, "Pagina " + (_pagina + 1) + " di " + pagine);
        Attiva(pulsanteIndietro, _pagina > 0);
        Attiva(pulsanteAvanti, _pagina + 1 < pagine);

        string vuoto = "";
        if (feed == null || !feed.HaDati()) vuoto = "Carico eventi e news…";
        else if (n == 0) vuoto = _scheda == 0 ? "Nessun evento in programma. Guarda su oculandiavr.it" : "Nessuna news per ora.";
        Scrivi(testoVuoto, vuoto);
        if (testoVuoto != null) testoVuoto.gameObject.SetActive(vuoto != "");
    }

    private void DisegnaDettaglio()
    {
        int i = _dettaglio;
        bool evento = _scheda == 0;
        Scrivi(dettaglioTitolo, feed.Campo(_scheda, i, "titolo"));
        if (evento)
        {
            Scrivi(dettaglioQuando, Unisci(feed.Campo(0, i, "quando"), Unisci(feed.Campo(0, i, "tipo"), feed.Campo(0, i, "dove"))));
        }
        else
        {
            Scrivi(dettaglioQuando, Unisci(feed.Campo(1, i, "data"), feed.Campo(1, i, "cat")));
        }
        Scrivi(dettaglioTesto, feed.Campo(_scheda, i, "testo"));
        Scrivi(dettaglioLink, feed.Campo(_scheda, i, "link_corto"));
        if (dettaglioCampoLink != null) dettaglioCampoLink.text = feed.Campo(_scheda, i, "link");
        Attiva(dettaglioInCorso, evento && feed.Vero(0, i, "in_corso"));

        if (dettaglioLocandina != null) dettaglioLocandina.gameObject.SetActive(evento);
        if (dettaglioCopertina != null) dettaglioCopertina.gameObject.SetActive(!evento);
        if (immagini != null) immagini.SvuotaCoda();
        Texture2D t = null;
        if (immagini != null) t = immagini.Chiedi(_scheda, feed.Numero(_scheda, i, "img"), feed.Campo(_scheda, i, "img_v"));
        MettiImmagine(evento ? dettaglioLocandina : dettaglioCopertina, t);
    }

    private void DisegnaInfo()
    {
        if (feed != null && feed.HaDati()) Scrivi(infoAggiornato, "Dati del " + feed.Aggiornato());
        else Scrivi(infoAggiornato, "Dati non ancora arrivati.");
    }

    // ------------------------------------------------------------------
    // Piccoli aiuti. Nomi diversi e non sovraccarichi: più sicuro per UdonSharp.

    private void Colori()
    {
        if (_colori) return;
        _colori = true;
        _schedaAttiva = new Color(0.549f, 0.898f, 0.588f);   // #8ce596
        _schedaSpenta = new Color(0.063f, 0.125f, 0.086f);   // #102016
        _testoAttivo = new Color(0.055f, 0.090f, 0.071f);    // #0e1712
        _testoSpento = new Color(0.933f, 0.965f, 0.941f);    // #eef6f0
        _vuoto = new Color(0.086f, 0.165f, 0.114f);          // riquadro scuro finché l'immagine non arriva
    }

    private int Conta()
    {
        if (feed == null || _scheda == 2) return 0;
        return feed.Conta(_scheda);
    }

    private int Righe()
    {
        return righe == null || righe.Length == 0 ? 1 : righe.Length;
    }

    private int Pagine()
    {
        int n = Conta();
        int per = Righe();
        return n <= 0 ? 1 : (n + per - 1) / per;
    }

    private string Unisci(string a, string b)
    {
        if (a == "") return b;
        if (b == "") return a;
        return a + " · " + b;
    }

    private void MettiImmagine(RawImage riquadro, Texture2D texture)
    {
        if (riquadro == null) return;
        Colori();
        riquadro.texture = texture;
        riquadro.color = texture != null ? Color.white : _vuoto;
    }

    private void Scrivi(TextMeshProUGUI testo, string valore)
    {
        if (testo != null) testo.text = valore;
    }

    private void Attiva(GameObject oggetto, bool acceso)
    {
        if (oggetto != null && oggetto.activeSelf != acceso) oggetto.SetActive(acceso);
    }

    private GameObject Oggetto(GameObject[] elenco, int i)
    {
        return elenco != null && i < elenco.Length ? elenco[i] : null;
    }

    private RawImage Riquadro(RawImage[] elenco, int i)
    {
        return elenco != null && i < elenco.Length ? elenco[i] : null;
    }

    private TextMeshProUGUI Testo(TextMeshProUGUI[] elenco, int i)
    {
        return elenco != null && i < elenco.Length ? elenco[i] : null;
    }
}
