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
sudo apt update && sudo apt install -y build-essential   # Triton needs a C compiler to build Unsloth's kernels
```

On Windows, open the NVIDIA Control Panel → Manage 3D settings → **CUDA - Sysmem Fallback Policy** → **Prefer No
Sysmem Fallback**. Without it, a run that runs out of VRAM under WSL2 doesn't fail: the driver moves the overflow into
system RAM and training goes on at a small fraction of its speed. Without a C compiler, Unsloth falls back to a loss
that builds the whole vocabulary's logits (several GB more VRAM), the likely cause of the first E2B run slowing from 7 s to minutes per step.

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

`env` must end with `OK`: a CUDA 12.8+ torch, the GPU with ~10 GB free, a C compiler, and **the base model found on Hugging Face**.
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

## D4b: bulk labels from the local teacher

The local Gemma 12B (`gemma4-12b` in `harness/models.json`) labels the training cases with the same rubric Claude used
(`prompts/director-teacher/`, now v3). Everything runs in **PowerShell** and needs the GPU: close RimWorld and any other
training job. Each call is constrained to the shown options, checked like a hand label, cached and saved as it finishes,
so **Ctrl+C is safe**: run the same command again and it carries on.

**1. Calibrate on the pilot (about 150 calls):** the 12B re-labels the 50 cases Claude labelled, into
`results\label-check-gemma4-12b-v3` (outside `training\data\labels`, so it can never be trained on), and reports how often
it agrees with Claude, plus the time per call.

```powershell
cd C:\RimworldMods\RimChronicles\RimChronicles
ollama pull gemma4:12b
dotnet run --project harness/RimChronicles.Eval -- director-label --recheck training/data/labels/pilot
```

Send me the agreement lines and the "s per item" figure before step 2: if the 12B disagrees with Claude too often, the
rubric or the teacher changes first.

**2. Label the training cases:** 4,000 cases from seed 1 (the pilot's 50 are skipped), 3 samples each, round-robin over
arcs and beats so a partial run is still balanced. It prints progress and the hours left every 25 items.

```powershell
dotnet run --project harness/RimChronicles.Eval -- director-label
```

The first run fixes the plan in `items.jsonl` (which cases, which option orders); later runs follow it and ignore
`--count`. To label fewer, pass `--count 1000` on the first run; to change it later, delete the folder and start over
(finished calls stay in the cache). Items that fail three times go to `failures.jsonl`; a later run retries them with fresh attempts. On a case with a Wait rule the third attempt can't pick against it (the label is marked `forced`), so such cases rarely fail.

**3. Check and aggregate, then build the dataset:**

```powershell
dotnet run --project harness/RimChronicles.Eval -- director-labels-check --pack training/data/labels/local-gemma4-12b
dotnet run --project harness/RimChronicles.Eval -- director-dataset --tier Minimum
dotnet run --project harness/RimChronicles.Eval -- director-dataset --tier Recommended
```

The dataset keeps at most 40% Wait among training cases (`--max-wait-share`; val and test keep the real mix): D0's
student waited in 78% of answers after training on 48% Wait. Commit `training/data/labels/local-gemma4-12b`
(`items.jsonl`, `labels.jsonl`, `labels-final.jsonl`) and push, as for the pilot.

**Hand labels for the Empire arc (`hand-empire-v1`, 2026-11-23).** The E2B v1 and v2 reports missed top-1 (75%, 77%)
on AwakenedEmpire: on its open cases the 12B almost never waits (Broken: 4%), while Claude's test labels wait about
half the time. Claude Code labelled 250 open Empire training cases from `seed-1` (120 Broken, 80 Ascendant, 50 Act2)
in session, with the same rubric as `test-v1` (`director-teacher/v1`), without looking at the test labels. They
agree with the 12B on 43% of those cases and wait on 55% of Broken (the 12B: 4%). Hand labels already win where both
cover a case; `--hand-only AwakenedEmpire` also leaves out the 12B's labels on the Empire's other open cases (about 870), so
they don't outvote the hand labels. Cases with a Wait rule keep the 12B's label.

## D6: train the directors

After the bulk labels (D4b) are checked and committed. One model at a time, in this order: E2B first (smaller and
faster, so problems show up sooner), then E4B.

**1. Build both datasets (PowerShell):**

```powershell
cd C:\RimworldMods\RimChronicles\RimChronicles
git pull
dotnet run --project harness/RimChronicles.Eval -- director-dataset --tier Minimum
dotnet run --project harness/RimChronicles.Eval -- director-dataset --tier Recommended
```

**2. Train E2B (WSL2):** about 2–3 hours. Every 100 steps it answers 100 val cases, prints top-1 and how often it waits,
logs a row to `~/rimchron-runs/director-e2b/progress.csv`, and keeps the LoRA with the best top-1. It stops early after
4 checks without improving. Close RimWorld, and run `ollama stop gemma4:12b` (or whichever model is loaded) first.

```bash
cd /mnt/c/RimworldMods/RimChronicles/RimChronicles/training
uv run python -m rimchron_train data --config config/director-e2b.yaml
uv run python -m rimchron_train sft --config config/director-e2b.yaml 2>&1 | tee ~/sft-director-e2b.log
```

It prints the 99th-percentile row length at the start: if that's far below `max_seq_length`, lowering it saves memory.
Ctrl+C is not resumable here; a stopped run starts over.

Every check also logs the pace (`sec_per_step`, without the checks) and the peak VRAM since the last check
(`vram_peak_gb`). Steps should take about 7 s on the 5060 Ti. If the pace reaches 3× the first steps', `sft` prints a
WARNING; at 10× it stops and keeps the best LoRA so far (`slow_warn_factor` and `slow_stop_factor` in the config; 0
never stops). That almost always means VRAM is spilling into system RAM: Task Manager → Performance → GPU shows
**Shared GPU memory** in use. Check the setup steps above, then lower `max_seq_length`.

**3. Export (WSL2):** Q4_K_M and Q8_0 GGUFs, each with a Modelfile.

```bash
uv run python -m rimchron_train export --config config/director-e2b.yaml
```

**4. Load into Ollama (PowerShell):**

```powershell
cd C:\RimworldMods\rc-models\director-e2b
ollama create rimchron-director-e2b -f Modelfile.merged.q4_k_m
```

**5. Score it against its prompted base (PowerShell):** `training/models.director.json` holds both.

```powershell
cd C:\RimworldMods\RimChronicles\RimChronicles
dotnet run --project harness/RimChronicles.Eval -- director-report --config training/models.director.json --models gemma4-e2b,director-e2b --base gemma4-e2b --name director-e2b-v1
```

Send me `progress.csv`, the end of the `sft` log, and commit the report.

**6. E4B (the Recommended tier).** The same steps with `director-e4b`, on the Recommended-tier dataset. Its config
differs from E2B's for memory: batch 1 with 16 accumulation steps (the same effective batch of 16) and
`max_seq_length` 2048, since the model is about twice as big and E2B already peaked at 10.8 of 15.7 GB. Expect
roughly 12–16 s per step and about 470 steps per epoch, so 3–5 hours if it stops early. Watch the first check's
`peak VRAM`: above about 14.5 GB, stop it and tell me before it spills.

```powershell
cd C:\RimworldMods\RimChronicles\RimChronicles
git checkout main
git pull
dotnet run --project harness/RimChronicles.Eval -- director-dataset --tier Recommended --max-wait-share 0.6 --hand-only AwakenedEmpire
ollama stop gemma4:e2b
ollama stop rimchron-director-e2b
```

```bash
cd /mnt/c/RimworldMods/RimChronicles/RimChronicles/training
uv run python -m rimchron_train data --config config/director-e4b.yaml
uv run python -m rimchron_train sft --config config/director-e4b.yaml 2>&1 | tee ~/sft-director-e4b-v1.log
uv run python -m rimchron_train export --config config/director-e4b.yaml
```

The export holds the merged model in system RAM (about 16 GB for E4B, twice E2B's). With `.wslconfig` at
`memory=24GB` and `swap=32GB` it fits; if it's killed, raise `memory` (keep 6–8 GB for Windows), run `wsl --shutdown`
and export again (`--modelfiles-only` if the GGUFs were written).

```powershell
cd C:\RimworldMods\rc-models\director-e4b
ollama create rimchron-director-e4b -f Modelfile.merged.q4_k_m
cd C:\RimworldMods\RimChronicles\RimChronicles
dotnet run --project harness/RimChronicles.Eval -- director-report --config training/models.director.json --models gemma4-e4b,director-e4b --base gemma4-e4b --name director-e4b-v1
```

## D8: the director report

`director-report` scores models on the frozen test set (500 cases, seed 9001, Claude's labels in
`training/data/labels/test-v1`) and writes `harness/reports/<name>.md` and `.json`: the training spec §7 metrics, the
ship bars, and top-1 per arc and beat. Every case runs five ways (grammar on and off, two more option orders, ids
renamed), so it's 2,500 calls per model, all cached. An **always-Wait** row is scored beside the models: 65% of the
test answers are Wait, so a model has to beat it. Run it in **PowerShell** with the GPU free.

**The prompted baseline** (about 15 min for E2B, 25 for E4B, an hour for 12B):

```powershell
cd C:\RimworldMods\RimChronicles\RimChronicles
dotnet run --project harness/RimChronicles.Eval -- director-report --models gemma4-e2b,gemma4-e4b,gemma4-12b --name base-v1
```

`--count 50` gives a quick look on 50 cases spread over the set. A fine-tuned model is added to `harness/models.json`
(or a config passed with `--config`) and compared with its prompted base: `--models gemma4-e2b,rimchron-director-e2b
--base gemma4-e2b`. Claude models are refused unless `--confirm-spend` is given. Commit the report under
`harness/reports/`: a model becomes a preset only after its report is committed.

## D7: use a trained director in the game

Once a director has a committed report that passes (D8):

1. Make sure Ollama has it: `ollama list` shows `rimchron-director-e2b` (D6 step 4).
2. In game: **Options → Mod settings → Rim Chronicles → Director model**: pick `rimchron-director-e2b` (it uses the
   Minimum prompt tier it was trained on). For a model that isn't listed yet, such as `rimchron-director-e4b`, pick
   **Other**, type its name and pick its tier (Recommended for E4B). Leave the address and key blank to use the main
   server. The main model keeps writing bulletins, letters and epilogues.
3. Press **Test connection**: it tests the main model, then the director.
4. In a Dev Mode game, **Rim Chronicles → Plot: director log** shows each decision; with a model set up the source is
   `Model`. The 200-day check (D11) uses this setup.

## D9: blind review

After a director passes its report (D8), you compare it with the teacher by eye, without knowing which answer is
which. In **PowerShell**, with Ollama running (after a report the answers come from the cache, so it's quick):

```powershell
cd C:\RimworldMods\RimChronicles\RimChronicles
dotnet run --project harness/RimChronicles.Eval -- director-review --config training/models.director.json --models director-e2b
start harness\reports\review-director-e2b\review.html
```

The page shows 50 test decisions, spread over arcs and beats. For each one, open the briefing if you like, read the
two answers (the option picked, what it does, the reason), and click **A is better**, **Same** or **B is better**. Your
picks stay in the browser if you close the page. When you're done, press **Download answers**, then:

```powershell
dotnet run --project harness/RimChronicles.Eval -- director-review --config training/models.director.json --models director-e2b --answers $env:USERPROFILE\Downloads\review-answers.json
```

It reveals which side was which and writes `review.md` (better, same, worse, and per arc and beat). The ship bar: the
model is picked or tied in at least 40% of cases. Commit the `review-director-e2b` folder with the report.
