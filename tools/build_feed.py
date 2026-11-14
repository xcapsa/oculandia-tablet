#!/usr/bin/env python3
"""Costruisce il feed del tablet di Oculandia per VRChat.

Legge due fonti pubbliche del sito (calendario eventi e ultime news) e scrive:

- feed/tablet.json: un file JSON piccolo, letto dal mondo con String Loading e VRCJson;
- feed/img/evento-N.jpg e feed/img/news-N.jpg: le immagini, con nomi fissi.

I nomi sono fissi perché un mondo VRChat può usare solo indirizzi scritti nell'editor.
Ogni evento e ogni news dice nel feed quale casella usa (campo "img").

Uso:
    python3 tools/build_feed.py --out feed/tablet.json
    python3 tools/build_feed.py --events eventi.json --news news.json --out feed/tablet.json
    python3 tools/build_feed.py --out feed/tablet.json --no-img

Senza --events e --news scarica i dati dal sito. Per le immagini serve Pillow
(vedi requirements.txt). Con --no-img le immagini non vengono toccate.
"""
from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import io
import json
import re
import sys
import unicodedata
import urllib.parse
import urllib.request
from pathlib import Path
from typing import Callable
from zoneinfo import ZoneInfo

EVENTS_URL = "https://oculandiavr.it/data/events.json"
NEWS_URL = "https://oculandiavr.it/api/ocunews-feed.php"
# Le serate non stanno tutte in events.json: film e serate ricorrenti, sport e locandine hanno fonti loro.
HOME_URL = "https://oculandiavr.it/api/home-events.php"
RECURRING_URL = "https://oculandiavr.it/api/recurring-events.php"
SPORTS_URL = "https://oculandiavr.it/data/sports-calendar.json"
SITE = "https://oculandiavr.it"
PUBLIC_BASE = "https://xcapsa.github.io/oculandia-tablet/feed/"
ROME = ZoneInfo("Europe/Rome")

MAX_EVENTS = 12  # sono anche le caselle immagine: evento-1.jpg … evento-12.jpg
HORIZON_DAYS = 45  # oltre questa distanza una serata non interessa a chi è nel mondo adesso
MAX_NEWS = 10  # news-1.jpg … news-10.jpg
MAX_BYTES = 100_000  # un file piccolo si scarica in fretta anche da visore
FEED_VERSION = 1

# Immagini. VRChat rifiuta le immagini oltre 2048 x 2048: qui restiamo molto sotto,
# perché ogni immagine occupa memoria nel visore di chi guarda.
EVENT_SIZE = (640, 800)  # locandina in piedi, 4:5
NEWS_SIZE = (768, 432)  # copertina sdraiata, 16:9
JPEG_QUALITY = 82
MAX_IMAGE_BYTES = 8_000_000
IMAGE_HOSTS = {"oculandiavr.it", "www.oculandiavr.it", "minecraft.oculandiavr.it"}
SOURCES_FILE = "fonti.json"  # ricorda da dove arriva ogni immagine, per non riscaricarla

# Tinte di riserva, dallo stile di Oculandia (fondo scuro, scritta chiara).
TINTE = [(16, 32, 22), (20, 44, 34), (18, 40, 48), (40, 34, 20), (44, 22, 28), (26, 30, 52)]
CHIARO = (238, 246, 240)
VERDE = (140, 229, 150)

GIORNI = ["lun", "mar", "mer", "gio", "ven", "sab", "dom"]
MESI = ["gen", "feb", "mar", "apr", "mag", "giu", "lug", "ago", "set", "ott", "nov", "dic"]

TIPI = {
    "tournament": "Torneo",
    "meetup": "Serata",
    "cinema": "Film",
    "film": "Film",
    "sport": "Sport",
    "football": "Sport",
    "live": "Diretta",
    "community": "Community",
    "gaming": "Gioco",
    "workshop": "Laboratorio",
}

CATEGORIE = {
    "hardware": "Hardware",
    "software": "Software",
    "giochi": "Giochi",
    "mercato": "Mercato",
    "eventi": "Eventi",
    "community": "Community",
}

