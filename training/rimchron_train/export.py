"""`export`: the trained LoRA for Ollama (training spec §6), two ways, since D0 must find which works: merged weights
in GGUF (Q4_K_M, …) with the base model's chat template, and the base model plus the LoRA as an adapter."""
from __future__ import annotations

import shutil
import subprocess
from pathlib import Path

from .config import Config


def base_modelfile(ollama_base: str) -> str:
    """The base model's Modelfile from Ollama (Windows' ollama.exe is callable from WSL2)."""
    for exe in ("ollama", "ollama.exe"):
        if shutil.which(exe):
            return subprocess.run([exe, "show", ollama_base, "--modelfile"], capture_output=True, text=True, check=True).stdout
    raise SystemExit(f"Can't run ollama to read {ollama_base}'s template. On Windows: "
                     f"ollama show {ollama_base} --modelfile > base.Modelfile, then pass --base-modelfile base.Modelfile")


def template_lines(modelfile: str) -> list[str]:
    """TEMPLATE and stop PARAMETERs from a Modelfile: the chat format must match the base model's (training spec §5.1)."""
    out, inside = [], False
    for line in modelfile.splitlines():
        if line.startswith("TEMPLATE"):
            inside = '"""' in line and line.count('"""') == 1
            out.append(line)
        elif inside:
            out.append(line)
            inside = '"""' not in line
        elif line.startswith("PARAMETER stop"):
            out.append(line)
    return out


def run(cfg: Config, base_modelfile_path: str | None) -> int:
    from unsloth import FastModel

    lora = cfg.run_dir / "lora"
    model, tokenizer = FastModel.from_pretrained(model_name=str(lora), max_seq_length=cfg.max_seq_length, load_in_4bit=cfg.load_in_4bit)
    out = cfg.export_dir / cfg.run_name
    out.mkdir(parents=True, exist_ok=True)
    tmpl = template_lines(Path(base_modelfile_path).read_text() if base_modelfile_path else base_modelfile(cfg.ollama_base))
    if not any(l.startswith("TEMPLATE") for l in tmpl):
        raise SystemExit(f"No TEMPLATE in {cfg.ollama_base}'s Modelfile; can't build a matching Modelfile.")

    # Path A: merged weights, quantised.
    try:
        model.save_pretrained_gguf(str(out / "merged"), tokenizer, quantization_method=cfg.quantizations)
        ggufs = sorted((out / "merged").rglob("*.gguf"))
        for gguf in ggufs:
            quant = gguf.stem.split(".")[-1].lower()
            (out / f"Modelfile.merged.{quant}").write_text(
                "\n".join([f"FROM ./{gguf.relative_to(out).as_posix()}", *tmpl, "PARAMETER temperature 0.2", ""]))
        print(f"path A (merged GGUF): {len(ggufs)} file(s) in {out / 'merged'}")
    except Exception as ex:  # noqa: BLE001 - the spike reports, path B may still work
        print(f"path A (merged GGUF) FAILED: {type(ex).__name__}: {ex}")

    # Path B: the base model as Ollama has it, plus the LoRA's safetensors as an adapter (Ollama's documented route
    # for Gemma adapters; whether it takes Gemma 4 E-models is what the spike finds out).
    try:
        shutil.copytree(lora, out / "lora", dirs_exist_ok=True)
        (out / "Modelfile.adapter").write_text(
            "\n".join([f"FROM {cfg.ollama_base}", "ADAPTER ./lora", "PARAMETER temperature 0.2", ""]))
        print(f"path B (LoRA adapter on {cfg.ollama_base}): {out / 'lora'}")
    except Exception as ex:  # noqa: BLE001
        print(f"path B (LoRA adapter) FAILED: {type(ex).__name__}: {ex}")

    print(f"Next, in PowerShell: ollama create rimchron-{cfg.run_name} -f <one of the Modelfiles in {out}>")
    return 0
