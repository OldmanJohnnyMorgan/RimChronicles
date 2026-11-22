# Director model training

Fine-tunes Gemma 4 to make the plot director's decisions (`docs/SPEC-director-model.md`, plan in
`docs/director-model-tech-spec.md`). Python lives only here and never ships with the mod. The C# harness builds the
data (`director-gen`, `director-pack`, `director-labels-check`, `director-dataset`); this folder trains and exports.

Commands: **PowerShell** for `dotnet` and `ollama` (Windows), **WSL2 bash** for Python (the GPU work). Don't train
while RimWorld or another training job is running: it needs the whole card.

## One-time setup (WSL2)

```bash
# uv manages the Python environment; keep it on the WSL disk, not /mnt/c (much faster)
curl -LsSf https://astral.sh/uv/install.sh | sh
echo 'export UV_PROJECT_ENVIRONMENT=$HOME/.venvs/rimchron-train' >> ~/.bashrc && source ~/.bashrc
cd /mnt/c/RimworldMods/RimChronicles/RimChronicles/training
uv sync            # first time: resolves and writes uv.lock (commit it: it pins every version)
nvidia-smi         # the RTX 5060 Ti should be listed
```

## D0: feasibility spike

Proves the whole path on a tiny run before any labelling: train a LoRA on Gemma 4 E2B, export it, load it in Ollama,
and answer director prompts through the game's own C# client with grammar. About 30–60 minutes of GPU.

**1. Build the data (PowerShell):**

```powershell
cd C:\RimworldMods\RimChronicles\RimChronicles
git pull
dotnet run --project harness/RimChronicles.Eval -- director-gen --seed 1
dotnet run --project harness/RimChronicles.Eval -- director-dataset --tier Minimum
```

`director-gen` rebuilds the cases the committed pilot labels refer to (same seed, same files: the manifest's sha256
must match). `director-dataset` writes `training/data/splits/minimum/`.

**2. Check the environment (WSL2):**

```bash
cd /mnt/c/RimworldMods/RimChronicles/RimChronicles/training
uv run python -m rimchron_train env --config config/d0.yaml
uv run python -m rimchron_train data --config config/d0.yaml
```

`env` must end with `OK`: a CUDA 12.8+ torch, the GPU with ~10 GB free, and **the base model found on Hugging Face**.
If it says the base model isn't found, find the Gemma 4 E2B *instruct* checkpoint's exact name (Unsloth's copy or
Google's) and put it in `config/d0.yaml` as `base_model`.

**3. Train (WSL2):** 30 steps on the pilot, a few minutes.

```bash
uv run python -m rimchron_train sft --config config/d0.yaml
```

If it stops with "`response_marker` … is not in the rendered chat template", it prints a rendered example: set
`instruction_marker` and `response_marker` in the config to the turn markers it shows. It ends by generating answers
for the validation rows: "val answers valid N/M" should be close to M even this early.

**4. Export (WSL2):** merge the LoRA into the model and convert it to GGUF.

```bash
uv run python -m rimchron_train export --config config/d0.yaml
```

It writes `C:\RimworldMods\rc-models\d0-e2b\Modelfile.merged.q4_k_m`, pointing at the GGUF in `merged_gguf\`, with
`gemma4:e2b`'s chat template. (D0 found Ollama refuses LoRA adapters, so there is no adapter route.) If it can't call
`ollama` from WSL, run
`ollama show gemma4:e2b --modelfile > C:\RimworldMods\rc-models\base.Modelfile` in PowerShell and add
`--base-modelfile /mnt/c/RimworldMods/rc-models/base.Modelfile`.

Conversion holds the merged model in system RAM (several GB for E2B). If it's killed (SIGKILL), give WSL more memory in
`%USERPROFILE%\.wslconfig` (`[wsl2]`, `memory=24GB`, `swap=32GB`), run `wsl --shutdown` and export again. If the GGUFs
were written but the Modelfiles weren't, `export --modelfiles-only` writes them without converting again.

**5. Load into Ollama (PowerShell):**

```powershell
cd C:\RimworldMods\rc-models\d0-e2b
ollama create rimchron-d0-merged -f Modelfile.merged.q4_k_m
```

**6. Smoke test through the C# client (PowerShell):** the director task on the 23 snapshots, grammar on and off.

```powershell
cd C:\RimworldMods\RimChronicles\RimChronicles
dotnet run --project harness/RimChronicles.Eval -- check --config training/models.d0.json
dotnet run --project harness/RimChronicles.Eval -- run --config training/models.d0.json --tasks director
```

Then push the run to `rc-results` as usual, and send me the console output of steps 2–5.

**Pass:** the merged model loads in Ollama, answers with 100% valid JSON under grammar, and stays under 3 s median
latency. The answers needn't be good yet (30 steps on 44 cases); D0 only proves the plumbing. **Passed on
2026-11-22** (run `20261122-143935`: 100% valid JSON and choices, 0.2 s median, 1.7 GB VRAM); see the training spec §11.