ARTICLE_URL = re.compile(r"https://oculandiavr\.it/n/[a-z0-9][a-z0-9-]{2,39}")

Fetch = Callable[[str], bytes]


class FeedError(Exception):
    """Dati di partenza non utilizzabili."""


def fetch_bytes(url: str, timeout: float = 20.0, limit: int = MAX_IMAGE_BYTES) -> bytes:
    request = urllib.request.Request(url, headers={"User-Agent": "oculandia-tablet-feed/2"})
    with urllib.request.urlopen(request, timeout=timeout) as response:  # noqa: S310 - solo indirizzi https del sito
        data = response.read(limit + 1)
    if len(data) > limit:
        raise OSError(f"risposta troppo grande: {url}")
    return data


def fetch_json(url: str, timeout: float = 20.0):
    return json.loads(fetch_bytes(url, timeout))


def clean(text: object, limit: int) -> str:
    """Una riga di testo semplice: niente a capo, niente tag, niente emoji, lunghezza limitata."""
    value = re.sub(r"<[^>]*>", " ", str(text or ""))
    # Il font del tablet non ha emoji né simboli: meglio toglierli che mostrare quadratini.
    value = "".join(ch for ch in value if unicodedata.category(ch) not in {"So", "Sk", "Cs", "Co", "Cn", "Cf"})
    value = re.sub(r"\s+", " ", value).strip()
    if len(value) <= limit:
        return value
    cut = value[: limit - 1].rsplit(" ", 1)[0].rstrip(" ,;:.-")
    return cut + "…"


def short_link(link: str) -> str:
    """L'indirizzo come lo si scrive a mano: senza https:// e senza barra finale."""
    return re.sub(r"^https://(www\.)?", "", link).rstrip("/")


def image_source(value: object) -> str:
    """Indirizzo https dell'immagine, solo se sta su un dominio di Oculandia. Altrimenti vuoto."""
    link = str(value or "").strip()
    if link.startswith("/") and not link.startswith("//"):
        link = SITE + link
    parts = urllib.parse.urlsplit(link)
    if parts.scheme != "https" or parts.hostname not in IMAGE_HOSTS:
        return ""
    return link


def parse_day(value: object) -> dt.date | None:
    try:
        return dt.date.fromisoformat(str(value)[:10])
    except ValueError:
        return None


def when_label(day: dt.date, time: str, end: dt.date | None) -> str:
    start = f"{GIORNI[day.weekday()]} {day.day} {MESI[day.month - 1]}"
    if end and end > day:
        return f"{start} – {end.day} {MESI[end.month - 1]}"
    return f"{start} · {time}" if time else start


def _rows(raw: object, key: str) -> list[dict]:
    rows = raw.get(key) if isinstance(raw, dict) else raw
    return [row for row in rows if isinstance(row, dict)] if isinstance(rows, list) else []


def extra_rows(home: object = None, recurring: object = None, sports: object = None) -> list[dict]:
    """Film, serate ricorrenti e sport, nella stessa forma di events.json.

    home-events.php porta le locandine: servono a dare un'immagine alle partite del calendario sportivo.
    """
    posters, rows = {}, []
    for item in _rows(home, "items"):
        ident = urllib.parse.parse_qs(urllib.parse.urlsplit(str(item.get("url") or "")).query).get("id", [""])[0]
        if ident:
            posters[ident] = item.get("image")
        if ident.startswith("sports-") or ident.startswith("atlasgate-") or not ident:
            continue  # arrivano già dal calendario sportivo o da events.json
        detail = str(item.get("detail") or "")
        clock = re.match(r"(\d{2}:\d{2})", detail)
        rows.append({"id": ident, "date": item.get("date"), "endDate": item.get("end_date"), "time": clock.group(1) if clock else "",
                     "title": item.get("title"), "type": "film" if "bigscreen" in detail.lower() else "community",
                     "platform": detail.split("·")[-1].strip() if "·" in detail else "", "description": "",
                     "url": item.get("url"), "image_url": item.get("image")})
    for item in _rows(recurring, "items"):
        text = re.sub(r"[*_#`]+", "", str(item.get("description") or ""))
        rows.append({"id": item.get("id"), "date": item.get("date"), "time": item.get("time"), "title": item.get("title"),
                     "type": "film" if str(item.get("platform") or "").lower() == "bigscreen" else "meetup",
                     "platform": item.get("platform"), "description": text, "url": "/events?tipo=film",
                     "image_url": item.get("image_url")})
    for item in _rows(sports, "events"):
        ident = str(item.get("id") or "")
        rows.append({"id": ident, "date": item.get("date"), "time": item.get("time"), "title": item.get("title"), "type": "sport",
                     "platform": "Bigscreen", "description": item.get("description"),
                     "url": f"/evento?id={urllib.parse.quote(ident)}" if ident else "/events?tipo=sport",
                     "image_url": item.get("image_url") or posters.get(ident)})
    return rows


