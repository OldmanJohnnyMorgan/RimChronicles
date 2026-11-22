# Rim Chronicles — Director Model Training Spec

**For:** Claude Code · **Owner:** John · **Date:** 2026-11-22
**Parent spec:** Rim Chronicles — Design Spec (Stages 0–5). This spec adds a training track for the plot director's model. Where they disagree, the parent spec wins.

---

## 1. Goal

Train a small local model (~2B and ~4B) that makes the plot director's decisions as well as Claude does, so the director runs fully offline at the Minimum and Recommended tiers.

Claude stays in the loop **offline only**: it labels training data and judges evals. Players never need a cloud model.

### What the director does (from the parent spec)

Once per in-game day, for each plot whose beat timer is up and whose pacing gate is open, code builds the list of **allowed transitions** from the plot's state machine. The model gets the world summary plus that list and returns:

```json
{ "choice": "Complication:VexSighted", "reason": "Mara has grieved for two weeks; the raider resurfacing now turns grief into purpose." }
```

The model **only chooses and explains**. Code owns state, quests, rewards and threat points. If the call fails or the choice is invalid, code falls back to weighted random. This spec does not change any of that.

### Non-goals

- Training a narration/prose model (bulletins, handoff scenes). That's a possible later track; same pipeline, different task.
- Large judge models (12B–31B). The director has to fit the Minimum tier and answer in under 10 s, so we train small models. Big models are only useful here as an optional alternative teacher (§4.4).
- Replacing grammar-constrained JSON. Grammar stays on in production; training just makes the model's *choices* better.

### Success criteria (summary; full bars in §7)

A fine-tuned model ships as a preset only if, on the held-out test set, it:

1. Picks an allowed option 100% of the time with grammar, ≥98% without.
2. Agrees with Claude's top choice at least 85% as often as Claude agrees with itself.
3. Beats the same base model with prompting alone by ≥10 points of top-1 agreement.
4. Invents no facts in `reason` (≤1 per 50 outputs).
5. Answers in under 10 s on John's GPU at the Minimum tier (target under 3 s).

If it misses, the prompted base model stays the default. Training is an upgrade, not a dependency.

---

## 1a. Target hardware

All training, labelling with a local teacher, and inference run on John's PC: **NVIDIA RTX 5060 Ti, 16 GB VRAM** (Blackwell).

| Job | Fits in 16 GB? | Notes |
| --- | --- | --- |
| Director inference, 2B/4B at Q4_K_M | Easily | Leaves room for the game; target p50 <3 s |
| QLoRA SFT on ~4B | Yes | Comfortable at seq length ≤4k, batch 2–4 with accumulation |
| QLoRA SFT on ~2B | Yes | Plain bf16 LoRA (no 4-bit) is also an option and may train slightly better |
| DPO on ~4B (policy + reference) | Yes, with care | Use Unsloth's DPO path, which avoids holding a separate reference model in memory |
| Local teacher, ~12–14B dense at Q4–Q5 | Yes | Recommended local teacher size |
| Local teacher, ~26B-class MoE at Q4 | Borderline | May need partial CPU offload; slower but workable for offline labelling |
| Local teacher, ~31B dense | No (fully on GPU) | Don't use |

**Blackwell requirement:** the training environment must use a PyTorch build for CUDA 12.8 or newer; older CUDA builds don't support this card. Pin it in `pyproject.toml` and have the scaffold step print the detected GPU, compute capability and CUDA version at startup, and fail with a clear message if they don't match.

Don't run training and RimWorld at the same time; training uses the whole card.

---

## 2. When to run it

| Step | Can start after | Why |
| --- | --- | --- |
| Build the pipeline (§3–§6) | Stage 0 harness exists | Reuses the snapshot format, prompt templates and scoring |
| First real training run | Stage 4 transition set is drafted in XML | Training on transitions that don't exist yet is wasted work |
| Retrain | Any change to arc templates, transitions or the director prompt | Model learns the option vocabulary; new options need new data |

Before Stage 4, use the pipeline on the three arcs already specified (Nemesis, Silent Press, Empire states) as a dry run.

---

## 3. Repository additions

Training is Python (Unsloth/TRL ecosystem). Everything else stays C#/.NET 10. The Python side never ships with the mod.

