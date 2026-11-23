"""Training progress for `sft` (D6): which val rows to generate on, how to score the answers, when to stop, and the
CSV log. No torch here, so all of it is testable in the codespace; `sft` calls it from a trainer callback."""
from __future__ import annotations

import csv
import json
import math
import statistics
from collections import defaultdict
from dataclasses import dataclass, field
from pathlib import Path

from . import data

LOG_FIELDS = ["step", "epoch", "train_loss", "val_valid", "val_top1", "val_wait", "best_top1", "best_step", "elapsed_min",
              "sec_per_step", "vram_peak_gb"]


def eval_sample(rows: list[data.Row], n: int) -> list[data.Row]:
    """Up to n val rows, one per case, spread evenly over the split (the dataset groups cases by id, and the
    generator lays situations out in blocks of ids, so the first n would be alike)."""
    seen, firsts = set(), []
    for r in rows:
        if r.case not in seen:
            seen.add(r.case)
            firsts.append(r)
    if n <= 0 or n >= len(firsts):
        return firsts
    step = len(firsts) / n
    return [firsts[int(i * step)] for i in range(n)]


def parse_choice(text: str, allowed: list[str]) -> str | None:
    """The answer's choice if it's valid (JSON with an allowed choice and a reason), else None."""
    if not data.is_valid_answer(text, allowed):
        return None
    return json.loads(text[text.index("{"):text.rindex("}") + 1])["choice"]


@dataclass
class EvalResult:
    total: int
    valid: int
    agree: int
    waits: int
    by_group: dict[str, tuple[int, int]] = field(default_factory=dict)  # group -> (agree, total)

    @property
    def top1(self) -> float:
        return self.agree / self.total if self.total else 0.0

    @property
    def valid_share(self) -> float:
        return self.valid / self.total if self.total else 0.0

    @property
    def wait_share(self) -> float:
        return self.waits / self.total if self.total else 0.0

    def as_dict(self) -> dict:
        return {"total": self.total, "valid": self.valid, "agree": self.agree, "top1": round(self.top1, 4),
                "waitShare": round(self.wait_share, 4),
                "byGroup": {g: {"agree": a, "total": t} for g, (a, t) in sorted(self.by_group.items())}}


def score(answers: list[tuple[data.Row, str]]) -> EvalResult:
    """Scores generated answers against the teacher's choices."""
    valid = agree = waits = 0
    groups: dict[str, list[int]] = defaultdict(lambda: [0, 0])
    for row, text in answers:
        choice = parse_choice(text, row.allowed)
        valid += choice is not None
        hit = choice == row.choice()
        agree += hit
        waits += choice == "Wait"
        groups[row.group][0] += hit
        groups[row.group][1] += 1
    return EvalResult(len(answers), valid, agree, waits, {g: (a, t) for g, (a, t) in groups.items()})


class EarlyStopper:
    """Stops when val top-1 hasn't improved by at least min_delta for `patience` evaluations in a row."""

    def __init__(self, patience: int, min_delta: float = 0.0):
        self.patience, self.min_delta = patience, min_delta
        self.best: float = -math.inf
        self.best_step: int | None = None
        self.bad = 0

    def update(self, step: int, value: float) -> tuple[bool, bool]:
        """Returns (improved, stop)."""
        if value > self.best + self.min_delta:
            self.best, self.best_step, self.bad = value, step, 0
            return True, False
        self.bad += 1
        return False, self.patience > 0 and self.bad >= self.patience


class CsvLog:
    """Appends one row per evaluation to `progress.csv` in the run folder."""

    def __init__(self, path: Path):
        self.path = path
        path.parent.mkdir(parents=True, exist_ok=True)
        with path.open("w", newline="", encoding="utf-8") as f:
            csv.writer(f).writerow(LOG_FIELDS)

    def write(self, **row) -> None:
        with self.path.open("a", newline="", encoding="utf-8") as f:
            csv.writer(f).writerow([row.get(k, "") for k in LOG_FIELDS])


def set_aside(run_dir: Path) -> Path | None:
    """Moves an earlier run's folder to <run>-prev-N before a new run starts, so its log and best LoRA survive."""
    if not (run_dir / "progress.csv").exists() and not (run_dir / "lora").exists():
        return None
    n = 1
    while (target := run_dir.with_name(f"{run_dir.name}-prev-{n}")).exists():
        n += 1
    run_dir.rename(target)
    return target


def token_count(input_ids) -> int:
    """Tokens in one tokenized row. Gemma 4's processor returns a batch (a list holding one list of ids) even for one
    text, so a plain len() would count the batch: 1."""
    ids = list(input_ids)
    if ids and hasattr(ids[0], "__len__"):  # a batch of rows; a token id (int or numpy int) has no length
        return sum(len(row) for row in ids)
    return len(ids)


def length_report(lengths: list[int], limit: int) -> str:
    """The longest row and the 99th percentile, against max_seq_length (training spec §5.2: set it from the p99)."""
    s = sorted(lengths)
    p99 = s[min(len(s) - 1, math.ceil(0.99 * len(s)) - 1)]
    return f"row lengths: p99 {p99}, longest {s[-1]} of max_seq_length {limit} tokens"


def last_train_loss(log_history: list[dict]) -> float | None:
    """The most recent training loss in a transformers Trainer log history."""
    for entry in reversed(log_history):
        if "loss" in entry:
            return entry["loss"]
    return None


class StepTimer:
    """Spots a run that slows down partway (on WSL2 the driver moves overflowing VRAM into system RAM instead of
    failing, and training then crawls for hours without an error). Steps are timed without the evaluations. The
    baseline is the median of the first `baseline_steps` steps (skipping the first, which warms up); each step after
    that compares the median of the last `window` steps with it. Returns "warn" past warn_factor (once until the
    pace recovers), "stop" past stop_factor (0: never), else None."""

    def __init__(self, warn_factor: float = 3.0, stop_factor: float = 10.0, baseline_steps: int = 20, window: int = 10):
        self.warn_factor, self.stop_factor = warn_factor, stop_factor
        self.baseline_steps, self.window = baseline_steps, window
        self.times: list[float] = []
        self.baseline: float | None = None
        self.warned = False

    @property
    def recent(self) -> float | None:
        return statistics.median(self.times[-self.window:]) if self.times else None

    @property
    def ratio(self) -> float:
        return self.recent / self.baseline if self.baseline and self.recent else 1.0

    def add(self, seconds: float) -> str | None:
        self.times.append(seconds)
        if self.baseline is None:
            if len(self.times) > self.baseline_steps:
                self.baseline = statistics.median(self.times[1:])
                self.times = self.times[-1:]  # the window starts after the baseline
            return None
        if len(self.times) < self.window:
            return None
        ratio = self.ratio
        if self.stop_factor and ratio >= self.stop_factor:
            return "stop"
        if ratio >= self.warn_factor:
            if not self.warned:
                self.warned = True
                return "warn"
        elif ratio < self.warn_factor / 2:
            self.warned = False
        return None


SLOWDOWN_HELP = ("Usually VRAM is full and the driver is moving it into system RAM (Task Manager > GPU > Shared GPU "
                 "memory). Fixes: `sudo apt install build-essential` in WSL (without a C compiler Unsloth's fused loss "
                 "kernel falls back to building the whole vocabulary's logits), NVIDIA Control Panel > Manage 3D "
                 "settings > CUDA - Sysmem Fallback Policy > Prefer No Sysmem Fallback, and a lower max_seq_length.")