def pick_balanced(items: list[dict], count: int) -> list[dict]:
    """Un po' di tutto: a turno un torneo, una partita, una serata, sempre partendo dalla più vicina."""
    groups: dict[str, list[dict]] = {}
    for item in items:
        groups.setdefault(item["tipo"], []).append(item)
    queues = sorted(groups.values(), key=lambda group: (group[0]["giorno"], group[0]["ora"] or "99:99"))
    chosen = []
    while len(chosen) < count and any(queues):
        for queue in queues:
            if queue and len(chosen) < count:
                chosen.append(queue.pop(0))
    chosen.sort(key=lambda item: (item["giorno"], item["ora"] or "99:99", item["titolo"]))
    return chosen


def build_events(raw: object, today: dt.date, extra: list[dict] | None = None) -> list[dict]:
    rows = raw.get("events") if isinstance(raw, dict) else raw
    if not isinstance(rows, list):
        raise FeedError("calendario eventi non valido")
    upcoming, seen = [], set()
    for row in list(rows) + list(extra or []):
        if not isinstance(row, dict):
            continue
        day = parse_day(row.get("date"))
        title = clean(row.get("title"), 70)
        if not day or not title:
            continue
        end = parse_day(row.get("endDate"))
        if (end or day) < today:
            continue  # già passato
        if (day - today).days > HORIZON_DAYS:
            continue  # troppo lontano
        key = (day.isoformat(), re.sub(r"[^a-z0-9]+", "", title.lower()))
        if key in seen:
            continue  # la stessa serata da due fonti
        seen.add(key)
        time = str(row.get("time") or "")
        time = time if re.fullmatch(r"\d{2}:\d{2}", time) else ""
        kind = str(row.get("type") or "").lower()
        link = str(row.get("url") or "")
        if link.startswith("/"):
            link = SITE + link
        if not link.startswith("https://"):
            link = SITE + "/events"
        upcoming.append(
            {
                "id": clean(row.get("id"), 60) or f"{day.isoformat()}-{title}",
                "titolo": title,
                "quando": when_label(day, time, end),
                "giorno": day.isoformat(),
                "ora": time,
                "tipo": TIPI.get(kind, "Evento"),
                "dove": clean(row.get("platform"), 40),
                "testo": clean(row.get("description"), 240),
                "in_corso": day <= today,
                "link": link,
                "link_corto": short_link(link),
                "_fonte": image_source(row.get("image_url")),
            }
        )
    upcoming.sort(key=lambda item: (item["giorno"], item["ora"] or "99:99", item["titolo"]))
    return pick_balanced(upcoming, MAX_EVENTS)


