#!/usr/bin/env python3
"""Costruisce il feed del tablet di Oculandia per VRChat.

Legge due fonti pubbliche del sito (calendario eventi e ultime news) e scrive
un file JSON piccolo, pensato per essere scaricato da un mondo VRChat con
String Loading e letto con VRCJson.

Uso:
    python3 tools/build_feed.py --out feed/tablet.json
    python3 tools/build_feed.py --events eventi.json --news news.json --out feed/tablet.json

Senza --events e --news scarica i dati dal sito. Non servono dipendenze.
"""
from __future__ import annotations

import argparse
import datetime as dt
import json
import re
import sys
import urllib.request
from pathlib import Path
from zoneinfo import ZoneInfo

EVENTS_URL = "https://oculandiavr.it/data/events.json"
NEWS_URL = "https://oculandiavr.it/api/ocunews-feed.php"
SITE = "https://oculandiavr.it"
ROME = ZoneInfo("Europe/Rome")

MAX_EVENTS = 12
MAX_NEWS = 10
MAX_BYTES = 60_000  # un file piccolo si scarica in fretta anche da visore
FEED_VERSION = 1

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
}

ARTICLE_URL = re.compile(r"https://oculandiavr\.it/n/[a-z0-9][a-z0-9-]{2,39}")


class FeedError(Exception):
    """Dati di partenza non utilizzabili."""


def fetch_json(url: str, timeout: float = 20.0):
    request = urllib.request.Request(url, headers={"User-Agent": "oculandia-tablet-feed/1"})
    with urllib.request.urlopen(request, timeout=timeout) as response:  # noqa: S310 - URL fisso del sito
        return json.load(response)


def clean(text: object, limit: int) -> str:
    """Una riga di testo semplice: niente a capo, niente tag, lunghezza limitata."""
    value = re.sub(r"<[^>]*>", " ", str(text or ""))
    value = re.sub(r"\s+", " ", value).strip()
    if len(value) <= limit:
        return value
    cut = value[: limit - 1].rsplit(" ", 1)[0].rstrip(" ,;:.-")
    return cut + "…"


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


def build_events(raw: object, today: dt.date) -> list[dict]:
    rows = raw.get("events") if isinstance(raw, dict) else raw
    if not isinstance(rows, list):
        raise FeedError("calendario eventi non valido")
    upcoming = []
    for row in rows:
        if not isinstance(row, dict):
            continue
        day = parse_day(row.get("date"))
        title = clean(row.get("title"), 70)
        if not day or not title:
            continue
        end = parse_day(row.get("endDate"))
        if (end or day) < today:
            continue  # già passato
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
                "titolo": title,
                "quando": when_label(day, time, end),
                "giorno": day.isoformat(),
                "ora": time,
                "tipo": TIPI.get(kind, "Evento"),
                "dove": clean(row.get("platform"), 40),
                "in_corso": day <= today,
                "link": link,
            }
        )
    upcoming.sort(key=lambda item: (item["giorno"], item["ora"] or "99:99", item["titolo"]))
    return upcoming[:MAX_EVENTS]


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
                "titolo": title,
                "data": f"{local.day} {MESI[local.month - 1]}",
                "riga": clean(row.get("body"), 140),
                "link": link,
                "_ordine": stamp.timestamp(),
            }
        )
    items.sort(key=lambda item: item["_ordine"], reverse=True)
    for item in items:
        del item["_ordine"]
    return items[:MAX_NEWS]


def build_feed(events_raw: object, news_raw: object, now: dt.datetime) -> dict:
    local = now.astimezone(ROME)
    return {
        "v": FEED_VERSION,
        "generato": local.strftime("%Y-%m-%dT%H:%M:%S%z"),
        "aggiornato": f"{local.day} {MESI[local.month - 1]} · {local:%H:%M}",
        "eventi": build_events(events_raw, local.date()),
        "news": build_news(news_raw),
    }


def encode(feed: dict) -> bytes:
    # Un campo per riga: il workflow confronta il file ignorando le righe con l'orario.
    data = json.dumps(feed, ensure_ascii=False, indent=1).encode("utf-8")
    if len(data) > MAX_BYTES:
        raise FeedError(f"feed troppo grande: {len(data)} byte")
    return data


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--events", type=Path, help="file JSON degli eventi (se manca, lo scarica dal sito)")
    parser.add_argument("--news", type=Path, help="file JSON delle news (se manca, lo scarica dal sito)")
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args(argv)
    try:
        events_raw = json.loads(args.events.read_text("utf-8")) if args.events else fetch_json(EVENTS_URL)
        news_raw = json.loads(args.news.read_text("utf-8")) if args.news else fetch_json(NEWS_URL)
        data = encode(build_feed(events_raw, news_raw, dt.datetime.now(dt.timezone.utc)))
    except (FeedError, OSError, ValueError) as error:
        # Meglio tenere il feed di prima che pubblicarne uno rotto.
        print(f"feed non aggiornato: {error}", file=sys.stderr)
        return 1
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_bytes(data + b"\n")
    feed = json.loads(data)
    print(f"{args.out}: {len(feed['eventi'])} eventi, {len(feed['news'])} news, {len(data)} byte")
    return 0


if __name__ == "__main__":
    sys.exit(main())
