import datetime as dt
import io
import json
import tempfile
import unittest
from pathlib import Path

from PIL import Image

import build_feed as bf

NOW = dt.datetime(2026, 11, 14, 12, 0, tzinfo=dt.timezone.utc)
TODAY = dt.date(2026, 11, 14)


def news_row(**over):
    row = {
        "confirmed": True,
        "title": "Titolo di prova",
        "article_url": "https://oculandiavr.it/n/titolo-di-prova-12345",
        "date": "2026-11-14T08:00:09+00:00",
        "body": "Prima riga della notizia.",
    }
    row.update(over)
    return row


class Events(unittest.TestCase):
    def test_past_events_are_dropped_and_rest_sorted(self):
        rows = [
            {"date": "2026-11-20", "time": "21:30", "title": "Dopo", "type": "meetup"},
            {"date": "2026-11-01", "time": "21:30", "title": "Passato"},
            {"date": "2026-11-15", "time": "21:00", "title": "Prima", "type": "film", "platform": "Bigscreen"},
        ]
        out = bf.build_events(rows, TODAY)
        self.assertEqual([e["titolo"] for e in out], ["Prima", "Dopo"])
        self.assertEqual(out[0]["quando"], "dom 15 nov · 21:00")
        self.assertEqual(out[0]["tipo"], "Film")
        self.assertEqual(out[0]["dove"], "Bigscreen")

    def test_running_multi_day_event_is_kept(self):
        out = bf.build_events([{"date": "2026-11-09", "endDate": "2026-11-22", "title": "Torneo", "type": "tournament"}], TODAY)
        self.assertEqual(len(out), 1)
        self.assertTrue(out[0]["in_corso"])
        self.assertEqual(out[0]["quando"], "lun 9 nov – 22 nov")

    def test_links_stay_on_https(self):
        rows = [
            {"date": "2026-11-15", "title": "A", "url": "/atlasgate"},
            {"date": "2026-11-15", "title": "B", "url": "javascript:alert(1)"},
        ]
        out = bf.build_events(rows, TODAY)
        self.assertEqual(out[0]["link"], "https://oculandiavr.it/atlasgate")
        self.assertEqual(out[1]["link"], "https://oculandiavr.it/events")

    def test_limit_and_bad_rows(self):
        rows = [{"date": "2026-12-01", "title": f"E{i:02d}"} for i in range(30)] + ["x", {"title": "senza data"}]
        self.assertEqual(len(bf.build_events(rows, TODAY)), bf.MAX_EVENTS)

    def test_invalid_calendar_raises(self):
        with self.assertRaises(bf.FeedError):
            bf.build_events({"events": "no"}, TODAY)


class News(unittest.TestCase):
    def test_only_confirmed_articles_on_the_site(self):
        raw = {
            "articles": [
                news_row(),
                news_row(confirmed=False, title="Non confermata"),
                news_row(article_url="https://example.com/n/altro-sito-12345", title="Altro sito"),
                news_row(date="non una data", title="Data rotta"),
            ]
        }
        out = bf.build_news(raw)
        self.assertEqual([n["titolo"] for n in out], ["Titolo di prova"])
        self.assertEqual(out[0]["data"], "14 nov")

    def test_newest_first_and_limit(self):
        raw = {"articles": [news_row(title=f"N{i:02d}", date=f"2026-11-{i + 1:02d}T10:00:00+00:00") for i in range(14)]}
        out = bf.build_news(raw)
        self.assertEqual(len(out), bf.MAX_NEWS)
        self.assertEqual(out[0]["titolo"], "N13")

    def test_text_is_one_plain_line(self):
        out = bf.build_news({"articles": [news_row(body="<b>Riga</b>\n\ncon   tag " + "parola " * 60)]})
        self.assertNotIn("<", out[0]["riga"])
        self.assertNotIn("\n", out[0]["riga"])
        self.assertLessEqual(len(out[0]["riga"]), 140)
        self.assertTrue(out[0]["riga"].endswith("…"))