def build_news(raw: object) -> list[dict]:
    rows = raw.get("articles") if isinstance(raw, dict) else None
    if not isinstance(rows, list):
        raise FeedError("feed news non valido")
    items = []
    for row in rows:
        if not isinstance(row, dict) or row.get("confirmed") is not True:
            continue
        link = str(row.get("article_url") or "")
        title = clean(row.get("title"), 90)
        if not title or not ARTICLE_URL.fullmatch(link):
            continue
        try:
            stamp = dt.datetime.fromisoformat(str(row.get("date")))
        except ValueError:
            continue
        if stamp.tzinfo is None:
            stamp = stamp.replace(tzinfo=dt.timezone.utc)
        local = stamp.astimezone(ROME)
        items.append(
            {
                "id": link.rsplit("/", 1)[1],
                "titolo": title,
                "data": f"{local.day} {MESI[local.month - 1]}",
                "cat": CATEGORIE.get(str(row.get("cat") or "").lower(), ""),
                "riga": clean(row.get("body"), 140),
                "testo": clean(row.get("body"), 420),
                "link": link,
                "link_corto": short_link(link),
                "_fonte": image_source(row.get("image_url")),
                "_ordine": stamp.timestamp(),
            }
        )
    items.sort(key=lambda item: item["_ordine"], reverse=True)
    for item in items:
        del item["_ordine"]
    return items[:MAX_NEWS]


def assign_slots(items: list[dict], previous: object, count: int) -> None:
    """Dà a ogni voce una casella immagine da 1 a count.

    Chi era già nel feed di prima tiene la sua casella: così, quando esce una news,
    cambia una sola immagine e non tutte.
    """
    old: dict[str, int] = {}
    if isinstance(previous, list):
        for row in previous:
            if isinstance(row, dict) and isinstance(row.get("id"), str) and isinstance(row.get("img"), int):
                old[row["id"]] = row["img"]
    taken: set[int] = set()
    for item in items:
        slot = old.get(item["id"], 0)
        if 1 <= slot <= count and slot not in taken:
            item["img"] = slot
            taken.add(slot)
    free = (slot for slot in range(1, count + 1) if slot not in taken)
    for item in items:
        if "img" not in item:
            item["img"] = next(free)


def build_feed(events_raw: object, news_raw: object, now: dt.datetime, previous: object = None,
               extra: list[dict] | None = None) -> dict:
    local = now.astimezone(ROME)
    before = previous if isinstance(previous, dict) else {}
    events = build_events(events_raw, local.date(), extra)
    news = build_news(news_raw)
    assign_slots(events, before.get("eventi"), MAX_EVENTS)
    assign_slots(news, before.get("news"), MAX_NEWS)
    for item in events + news:
        item["img_v"] = ""
    return {
        "v": FEED_VERSION,
        "generato": local.strftime("%Y-%m-%dT%H:%M:%S%z"),
        "aggiornato": f"{local.day} {MESI[local.month - 1]} · {local:%H:%M}",
        "img_base": PUBLIC_BASE + "img/",
        "caselle": {"eventi": MAX_EVENTS, "news": MAX_NEWS},
        "eventi": events,
        "news": news,
    }


# ---------------------------------------------------------------- immagini


def _jpeg(image) -> bytes:
    buffer = io.BytesIO()
    image.save(buffer, "JPEG", quality=JPEG_QUALITY, optimize=True)
    return buffer.getvalue()


def render_image(data: bytes, size: tuple[int, int]) -> bytes:
    """Ritaglia al centro e ridimensiona alla misura della casella. JPEG senza dati extra."""
    from PIL import Image, ImageOps

    with Image.open(io.BytesIO(data)) as source:
        source.load()
        image = ImageOps.exif_transpose(source)
        if image.mode in ("RGBA", "LA", "P"):
            # Le parti trasparenti prendono il fondo scuro del tablet.
            image = image.convert("RGBA")
            base = Image.new("RGBA", image.size, TINTE[0] + (255,))
            image = Image.alpha_composite(base, image)
        image = image.convert("RGB")
        return _jpeg(ImageOps.fit(image, size, Image.LANCZOS))


