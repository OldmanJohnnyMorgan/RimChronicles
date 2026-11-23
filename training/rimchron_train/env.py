"""`env`: check the GPU, CUDA and libraries before any training (training spec §1a)."""
from __future__ import annotations

import importlib.metadata as md
import os
import shutil
import sys


def run(base_model: str | None = None) -> int:
    problems: list[str] = []
    try:
        import torch
    except ImportError:
        print("torch is not installed: run `uv sync` in training/ first.")
        return 1

    print(f"python {sys.version.split()[0]}, torch {torch.__version__}, CUDA build {torch.version.cuda}")
    if not torch.cuda.is_available():
        problems.append("CUDA is not available to torch (check the NVIDIA driver and WSL2 GPU support: `nvidia-smi`).")
    else:
        name = torch.cuda.get_device_name(0)
        major, minor = torch.cuda.get_device_capability(0)
        free, total = torch.cuda.mem_get_info(0)
        print(f"GPU {name}, compute capability {major}.{minor}, VRAM {total / 2**30:.1f} GB ({free / 2**30:.1f} GB free)")
        cuda = tuple(int(x) for x in (torch.version.cuda or "0.0").split(".")[:2])
        if major >= 12 and cuda < (12, 8):
            problems.append(f"Blackwell (compute {major}.{minor}) needs a CUDA 12.8+ torch build; this one is CUDA {torch.version.cuda}.")
        if free < 10 * 2**30:
            problems.append(f"Only {free / 2**30:.1f} GB VRAM free: close RimWorld, Ollama models and other training first.")

    for pkg in ("unsloth", "transformers", "trl", "peft", "datasets"):
        try:
            print(f"{pkg} {md.version(pkg)}")
        except md.PackageNotFoundError:
            problems.append(f"{pkg} is not installed.")

    cc = os.environ.get("CC") or next((c for c in ("cc", "gcc", "clang") if shutil.which(c)), None)
    if cc and shutil.which(cc):
        print(f"C compiler {shutil.which(cc)}")
    else:
        problems.append("No C compiler: Triton can't build Unsloth's kernels, and training falls back to paths that "
                        "use several GB more VRAM and can run many times slower. Run `sudo apt install build-essential`.")
    if "microsoft" in os.uname().release.lower():
        print("WSL2: in the NVIDIA Control Panel set CUDA - Sysmem Fallback Policy to Prefer No Sysmem Fallback, so a "
              "run that runs out of VRAM stops with an error instead of crawling in system RAM.")

    if base_model:
        try:
            from huggingface_hub import model_info

            info = model_info(base_model)
            print(f"base model {base_model}: found ({info.sha[:8] if info.sha else 'no sha'})")
        except Exception as ex:  # noqa: BLE001 - any failure here means "fix base_model in the config"
            problems.append(f"base model {base_model!r} not found on Hugging Face ({type(ex).__name__}): fix base_model in the config.")

    for p in problems:
        print("PROBLEM:", p)
    print("OK" if not problems else f"{len(problems)} problem(s).")
    return 0 if not problems else 1
