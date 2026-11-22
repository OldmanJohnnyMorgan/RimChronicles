from __future__ import annotations

import os
from dataclasses import dataclass, field
from pathlib import Path


@dataclass
class Config:
    base_model: str
    ollama_base: str
    dataset: Path
    output_dir: Path
    export_dir: Path
    run_name: str
    instruction_marker: str
    response_marker: str
    load_in_4bit: bool = True
    max_seq_length: int = 4096
    lora_r: int = 16
    lora_alpha: int = 32
    lora_dropout: float = 0.05
    learning_rate: float = 2e-4
    warmup_ratio: float = 0.03
    per_device_batch: int = 2
    grad_accumulation: int = 8
    epochs: float = 3
    max_steps: int = -1
    seed: int = 1
    quantizations: list[str] = field(default_factory=lambda: ["q4_k_m"])
    # Generate-based evaluation and early stopping (training spec §5.2): every eval_every_steps optimizer steps, answer
    # eval_rows val cases; keep the LoRA with the best top-1; stop after `patience` evaluations without improvement.
    eval_every_steps: int = 0                 # 0: only at the end
    eval_rows: int = 100
    patience: int = 3                         # 0: never stop early
    min_delta: float = 0.0
    max_new_tokens: int = 300                 # the game's budget (prompts/director/v2: 200 at Minimum, 300 above)
    min_free_vram_gb: float = 6.0             # stop at the start, with a clear message, below this

    @property
    def run_dir(self) -> Path:
        return self.output_dir / self.run_name


def load(path: str | Path) -> Config:
    import yaml  # only needed on the training machine

    path = Path(path)
    raw = yaml.safe_load(path.read_text(encoding="utf-8"))
    here = path.resolve().parent.parent  # training/

    def resolve(p: str) -> Path:
        p = os.path.expanduser(p)
        return Path(p) if os.path.isabs(p) else (here / p).resolve()

    for key in ("dataset", "output_dir", "export_dir"):
        raw[key] = resolve(raw[key])
    return Config(**raw)