class Feed(unittest.TestCase):
    def test_shape_and_size(self):
        feed = bf.build_feed([{"date": "2026-11-15", "time": "21:30", "title": "Serata"}], {"articles": [news_row()]}, NOW)
        self.assertEqual(feed["v"], 1)
        self.assertEqual(feed["aggiornato"], "14 nov · 13:00")  # ora di Roma
        data = bf.encode(feed)
        self.assertEqual(json.loads(data)["eventi"][0]["titolo"], "Serata")
        self.assertLess(len(data), bf.MAX_BYTES)

    def test_oversized_feed_is_refused(self):
        with self.assertRaises(bf.FeedError):
            bf.encode({"x": "a" * (bf.MAX_BYTES + 1)})

    def test_extra_fields_for_the_tablet(self):
        feed = bf.build_feed(
            [{"id": "serata-1", "date": "2026-11-15", "title": "Serata 🎉", "description": "Tutti <b>benvenuti</b>", "url": "/events"}],
            {"articles": [news_row(cat="giochi")]},
            NOW,
        )
        event, news = feed["eventi"][0], feed["news"][0]
        self.assertEqual(event["titolo"], "Serata")  # niente emoji: il font del tablet non le ha
        self.assertEqual(event["testo"], "Tutti benvenuti")
        self.assertEqual(event["link_corto"], "oculandiavr.it/events")
        self.assertEqual((event["id"], event["img"]), ("serata-1", 1))
        self.assertEqual(news["cat"], "Giochi")
        self.assertEqual(news["link_corto"], "oculandiavr.it/n/titolo-di-prova-12345")
        self.assertEqual(feed["caselle"], {"eventi": bf.MAX_EVENTS, "news": bf.MAX_NEWS})
        self.assertTrue(feed["img_base"].startswith("https://xcapsa.github.io/oculandia-tablet/feed/img/"))

    def test_private_fields_are_not_published(self):
        feed = bf.build_feed([{"date": "2026-11-15", "title": "A", "image_url": "/a.jpg"}], {"articles": [news_row()]}, NOW)
        self.assertEqual(feed["eventi"][0]["_fonte"], "https://oculandiavr.it/a.jpg")
        self.assertNotIn("_fonte", json.loads(bf.encode(feed))["eventi"][0])

    def test_full_feed_stays_small(self):
        events = [{"date": "2026-12-01", "title": "T" * 70, "description": "d" * 400, "platform": "p" * 40} for _ in range(30)]
        news = {"articles": [news_row(title=f"{i} " + "t" * 90, body="parola " * 200, article_url=f"https://oculandiavr.it/n/articolo-numero-{i:05d}") for i in range(30)]}
        self.assertLess(len(bf.encode(bf.build_feed(events, news, NOW))), 30_000)


class Slots(unittest.TestCase):
    def test_items_keep_their_slot_when_the_list_moves(self):
        first = [{"id": "a"}, {"id": "b"}, {"id": "c"}]
        bf.assign_slots(first, None, 3)
        self.assertEqual([item["img"] for item in first], [1, 2, 3])
        # Esce "a", entra "d" in testa: "b" e "c" non si muovono, "d" prende la casella libera.
        second = [{"id": "d"}, {"id": "b"}, {"id": "c"}]
        bf.assign_slots(second, first, 3)
        self.assertEqual([item["img"] for item in second], [1, 2, 3])
        third = [{"id": "e"}, {"id": "d"}, {"id": "c"}]
        bf.assign_slots(third, second, 3)
        self.assertEqual({item["id"]: item["img"] for item in third}, {"e": 2, "d": 1, "c": 3})

    def test_broken_previous_feed_is_ignored(self):
        for previous in ("x", [{"id": "a", "img": 99}, {"id": "b", "img": "1"}, 5], [{"id": "a", "img": 2}, {"id": "b", "img": 2}]):
            items = [{"id": "a"}, {"id": "b"}]
            bf.assign_slots(items, previous, 2)
            self.assertEqual(sorted(item["img"] for item in items), [1, 2])


def picture(width=1280, height=720, color=(200, 30, 30), kind="JPEG", mode="RGB"):
    buffer = io.BytesIO()
    Image.new(mode, (width, height), color).save(buffer, kind)
    return buffer.getvalue()


class FakeSite:
    """Finto sito: risponde solo agli indirizzi previsti e conta le richieste."""

    def __init__(self, pages):
        self.pages = pages
        self.calls = []

    def __call__(self, url):
        self.calls.append(url)
        if url not in self.pages:
            raise OSError("404")
        return self.pages[url]


