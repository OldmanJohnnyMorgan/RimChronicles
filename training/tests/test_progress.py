"""Training progress tests (D6): stdlib only, no torch."""
import csv
import dataclasses
import json
import re
import tempfile
import unittest
from pathlib import Path

from rimchron_train import config, data, progress


def row(case, choice="Wait", allowed=("Wait", "Raid"), group="Nemesis/Hook", augment="orig"):
    return data.Row(f"{case}#{augment}", case, group, "director/v2", "Minimum", "s", "u", list(allowed),
                    json.dumps({"choice": choice, "reason": "r"}))


class ProgressTests(unittest.TestCase):
    def test_eval_sample_takes_one_row_per_case_spread_over_the_split(self):
        rows = [row(f"c{i:03}", augment=a) for i in range(100) for a in ("orig", "order1")]
        picked = progress.eval_sample(rows, 10)
        self.assertEqual(len(picked), 10)
        self.assertEqual(len({r.case for r in picked}), 10)
        self.assertEqual(picked[0].case, "c000")
        self.assertEqual(picked[-1].case, "c090")  # spread, not the first ten
        self.assertEqual(len(progress.eval_sample(rows, 500)), 100)

    def test_score_counts_valid_agreement_wait_and_groups(self):
        answers = [
            (row("a", "Wait"), '{"choice": "Wait", "reason": "x"}'),
            (row("b", "Raid", group="Empire/Act2"), '{"choice": "Wait", "reason": "x"}'),
            (row("c", "Raid", group="Empire/Act2"), 'not json'),
            (row("d", "Raid"), '{"choice": "Storm", "reason": "x"}'),  # not allowed
        ]
        r = progress.score(answers)
        self.assertEqual((r.total, r.valid, r.agree, r.waits), (4, 2, 1, 2))
        self.assertEqual(r.by_group["Empire/Act2"], (0, 2))
        self.assertAlmostEqual(r.top1, 0.25)
        self.assertEqual(r.as_dict()["byGroup"]["Nemesis/Hook"], {"agree": 1, "total": 2})

    def test_early_stopper_keeps_the_best_and_stops_after_patience(self):
        s = progress.EarlyStopper(patience=2)
        self.assertEqual(s.update(100, 0.50), (True, False))
        self.assertEqual(s.update(200, 0.60), (True, False))
        self.assertEqual(s.update(300, 0.60), (False, False))  # a tie isn't an improvement
        self.assertEqual(s.update(400, 0.55), (False, True))
        self.assertEqual((s.best, s.best_step), (0.60, 200))
        never = progress.EarlyStopper(patience=0)
        for step in range(10):
            self.assertFalse(never.update(step, 0.1)[1])

    def test_csv_log_writes_a_header_and_rows(self):
        with tempfile.TemporaryDirectory() as t:
            log = progress.CsvLog(Path(t) / "run" / "progress.csv")
            log.write(step=100, val_top1=0.5)
            rows = list(csv.reader((Path(t) / "run" / "progress.csv").open()))
            self.assertEqual(rows[0], progress.LOG_FIELDS)
            self.assertEqual(rows[1][0], "100")
            self.assertEqual(rows[1][progress.LOG_FIELDS.index("val_top1")], "0.5")

    def test_set_aside_keeps_an_earlier_run(self):
        with tempfile.TemporaryDirectory() as t:
            run = Path(t) / "director-e2b"
            self.assertIsNone(progress.set_aside(run))  # nothing there yet
            (run / "lora").mkdir(parents=True)
            (run / "progress.csv").write_text("x")
            self.assertEqual(progress.set_aside(run), Path(t) / "director-e2b-prev-1")
            (run / "lora").mkdir(parents=True)
            self.assertEqual(progress.set_aside(run), Path(t) / "director-e2b-prev-2")
            self.assertFalse(run.exists())

    def test_length_report_and_last_loss(self):
        self.assertIn("p99 99, longest 100 of max_seq_length 4096", progress.length_report(list(range(1, 101)), 4096))
        self.assertEqual(progress.last_train_loss([{"loss": 2.0}, {"loss": 1.5}, {"eval": 1}]), 1.5)
        self.assertIsNone(progress.last_train_loss([{"eval": 1}]))


class ConfigFileTests(unittest.TestCase):
    """The YAML configs only use keys Config knows (pyyaml isn't in the codespace, so keys are read by pattern)."""

    def test_every_config_file_matches_the_config_fields(self):
        fields = {f.name for f in dataclasses.fields(config.Config)}
        required = {f.name for f in dataclasses.fields(config.Config)
                    if f.default is dataclasses.MISSING and f.default_factory is dataclasses.MISSING}
        files = sorted((Path(__file__).resolve().parent.parent / "config").glob("*.yaml"))
        self.assertTrue(files)
        for f in files:
            keys = {m.group(1) for m in re.finditer(r"^([a-z_0-9]+):", f.read_text(encoding="utf-8"), re.M)}
            self.assertFalse(keys - fields, f"{f.name}: unknown keys {keys - fields}")
            self.assertFalse(required - keys, f"{f.name}: missing {required - keys}")



class StepTimerTests(unittest.TestCase):
    def feed(self, timer, seconds, n):
        return [timer.add(seconds) for _ in range(n)]

    def test_baseline_skips_the_warm_up_step(self):
        t = progress.StepTimer(baseline_steps=5, window=3)
        self.assertEqual([t.add(s) for s in (60, 7, 7, 7, 7, 7)], [None] * 6)
        self.assertEqual(t.baseline, 7)

    def test_a_steady_run_never_warns(self):
        t = progress.StepTimer(baseline_steps=5, window=3)
        self.assertEqual(set(self.feed(t, 7, 200)), {None})

    def test_a_slowdown_warns_once_then_stops(self):
        t = progress.StepTimer(warn_factor=3, stop_factor=10, baseline_steps=5, window=3)
        self.feed(t, 7, 10)
        out = self.feed(t, 30, 10)
        self.assertEqual(out.count("warn"), 1)
        self.assertNotIn("stop", out)
        self.assertIn("stop", self.feed(t, 100, 3))

    def test_one_slow_step_is_not_a_slowdown(self):
        t = progress.StepTimer(baseline_steps=5, window=3)
        self.feed(t, 7, 10)
        self.assertEqual([t.add(s) for s in (500, 7, 7)], [None] * 3)

    def test_warns_again_after_the_pace_recovers(self):
        t = progress.StepTimer(warn_factor=3, stop_factor=0, baseline_steps=5, window=3)
        self.feed(t, 7, 10)
        self.assertIn("warn", self.feed(t, 30, 5))
        self.feed(t, 7, 5)
        self.assertIn("warn", self.feed(t, 30, 5))
        self.assertNotIn("stop", self.feed(t, 1000, 5))  # stop_factor 0: never stops

if __name__ == "__main__":
    unittest.main()