def fallback_image(title: str, size: tuple[int, int]) -> bytes:
    """Immagine di riserva: tinta unita con il nome. Uguale a ogni giro per lo stesso titolo."""
    from PIL import Image, ImageDraw, ImageFont

    tint = TINTE[hashlib.sha256(title.encode("utf-8")).digest()[0] % len(TINTE)]
    image = Image.new("RGB", size, tint)
    draw = ImageDraw.Draw(image)
    width, height = size
    margin = width // 12
    small = ImageFont.load_default(size=max(18, width // 26))
    big = ImageFont.load_default(size=max(28, width // 14))
    draw.text((margin, margin), "OCULANDIA", font=small, fill=VERDE)
    # Il font incluso in Pillow ha solo le lettere di base: gli accenti si tolgono (Città diventa Citta).
    title = title.translate(str.maketrans({"–": "-", "—": "-", "’": "'", "‘": "'", "“": '"', "”": '"', "…": "..."}))
    title = unicodedata.normalize("NFKD", title).encode("ascii", "ignore").decode("ascii")
    lines: list[str] = []
    line = ""
    for word in (title or "Oculandia").split():
        attempt = f"{line} {word}".strip()
        if line and draw.textlength(attempt, font=big) > width - 2 * margin:
            lines.append(line)
            line = word
        else:
            line = attempt
    lines.append(line)
    lines = lines[:4]
    step = int(big.size * 1.25)
    top = (height - step * len(lines)) // 2
    for index, text in enumerate(lines):
        draw.text((margin, top + index * step), text, font=big, fill=CHIARO)
    draw.rectangle((margin, height - margin - 6, margin + width // 6, height - margin), fill=VERDE)
    return _jpeg(image)


def _digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()[:8]


def build_images(feed: dict, img_dir: Path, fetch: Fetch = fetch_bytes, log: Callable[[str], None] = print) -> dict:
    """Scrive tutte le caselle immagine e mette in ogni voce "img_v" (impronta del file).

    Non lascia mai una casella senza file: se l'immagine manca o non si scarica,
    scrive quella di riserva. Riscrive un file solo se il contenuto cambia.
    Restituisce un riassunto: {"scritte": n, "uguali": n, "riserva": n}.
    """
    img_dir.mkdir(parents=True, exist_ok=True)
    sources_path = img_dir / SOURCES_FILE
    try:
        known = json.loads(sources_path.read_text("utf-8"))
        known = known if isinstance(known, dict) else {}
    except (OSError, ValueError):
        known = {}
    downloads: dict[str, bytes | None] = {}
    summary = {"scritte": 0, "uguali": 0, "riserva": 0}
    sources: dict[str, str] = {}

    def download(url: str) -> bytes | None:
        if url not in downloads:
            try:
                downloads[url] = fetch(url)
            except (OSError, ValueError) as error:
                log(f"immagine non scaricata: {url}: {error}")
                downloads[url] = None
        return downloads[url]

    plan = (("evento", "eventi", MAX_EVENTS, EVENT_SIZE, False), ("news", "news", MAX_NEWS, NEWS_SIZE, True))
    for prefix, key, count, size, stable_urls in plan:
        by_slot = {item["img"]: item for item in feed[key]}
        for slot in range(1, count + 1):
            name = f"{prefix}-{slot}.jpg"
            path = img_dir / name
            item = by_slot.get(slot)
            title = item["titolo"] if item else "Oculandia"
            source = item.get("_fonte", "") if item else ""
            current = path.read_bytes() if path.exists() else None
            same_source = current is not None and bool(source) and known.get(name) == source
            data = None
            if same_source and stable_urls:
                data = current  # stesso indirizzo, stessa immagine: niente download
            elif source:
                raw = download(source)
                if raw is not None:
                    try:
                        data = render_image(raw, size)
                    except Exception as error:  # noqa: BLE001 - Pillow solleva tipi diversi per file rotti
                        log(f"immagine non leggibile: {source}: {error}")
                if data is None and same_source:
                    data = current  # scaricamento fallito: teniamo quella buona di prima
            label = source
            if data is None:
                data = fallback_image(title, size)
                label = "riserva: " + title
                summary["riserva"] += 1
            if data == current:
                summary["uguali"] += 1
            else:
                path.write_bytes(data)
                summary["scritte"] += 1
            sources[name] = label
            if item:
                item["img_v"] = _digest(data)
    text = json.dumps(sources, ensure_ascii=False, indent=1, sort_keys=True) + "\n"
    if not sources_path.exists() or sources_path.read_text("utf-8") != text:
        sources_path.write_text(text, "utf-8")
    return summary


def keep_image_versions(feed: dict, img_dir: Path) -> None:
    """Con --no-img: "img_v" si calcola dai file che ci sono già."""
    for prefix, key in (("evento", "eventi"), ("news", "news")):
        for item in feed[key]:
            path = img_dir / f"{prefix}-{item['img']}.jpg"
            item["img_v"] = _digest(path.read_bytes()) if path.exists() else ""


# ---------------------------------------------------------------- scrittura


def encode(feed: dict) -> bytes:
    # I campi che iniziano con "_" servono solo durante la costruzione.
    public = {
        key: [{k: v for k, v in row.items() if not k.startswith("_")} for row in value] if key in ("eventi", "news") else value
        for key, value in feed.items()
    }
    # Un campo per riga: così le differenze tra due versioni si leggono bene.
    data = json.dumps(public, ensure_ascii=False, indent=1).encode("utf-8")
    if len(data) > MAX_BYTES:
        raise FeedError(f"feed troppo grande: {len(data)} byte")
    return data


def same_content(new: bytes, old: object) -> bool:
    """Vero se cambia solo l'orario. In quel caso il file non va riscritto."""
    if not isinstance(old, dict):
        return False
    fresh = json.loads(new)
    for feed in (fresh, old):
        feed.pop("generato", None)
        feed.pop("aggiornato", None)
    return fresh == old


def optional_json(fetch: Fetch, url: str) -> object:
    """Una fonte in più che non risponde non deve fermare il feed: si va avanti senza."""
    try:
        return json.loads(fetch(url))
    except (OSError, ValueError) as error:
        print(f"fonte saltata ({url}): {error}", file=sys.stderr)
        return None


def read_previous(path: Path) -> object:
    try:
        return json.loads(path.read_text("utf-8"))
    except (OSError, ValueError):
        return None


def main(argv: list[str] | None = None, fetch: Fetch = fetch_bytes, now: dt.datetime | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--events", type=Path, help="file JSON degli eventi (se manca, lo scarica dal sito)")
    parser.add_argument("--news", type=Path, help="file JSON delle news (se manca, lo scarica dal sito)")
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--img-dir", type=Path, help="cartella delle immagini (se manca: img accanto al feed)")
    parser.add_argument("--no-img", action="store_true", help="non scaricare e non riscrivere le immagini")
    args = parser.parse_args(argv)
    img_dir = args.img_dir or args.out.parent / "img"
    previous = read_previous(args.out)
    try:
        events_raw = json.loads(args.events.read_text("utf-8")) if args.events else json.loads(fetch(EVENTS_URL))
        news_raw = json.loads(args.news.read_text("utf-8")) if args.news else json.loads(fetch(NEWS_URL))
        extra = [] if args.events else extra_rows(*(optional_json(fetch, url) for url in (HOME_URL, RECURRING_URL, SPORTS_URL)))
        feed = build_feed(events_raw, news_raw, now or dt.datetime.now(dt.timezone.utc), previous, extra)
        encode(feed)  # controlla la misura prima di toccare le immagini
        if args.no_img:
            keep_image_versions(feed, img_dir)
            summary = None
        else:
            summary = build_images(feed, img_dir, fetch)
        data = encode(feed)
    except (FeedError, OSError, ValueError) as error:
        # Meglio tenere il feed di prima che pubblicarne uno rotto.
        print(f"feed non aggiornato: {error}", file=sys.stderr)
        return 1
    if summary:
        print(f"{img_dir}: {summary['scritte']} immagini scritte, {summary['uguali']} uguali a prima, {summary['riserva']} di riserva")
    if same_content(data, previous):
        print(f"{args.out}: nessuna novità, file lasciato com'è")
        return 0
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_bytes(data + b"\n")
    print(f"{args.out}: {len(feed['eventi'])} eventi, {len(feed['news'])} news, {len(data)} byte")
    return 0


if __name__ == "__main__":
    sys.exit(main())