```
training/
  README.md                 # how to run each step end to end
  pyproject.toml            # pinned deps; uv or pip
  config/
    director.yaml           # all paths, models, hyperparams, teacher settings
  rimchron_train/
    snapgen/                # synthetic snapshot generator
    label/                  # teacher labelling (Claude or local teacher)
    dataset/                # build, dedupe, split, augment
    sft.py                  # QLoRA SFT
    dpo.py                  # optional preference stage
    export.py               # merge LoRA → GGUF → Ollama Modelfile
  data/                     # gitignored except manifests
    snapshots/{synthetic,exported,handwritten}/
    labels/
    splits/
  runs/                     # gitignored; checkpoints, logs
harness/
  candidates.yaml           # add fine-tuned models here (no code change)
prompts/
  director/v{N}.txt         # single source of truth for the director prompt
```

**Rule:** the training code reads the prompt from `prompts/director/vN.txt` — the same file the mod and harness use. Never copy the prompt into Python. Each dataset and model records the prompt version it was built with; the harness refuses to evaluate a model against a different prompt version.

---

## 4. Data

### 4.1 Example format

One JSONL line per decision:

```json
{
  "id": "syn-01822-nemesis-hook",
  "source": "synthetic",            // synthetic | exported | handwritten
  "prompt_version": 3,
  "snapshot_id": "syn-01822",
  "plot": { "id": "nemesis-01", "template": "Nemesis", "beat": "Hook", "daysInBeat": 11 },
  "allowed": [
    { "id": "Complication:VexSighted",      "desc": "The raider is seen at a nearby outpost" },
    { "id": "Complication:ImperialBounty",  "desc": "The Empire posts a bounty on the raider" },
    { "id": "Wait",                         "desc": "Hold this beat a few more days" }
  ],
  "messages": [ /* rendered system + user turns, from prompts/director/vN.txt */ ],
  "teacher": {
    "model": "claude-...",
    "samples": 5,
    "choice_counts": { "Complication:VexSighted": 4, "Wait": 1 },
    "scores": { "Complication:VexSighted": 4.6, "Complication:ImperialBounty": 3.1, "Wait": 2.0 },
    "best": "Complication:VexSighted",
    "reason": "…",
    "agreement": 0.8
  }
}
```

Transition descriptions (`desc`) come from the XML defs, so the model reads meanings, not just IDs.

### 4.2 Snapshot sources

The 20 hand-written Stage 0 snapshots are not enough to train on. Three sources:

| Source | Target count | Use |
| --- | --- | --- |
| **Synthetic** — generated by `snapgen` | 3,000–5,000 decisions | Bulk of training data |
| **Exported** — from real saves via the Stage 1 exporter | 200+ decisions, grows over time | Train + most of the test set |
| **Hand-written** — Stage 0 set plus targeted edge cases | 50–100 | Test set only |

**Synthetic generator requirements:**

- Builds valid snapshots in the Stage 0 schema from seeded random draws: day/season, colony size, wealth, mood, pawns (traits, backstories, relations drawn from real RimWorld defs lists), tales, factions and plot states.
- Covers every arc template × every beat × colony condition (thriving, strained, desperate) roughly evenly. Report coverage as a table after each run.
- Builds `allowed` from the real state machine definitions (parsed from `Defs/` XML), not a hard-coded list.
- Deliberately produces cases where **Wait is right** (protagonist just lost a spouse yesterday, colony recovering from a raid) and cases where Wait is wrong (beat stalled 30+ days).
- Name pools are large and varied so the model can't memorise names. Some pawn names and tale text include instruction-like strings (e.g. a nickname like "Ignore-the-list") so we can test that the model treats the snapshot as data.
- Fully reproducible from a seed.

### 4.3 Labelling with Claude (teacher)

> **Terms: cleared (2026-11-22).** John has confirmed that using Claude's labels to train this narrow game-director model is allowed. Claude is the default teacher. The pipeline still works with the local teacher (§4.4) by config alone, for offline runs or cost.

For each decision:

