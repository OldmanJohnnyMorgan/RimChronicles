"""Dataset rows from `rimchron-eval director-dataset`. No torch here, so this is testable anywhere.

The C# harness renders every prompt (the game's own renderer); Python only turns a row into chat messages and checks
that the dataset matches what the run expects.
"""
from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path


@dataclass(frozen=True)
class Row:
    id: str
    case: str
    group: str
    prompt_version: str
    tier: str
    system: str
    user: str
    allowed: list[str]
    target: str

    def messages(self, with_answer: bool = True) -> list[dict[str, str]]:
        """System, user and (for training) the assistant's JSON answer, as the game's client sends them."""
        msgs = [{"role": "system", "content": self.system}, {"role": "user", "content": self.user}]
        if with_answer:
            msgs.append({"role": "assistant", "content": self.target})
        return msgs

    def choice(self) -> str:
        return json.loads(self.target)["choice"]


def load_rows(path: Path) -> list[Row]:
    rows = []
    for n, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        if not line.strip():
            continue
        r = json.loads(line)
        row = Row(r["id"], r["case"], r.get("group", ""), r["promptVersion"], r["tier"], r["system"], r["user"],
                  list(r["allowed"]), r["target"])
        if row.choice() not in row.allowed:
            raise ValueError(f"{path.name}:{n}: target choice {row.choice()!r} is not in allowed {row.allowed}")
        rows.append(row)
    return rows


def load_split(dataset_dir: Path, split: str) -> list[Row]:
    return load_rows(dataset_dir / f"{split}.jsonl")


def check_manifest(dataset_dir: Path) -> dict:
    """The dataset's manifest; every row must match its prompt version and tier (training spec §3)."""
    manifest = json.loads((dataset_dir / "manifest.json").read_text(encoding="utf-8"))
    for split in ("train", "val"):
        for row in load_split(dataset_dir, split):
            if row.prompt_version != manifest["promptVersion"] or row.tier != manifest["tier"]:
                raise ValueError(f"{row.id}: built for {row.prompt_version}/{row.tier}, manifest says "
                                 f"{manifest['promptVersion']}/{manifest['tier']}")
    return manifest


def is_valid_answer(text: str, allowed: list[str]) -> bool:
    """A generated answer is JSON with an allowed choice and a reason (what grammar enforces in the game)."""
    try:
        start, end = text.index("{"), text.rindex("}") + 1
        answer = json.loads(text[start:end])
    except ValueError:
        return False
    return isinstance(answer, dict) and answer.get("choice") in allowed and isinstance(answer.get("reason"), str)
