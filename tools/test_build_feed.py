import datetime as dt
import json
import unittest

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


if __name__ == "__main__":
    unittest.main()