1. Send the director prompt **plus a teacher rubric** (separate file, `prompts/director/teacher_vN.txt`) asking Claude to score every allowed option 1–5 on: fits what just happened in this colony, pacing (tension vs. breathing room), uses the protagonist's traits/relations/backstory, sets up a good next beat, and doesn't repeat the last beat type. Then pick the best and write a reason of ≤30 words that names only things in the snapshot.
2. Sample **5 times** at temperature ~0.7, shuffling the order of `allowed` each time (removes position bias from the labels).
3. Store per-option mean scores, choice counts and agreement (share of samples matching the most common pick).
4. **Claude self-agreement** across all labelled items = the ceiling used in §7.

Filtering:

- Agreement ≥ 0.6 → train on the majority choice.
- Agreement < 0.6 → keep out of SFT; keep for DPO only if one option scores ≥1.0 below the best.
- Drop any label whose `reason` names an entity not in the snapshot (automatic check against the snapshot's entity list).

Use the Batch API for cost. Cache every request/response by content hash so re-runs never pay twice. Log total spend per run.

### 4.4 Local teacher (alternative)

Same `label/` interface, pointed at a ~12–14B dense open model at Q4–Q5 through Ollama (fits fully in 16 GB; see §1a). A ~26B-class MoE with partial CPU offload is an option if it labels noticeably better on a 50-item sample. Lower quality ceiling, but free and fully offline. Config switch: `teacher: claude | local` (default `claude`). If both are available, label the test set with Claude and report how often the local teacher agrees with it.

### 4.5 Splits

- Split **by snapshot seed**, never by row, so no snapshot appears in two splits.
- **Train:** synthetic + ~70% of exported.
- **Val:** 10% of train sources, for early stopping.
- **Test (frozen):** all hand-written + ~30% of exported + 200 synthetic from a held-out seed range. Test is never used for tuning and is versioned; changing it bumps its version.

### 4.6 Augmentation

- Shuffle `allowed` order for every training example (3 orderings per decision).
- Rename transition IDs in 20% of copies (e.g. `Complication:VexSighted` → `C2`) with `desc` unchanged, so the model reads descriptions rather than memorising IDs.
- Add ~5% of examples where the snapshot mentions an option that is *not* in `allowed`; the target is still an allowed option.

---

## 5. Training

### 5.1 Base models

The mod uses Gemma 4 only (Qwen and Raon-1 nano were dropped on 2026-11-22). Train in this order:

| Priority | Base | Tier |
| --- | --- | --- |
| 1 | Gemma 4 E4B (instruct) | Recommended |
| 2 | Gemma 4 E2B (instruct) | Minimum |

Use the official Gemma 4 chat template for both training and the Ollama Modelfile; a mismatch between the two is the most common cause of a fine-tune that looks good in Python and fails in the mod.

Use instruct checkpoints. Turn any "thinking" mode off for the director; the reason field is the only explanation we want, and thinking tokens cost latency.

### 5.2 Stage A — SFT (always)

QLoRA with Unsloth + TRL `SFTTrainer`.

- Input: rendered `messages`. Target: the JSON object only (`choice` + teacher `reason`). Loss on the assistant turn only.
- Starting hyperparameters (put all in `director.yaml`): LoRA r=16, alpha=32, dropout 0.05, all linear layers; lr 2e-4, cosine, warmup 3%; 2–3 epochs; max seq length set from the 99th percentile of prompt length; effective batch 16.
- Early stop on val top-1 agreement (computed by actually generating, not just val loss).
- Log to a local CSV/TensorBoard; no cloud tracking required.
- Must fit in 16 GB (RTX 5060 Ti): detect available VRAM at start, print it, and pick batch/accumulation to fit. Use gradient checkpointing (Unsloth mode) by default. Fail early with a clear message if it won't fit.

### 5.3 Stage B — DPO (only if SFT misses the bar)

- Pairs: chosen = teacher's best option + reason; rejected = (a) the teacher's lowest-scored allowed option, (b) the SFT model's wrong picks on train snapshots, (c) a correct choice with a reason that invents a fact.
- Only use pairs where the score gap is ≥1.0.
- Start from the SFT adapter; β=0.1; 1 epoch. Keep if test agreement improves and nothing in §7 gets worse.

### 5.4 Reproducibility

Every run writes `runs/<run-id>/manifest.json`: git commit, prompt version, dataset hashes, base model and revision, config, seed, final metrics. A run can be reproduced from its manifest alone.

---

## 6. Export and integration

1. Merge the LoRA into the base and export GGUF at **Q4_K_M** (default) and **Q8_0**.
2. Generate an Ollama `Modelfile` with the base model's chat template and `temperature 0.2`. Name: `rimchron-director-{base}-v{N}`.
3. Smoke test: load in Ollama, run 20 test decisions through the **C# LLM client** (not Python), confirm grammar-constrained JSON works.
4. Add the model to `harness/candidates.yaml`. No code changes in the mod: the director endpoint in settings simply points to the new model name.
5. Mod settings get a **Director model** preset list: "Base (prompted)" and "Rim Chronicles Director vN" per tier. Fallback behaviour is unchanged.

Distribution (later, optional): publishing weights for players needs the Gemma licence terms checked for redistributing fine-tuned weights. Until then models are for John's machine only.

---

## 7. Evaluation

Runs through the Stage 0 harness on the frozen test set, comparing: prompted base, SFT, SFT+DPO, Claude (reference).

| Metric | How | Ship bar |
| --- | --- | --- |
| JSON validity | Parses, matches schema | 100% with grammar; ≥98% without |
| Allowed choice | `choice` ∈ `allowed` | 100% with grammar; ≥98% without |
| Top-1 agreement | Matches teacher's best | ≥85% of Claude self-agreement |
| Top-2 agreement | In teacher's top two | ≥95% |
| Mean regret | Teacher's score of best minus score of chosen | ≤0.4 |
| Lift over base | Top-1 vs. prompted base, same size | ≥+10 points |
| Wait accuracy | On Wait-correct and Wait-wrong subsets | ≥85% each |
| Position bias | Same item, 3 orderings: share with identical choice | ≥90% |
| ID robustness | Renamed-ID copies vs. originals: same choice | ≥90% |
| Invented facts | Automatic entity check + Claude judge on `reason` | ≤1 per 50 |
| Injection resistance | Instruction-like names: output still valid and on-task | 100% |
| Latency | p50 / p95 on John's GPU, Q4_K_M | p95 <10 s; target p50 <3 s |

Report per arc template and per beat, so a model that's good overall but bad at, say, Empire Act 2 is visible.

**Human check:** a small blind review tool (CLI or local HTML page) shows John 50 random test decisions with two anonymised answers (fine-tuned vs. Claude). He picks the better one or "same". Ship only if the fine-tuned model is picked or tied in ≥40% of cases.

**In-game check:** one 200-day Dev Mode game (Stage 4 exit test) with the fine-tuned director. The decision log should still read like a sensible story outline.

---

## 8. Build order for Claude Code

Each step ends with tests passing and a short note in `training/README.md`.

1. **Scaffold** `training/` with pinned deps, `director.yaml`, and a CLI (`python -m rimchron_train <step>`).
2. **Prompt loader** that renders `prompts/director/vN.txt` exactly as the C# client does. Add a cross-language test: C# and Python render the same snapshot to byte-identical prompts.
3. **Snapshot generator** with seed, coverage report and schema validation against the Stage 0 schema.
4. **Transition loader** that parses arc state machines from `Defs/` XML (stub with the three known arcs until Stage 4).
5. **Labeller** with teacher switch (`claude` default, `local` alternative), sampling, shuffling, caching, spend log and filters. Run a 50-item pilot with Claude first and check spend before labelling the full set.
6. **Dataset builder:** dedupe, split by seed, augment, write JSONL + manifest.
7. **SFT trainer** with VRAM check and generate-based early stopping.
8. **Exporter** to GGUF + Modelfile, plus the C# smoke test.
9. **Harness metrics** from §7 that don't already exist (top-k, regret, Wait, position bias, ID robustness, injection) and the per-arc report.
10. **Blind review tool.**
11. **DPO trainer** — build only if step 9 shows SFT missing the bar.

### Rules (add to `CLAUDE.md`)

- Python lives only under `training/`; nothing in it is a runtime dependency of the mod.
- The prompt has one source: `prompts/director/`. Never inline it.
- Never train or tune on the test split. Never edit the test split without bumping its version.
- All randomness is seeded and recorded in the manifest.
- Cache every teacher call; never re-label something already cached.
- A model only becomes a preset after its §7 report is committed to `harness/reports/`.

---

## 9. Open questions

1. **One model or two:** if a later prose-model track happens, test whether one adapter can do both director and narration before training two.
