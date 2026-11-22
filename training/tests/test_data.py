"""Data handling tests: stdlib only (no torch), so they run in the codespace. `python -m unittest discover -s tests`."""
import json
import tempfile
import unittest
from pathlib import Path

from rimchron_train import data


def row(**kw):
    r = {"id": "c#orig", "case": "c", "group": "Nemesis/Hook", "promptVersion": "director/v2", "tier": "Minimum",
         "system": "sys", "user": "usr", "allowed": ["Wait", "Raid"], "target": json.dumps({"choice": "Wait", "reason": "It's time."})}
    r.update(kw)
    return json.dumps(r)


class DataTests(unittest.TestCase):
    def write(self, d: Path, train: list[str], manifest: dict | None = None):
        (d / "train.jsonl").write_text("\n".join(train) + "\n", encoding="utf-8")
        (d / "val.jsonl").write_text(row() + "\n", encoding="utf-8")
        (d / "manifest.json").write_text(json.dumps(manifest or {"promptVersion": "director/v2", "tier": "Minimum"}), encoding="utf-8")

    def test_messages_are_system_user_assistant(self):
        with tempfile.TemporaryDirectory() as t:
            self.write(Path(t), [row()])
            r = data.load_split(Path(t), "train")[0]
            self.assertEqual([m["role"] for m in r.messages()], ["system", "user", "assistant"])
            self.assertEqual(len(r.messages(with_answer=False)), 2)
            self.assertIn("It's time.", r.messages()[2]["content"])

    def test_a_target_outside_allowed_is_rejected(self):
        with tempfile.TemporaryDirectory() as t:
            self.write(Path(t), [row(target=json.dumps({"choice": "Storm", "reason": "x"}))])
            with self.assertRaises(ValueError):
                data.load_split(Path(t), "train")

    def test_manifest_must_match_rows(self):
        with tempfile.TemporaryDirectory() as t:
            self.write(Path(t), [row(promptVersion="director/v1")])
            with self.assertRaises(ValueError):
                data.check_manifest(Path(t))

    def test_valid_answer(self):
        self.assertTrue(data.is_valid_answer('{"choice": "Wait", "reason": "x"}', ["Wait"]))
        self.assertTrue(data.is_valid_answer('Sure: {"choice": "Wait", "reason": "x"}', ["Wait"]))
        self.assertFalse(data.is_valid_answer('{"choice": "Storm", "reason": "x"}', ["Wait"]))
        self.assertFalse(data.is_valid_answer("no json", ["Wait"]))


class ExportTests(unittest.TestCase):
    def test_template_lines_keep_template_and_stops(self):
        from rimchron_train.export import template_lines
        mf = 'FROM /x/blob\nTEMPLATE """{{ .System }}\n{{ .Prompt }}"""\nPARAMETER stop <end_of_turn>\nPARAMETER temperature 1\nLICENSE """..."""\n'
        lines = template_lines(mf)
        self.assertEqual(lines[0], 'TEMPLATE """{{ .System }}')
        self.assertIn("PARAMETER stop <end_of_turn>", lines)
        self.assertNotIn("PARAMETER temperature 1", lines)
        self.assertFalse(any(l.startswith("FROM") or l.startswith("LICENSE") for l in lines))


if __name__ == "__main__":
    unittest.main()
