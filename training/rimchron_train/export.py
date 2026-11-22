"""`export`: the trained LoRA for Ollama (training spec §6): merged weights in GGUF (Q4_K_M, …) with the base model's
chat template. D0 found this is the only route: Ollama refuses LoRA adapters ("LoRA adapters are no longer supported")."""
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


def write_merged_modelfiles(out: Path, tmpl: list[str]) -> list[Path]:
    """One Modelfile.merged.<quant> per text GGUF under out/merged* (Unsloth writes them to merged_gguf/ beside the
    merged/ checkpoint). The mmproj file is Gemma's vision projector: the director is text-only, so it's skipped."""
    ggufs = sorted(g for d in sorted(out.glob("merged*")) if d.is_dir() for g in d.rglob("*.gguf") if "mmproj" not in g.name.lower())
    for gguf in ggufs:
        quant = gguf.stem.split(".")[-1].lower()
        (out / f"Modelfile.merged.{quant}").write_text(
            "\n".join([f"FROM ./{gguf.relative_to(out).as_posix()}", *tmpl, "PARAMETER temperature 0.2", ""]))
    return ggufs


def run(cfg: Config, base_modelfile_path: str | None, modelfiles_only: bool = False) -> int:
    out = cfg.export_dir / cfg.run_name
    out.mkdir(parents=True, exist_ok=True)
    tmpl = template_lines(Path(base_modelfile_path).read_text() if base_modelfile_path else base_modelfile(cfg.ollama_base))
    if not any(l.startswith("TEMPLATE") for l in tmpl):
        raise SystemExit(f"No TEMPLATE in {cfg.ollama_base}'s Modelfile; can't build a matching Modelfile.")
    if modelfiles_only:
        ggufs = write_merged_modelfiles(out, tmpl)
        print(f"merged GGUF: wrote Modelfiles for {[g.name for g in ggufs]} in {out}")
        return 0 if ggufs else 1

    from unsloth import FastModel

    lora = cfg.run_dir / "lora"
    model, tokenizer = FastModel.from_pretrained(model_name=str(lora), max_seq_length=cfg.max_seq_length, load_in_4bit=cfg.load_in_4bit)

    # Merged weights, quantised. Conversion holds the model in host RAM: give WSL enough (README).
    model.save_pretrained_gguf(str(out / "merged"), tokenizer, quantization_method=cfg.quantizations)
    ggufs = write_merged_modelfiles(out, tmpl)
    print(f"merged GGUF: {[g.name for g in ggufs]} in {out}")
    if not ggufs:
        return 1


    print(f"Next, in PowerShell: cd {out}; ollama create rimchron-{cfg.run_name} -f Modelfile.merged.<quant>")
    return 0