def small_feed(previous=None):
    events = [
        {"id": "e1", "date": "2026-11-15", "title": "Con locandina", "image_url": "/assets/a.jpg"},
        {"id": "e2", "date": "2026-11-16", "title": "Senza locandina"},
        {"id": "e3", "date": "2026-11-17", "title": "Locandina rotta", "image_url": "/assets/rotta.jpg"},
        {"id": "e4", "date": "2026-11-18", "title": "Altro sito", "image_url": "https://example.com/x.jpg"},
    ]
    news = {"articles": [news_row(image_url="https://minecraft.oculandiavr.it/n/uno.jpg")]}
    return bf.build_feed(events, news, NOW, previous)


class Images(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.dir = Path(self.tmp.name) / "img"
        self.site = FakeSite(
            {
                "https://oculandiavr.it/assets/a.jpg": picture(1024, 1280),
                "https://oculandiavr.it/assets/rotta.jpg": b"non sono una immagine",
                "https://minecraft.oculandiavr.it/n/uno.jpg": picture(3000, 1000, kind="PNG", mode="RGBA", color=(0, 0, 0, 0)),
            }
        )
        self.log = []

    def build(self, feed):
        return bf.build_images(feed, self.dir, self.site, self.log.append)

    def size(self, name):
        with Image.open(self.dir / name) as image:
            return image.format, image.mode, image.size

    def test_every_slot_gets_a_file_within_vrchat_limits(self):
        summary = self.build(small_feed())
        names = sorted(path.name for path in self.dir.glob("*.jpg"))
        expected = [f"evento-{i}.jpg" for i in range(1, bf.MAX_EVENTS + 1)] + [f"news-{i}.jpg" for i in range(1, bf.MAX_NEWS + 1)]
        self.assertEqual(names, sorted(expected))
        self.assertEqual(summary["scritte"], bf.MAX_EVENTS + bf.MAX_NEWS)
        for name in names:
            kind, mode, size = self.size(name)
            self.assertEqual((kind, mode), ("JPEG", "RGB"))
            self.assertEqual(size, bf.EVENT_SIZE if name.startswith("evento") else bf.NEWS_SIZE)
            self.assertLessEqual(max(size), 1024)

    def test_missing_broken_and_foreign_images_become_fallbacks(self):
        feed = small_feed()
        summary = self.build(feed)
        # 3 eventi senza immagine buona + 8 caselle evento vuote + 9 caselle news vuote
        self.assertEqual(summary["riserva"], 3 + (bf.MAX_EVENTS - 4) + (bf.MAX_NEWS - 1))
        self.assertNotIn("https://example.com/x.jpg", self.site.calls)  # solo domini di Oculandia
        self.assertTrue(any("rotta.jpg" in line for line in self.log))
        sources = json.loads((self.dir / bf.SOURCES_FILE).read_text("utf-8"))
        self.assertEqual(sources["evento-1.jpg"], "https://oculandiavr.it/assets/a.jpg")
        self.assertEqual(sources["evento-2.jpg"], "riserva: Senza locandina")
        self.assertEqual(sources["evento-12.jpg"], "riserva: Oculandia")
        for item in feed["eventi"] + feed["news"]:
            self.assertRegex(item["img_v"], r"^[0-9a-f]{8}$")

    def test_second_run_writes_nothing_and_skips_news_downloads(self):
        feed = small_feed()
        self.build(feed)
        before = {path.name: path.stat().st_mtime_ns for path in self.dir.iterdir()}
        versions = [item["img_v"] for item in feed["eventi"] + feed["news"]]
        self.site.calls.clear()
        again = small_feed(json.loads(bf.encode(feed)))
        summary = self.build(again)
        self.assertEqual(summary["scritte"], 0)
        self.assertEqual({path.name: path.stat().st_mtime_ns for path in self.dir.iterdir()}, before)
        self.assertEqual([item["img_v"] for item in again["eventi"] + again["news"]], versions)
        self.assertNotIn("https://minecraft.oculandiavr.it/n/uno.jpg", self.site.calls)

    def test_failed_download_keeps_the_previous_good_image(self):
        feed = small_feed()
        self.build(feed)
        good = (self.dir / "evento-1.jpg").read_bytes()
        del self.site.pages["https://oculandiavr.it/assets/a.jpg"]
        again = small_feed(json.loads(bf.encode(feed)))
        self.build(again)
        self.assertEqual((self.dir / "evento-1.jpg").read_bytes(), good)
        self.assertEqual(again["eventi"][0]["img_v"], feed["eventi"][0]["img_v"])

    def test_changed_picture_changes_the_version(self):
        feed = small_feed()
        self.build(feed)
        self.site.pages["https://oculandiavr.it/assets/a.jpg"] = picture(1024, 1280, color=(10, 200, 10))
        again = small_feed(json.loads(bf.encode(feed)))
        summary = self.build(again)
        self.assertEqual(summary["scritte"], 1)
        self.assertNotEqual(again["eventi"][0]["img_v"], feed["eventi"][0]["img_v"])

    def test_fallback_is_stable_and_differs_by_title(self):
        first = bf.fallback_image("Serata VRChat nella stanza Oculandia con un titolo molto lungo", bf.EVENT_SIZE)
        self.assertEqual(first, bf.fallback_image("Serata VRChat nella stanza Oculandia con un titolo molto lungo", bf.EVENT_SIZE))
        self.assertNotEqual(first, bf.fallback_image("Altro", bf.EVENT_SIZE))
        with Image.open(io.BytesIO(bf.fallback_image("", bf.NEWS_SIZE))) as image:
            self.assertEqual(image.size, bf.NEWS_SIZE)

    def test_image_source_accepts_only_oculandia_https(self):
        self.assertEqual(bf.image_source("/assets/a.jpg"), "https://oculandiavr.it/assets/a.jpg")
        self.assertEqual(bf.image_source("https://minecraft.oculandiavr.it/a.jpg"), "https://minecraft.oculandiavr.it/a.jpg")
        for bad in ("", None, "http://oculandiavr.it/a.jpg", "//example.com/a.jpg", "https://oculandiavr.it.example.com/a.jpg", "file:///etc/passwd"):
            self.assertEqual(bf.image_source(bad), "")


class Main(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.out = self.root / "feed" / "tablet.json"
        self.site = FakeSite(
            {
                bf.EVENTS_URL: json.dumps([{"id": "e1", "date": "2026-11-15", "title": "Serata", "image_url": "/assets/a.jpg"}]).encode(),
                bf.NEWS_URL: json.dumps({"articles": [news_row()]}).encode(),
                "https://oculandiavr.it/assets/a.jpg": picture(800, 1000),
            }
        )

    def run_main(self, *extra, minutes=0):
        return bf.main(["--out", str(self.out), *extra], fetch=self.site, now=NOW + dt.timedelta(minutes=minutes))

    def test_writes_feed_and_images(self):
        self.assertEqual(self.run_main(), 0)
        feed = json.loads(self.out.read_text("utf-8"))
        self.assertEqual(feed["eventi"][0]["img"], 1)
        self.assertRegex(feed["eventi"][0]["img_v"], r"^[0-9a-f]{8}$")
        self.assertTrue((self.out.parent / "img" / "evento-1.jpg").exists())
        self.assertTrue((self.out.parent / "img" / f"news-{bf.MAX_NEWS}.jpg").exists())

    def test_nothing_is_rewritten_when_only_the_time_changes(self):
        self.run_main()
        before = {path: path.read_bytes() for path in self.root.rglob("*") if path.is_file()}
        self.assertEqual(self.run_main(minutes=30), 0)
        self.assertEqual({path: path.read_bytes() for path in self.root.rglob("*") if path.is_file()}, before)

    def test_new_content_rewrites_the_feed(self):
        self.run_main()
        self.site.pages[bf.NEWS_URL] = json.dumps({"articles": [news_row(title="Nuovo titolo")]}).encode()
        self.run_main(minutes=30)
        feed = json.loads(self.out.read_text("utf-8"))
        self.assertEqual(feed["news"][0]["titolo"], "Nuovo titolo")
        self.assertEqual(feed["aggiornato"], "14 nov · 13:30")

    def test_broken_source_keeps_the_previous_feed(self):
        self.run_main()
        before = self.out.read_bytes()
        self.site.pages[bf.NEWS_URL] = b"<html>errore</html>"
        self.assertEqual(self.run_main(minutes=30), 1)
        self.assertEqual(self.out.read_bytes(), before)

    def test_no_img_leaves_images_alone(self):
        self.assertEqual(self.run_main("--no-img"), 0)
        self.assertFalse((self.out.parent / "img").exists())
        self.assertEqual(json.loads(self.out.read_text("utf-8"))["eventi"][0]["img_v"], "")


if __name__ == "__main__":
    unittest.main()
