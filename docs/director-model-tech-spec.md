# Director model training: technical spec and plan

**For:** `docs/SPEC-director-model.md` (the training spec). **Date:** 2026-11-22. **Status:** draft for review.

This turns the training spec into a build plan that fits the repo as it is after Stage 5. Where this document departs
from the training spec, it says why. The parent design spec still wins over both.

## 1. Review: what changes and why

| # | Training spec says | Repo reality | Decision |
| --- | --- | --- | --- |
| R1 | Prompt is `prompts/director/vN.txt`; Python renders it, with a C#/Python byte-identical test | The prompt is a folder (`prompts/director/v1/`: `system.md`, `user.md`, `meta.json`, `schema.json`). Rendering runs through `WorldSummarizer.ForDirector`, which fits the world summary to a per-tier token budget, resolves pawn ids to names, and more | **C# renders, Python never does.** A harness command exports each case with its rendered `system` and `user` text. Python only applies the Gemma chat template. Byte-identical by construction; no second renderer to keep in step |
| R2 | Allowed transitions parsed from `Defs/` XML state machines | Transitions are C# code in Core: `NemesisArc.Transitions`, `LostFamilyArc.Transitions`, `EmpireArc.Transitions` (the plot plus the `EmpireSaga`). Silent Press only ever offers one option, so the model never sees it | **The case generator is C#** and calls the real `Transitions()` on generated plot states, so `allowed` is exactly what the game would offer. Arcs with real choices: Nemesis, Lost Family, Awakened Empire |
| R3 | Each option has a `desc` from the defs | Options are ids only (`Choice:RescueSaskia`, `Empire:DefendAlly`). The prompt lists ids | **Add `Desc` to `PlotTransition`**, written per arc in Core, and show it in **director prompt v2**: `- Choice:RescueSaskia: offer a Rescue quest to free her`. Helps the prompted base model too, and makes ID-renaming augmentation meaningful |
| R4 | The decision has a `plot` with `daysInBeat` | The snapshot lists every active plot, with no marker for which one is deciding | **v2 names the deciding plot** and its days in the current beat; the snapshot gains a `director` block (`plotId`, `daysInBeat`) |
| R5 | Snapgen, labeller, dataset builder in Python | The harness already has the Anthropic provider (official SDK), a request cache keyed by content, Ollama providers, snapshot loading and validation, and run folders | **Data pipeline in C#** (harness commands), **training in Python** (`training/`, Unsloth/TRL only). Python stays small: SFT, DPO, export |
| R6 | Teacher samples 5× at temperature ~0.7 | Opus 5.5 and Sonnet 5.5 reject non-default sampling parameters, and Opus 5.5 can't turn thinking off | **Sample diversity comes from the 5 shuffled orderings** (needed anyway against position bias). Opus runs at effort `low`; thinking tokens are counted in the cost estimate. Self-agreement is measured the same way |
| R7 | `harness/candidates.yaml` | Models live in `harness/models.json` | Add fine-tuned models to `models.json` |
| R8 | Settings get a "Director model" preset | Since PR #66 the mod has a main model and an optional prose model | **Add a director model override** (blank: the main model), like the prose model. `PlotDriver` uses it |
| R9 | Train Gemma 4 E2B/E4B; export GGUF to Ollama | The E-series uses per-layer embeddings. Unsloth fine-tuning, `llama.cpp` GGUF conversion and Ollama import of a *fine-tuned* E-model are unverified on this setup | **A feasibility spike comes first** (D0), before any labelling money is spent |
| R10 | Wait right/wrong subsets judged by the teacher | Need ground truth to score "Wait accuracy" | The generator tags each case `waitExpected: true / false / null` by construction; the teacher's label is checked against it, and disagreements are reported |
| R11 | Exported data from real saves via the Stage 1 exporter | The exporter dumps the world, not director decisions | A Dev Mode setting **records every director request** (snapshot and options) to a folder, for the `exported` source |

Python training runs on the PC (RTX 5060 Ti) only. The codespace has no GPU: its Python tests cover data handling with
no torch import.

## 2. Architecture

```
C# (codespace + PC)                                      Python (PC only, training/)
--------------------------------------------------      --------------------------------
Core: PlotTransition.Desc, arcs write descriptions
harness: director-gen  -> cases/*.json (snapshot + plot state + allowed + waitExpected + seed)
harness: director-label -> labels/*.jsonl (teacher scores, picks, reason; cached; spend log)
harness: director-dataset -> splits/{train,val,test}.jsonl  --->  sft.py (Unsloth QLoRA)
         (rendered system/user text, target JSON,               dpo.py (only if needed)
          augmentations, manifest)                              export.py -> GGUF + Modelfile
harness: run --models rimchron-director-e4b-v1  <-----------   (Ollama on the PC)
harness: director-report -> harness/reports/<run>.md (the §7 metrics, per arc and beat)
harness: spotcheck (blind review, existing command extended)
mod: settings "Director model" -> PlotDriver
```

One source of truth each: prompt text (`prompts/director/`), rendering (`WorldSummarizer`), transitions (Core arcs),
teacher rubric (`prompts/director-teacher/`).

## 3. Data formats

**Case** (`training/data/cases/<source>/<id>.json`): a Stage 0 snapshot plus

```json
"director": { "plotId": "nemesis-1", "daysInBeat": 11, "waitExpected": false, "seed": 1822, "arc": "Nemesis", "beat": "Hook" }
```

and `allowedTransitions` filled from the arc code (with descriptions). Validated against `harness/schema/snapshot.schema.json`
(extended for `director`).

**Label** (`labels/<teacher>/<case-id>.jsonl`, one line per sample): order shown, per-option scores 1–5, pick, reason,
token usage. Aggregated per case: mean scores, choice counts, best, agreement. Filters as the training spec §4.3.

**Training row** (`splits/*.jsonl`): `{ id, seed, arc, beat, waitExpected, promptVersion, system, user, allowed[], target: {choice, reason}, augment }`.
Python wraps `system`/`user`/`target` in the Gemma 4 chat template; nothing else is rendered in Python.

**Manifests**: every dataset and run writes `manifest.json` (git commit, prompt and rubric versions, seeds, file hashes,
teacher model, counts). The harness refuses a model whose manifest names another director prompt version.

## 4. Teacher cost (estimate)

Assumptions: ~2,000 input tokens per labelling call (system, rubric, snapshot) and ~600 output tokens (scores, reason,
low-effort thinking); 5 samples per case; Batch API (half price). Prices from the API docs (2026-11): Opus 5.5 $4/$20,
Sonnet 5.5 $2/$10, Haiku 5.5 $0.10/$0.50 per million tokens.

| Labelling job | Calls | Opus 5.5 | Sonnet 5.5 | Local Gemma 12B |
| --- | --- | --- | --- | --- |
| Pilot, 50 cases | 250 | ~$2.50 | ~$1.25 | free |
| Test set, ~500 cases | 2,500 | ~$25 | ~$13 | free |
| Full training set, 4,000 cases | 20,000 | ~$200 | ~$100 | free (~1–2 days of GPU) |

Real costs come from the pilot, which logs actual tokens before anything larger runs. The labeller prints an estimate
and needs `--confirm` above $5.

## 5. Build plan

Each milestone is a PR with tests passing, like the stages. GPU steps run on the PC with exact PowerShell commands in
`training/README.md`.

| # | Milestone | Where | Delivers | Done when |
| --- | --- | --- | --- | --- |
| **D0** | Feasibility spike | PC | `training/` scaffold (pinned deps, CUDA ≥12.8 torch, GPU/VRAM check), a 20-example LoRA on Gemma 4 E2B, merge, GGUF, Ollama import, one grammar-constrained call through the C# client | The fine-tuned E2B answers valid JSON in Ollama, under 3 s. **Go/no-go for the whole track** |
| D1 | Director prompt v2 | codespace | `PlotTransition.Desc` for every option in the three arcs; the deciding plot and days-in-beat in the prompt; snapshot `director` block; `validate` and tests | Free harness run (E2B, E4B, 12B) at least as good as v1 on choice validity; this is the **prompted-base baseline** |
| D2 | Case generator | codespace | `director-gen --seed N --count M`: seeded pawns, factions, tales and plot states for every arc × beat × colony mood; real `Transitions()`; Wait-right/Wait-wrong cases; injection-style names; schema validation; coverage table | 5,000 cases from one seed, reproducible byte-for-byte, even coverage |
| D3 | In-game decision recording | codespace, then PC | Dev Mode setting to record each director request; a harness command to import them as `exported` cases | Cases from a real save load and label like synthetic ones |
| D4 | Teacher labelling | codespace, then PC | `prompts/director-teacher/v1` (rubric); `director-label` with `--teacher claude|local`, Batch API, 5 shuffled orderings, cache, spend log, filters, `--confirm` above $5 | 50-case pilot labelled; real cost and self-agreement known |
| D5 | Dataset builder | codespace | `director-dataset`: split by seed, frozen versioned test set, augmentation (3 orderings, ID renaming on 20%, 5% decoy mentions), manifest | Splits disjoint by seed (tested); test set hash recorded |
| D6 | SFT | PC | `python -m rimchron_train sft`: Unsloth QLoRA per `director.yaml`, generate-based early stopping on val top-1, CSV log, run manifest | E4B and E2B adapters trained |
| D7 | Export and integration | PC + codespace | `export.py` (merge, GGUF Q4_K_M and Q8_0, Modelfile with the Gemma 4 template and temperature 0.2); `models.json` entries; **Director model** setting in the mod | The fine-tuned model runs in the harness and in the game |
| D8 | Metrics and report | codespace | `director-report`: JSON/choice validity, top-1/top-2 agreement, regret, lift over base, Wait accuracy, position bias, ID robustness, invented facts, injection, latency; per arc and beat; committed to `harness/reports/` | A report for base, SFT and Claude on the frozen test set |
| D9 | Blind review | codespace | `spotcheck` extended: 50 decisions, two anonymised answers (fine-tuned vs Claude), better / same | John's review recorded with the report |
| D10 | DPO (only if D8 misses a bar) | PC | Pairs from teacher scores (gap ≥ 1.0), SFT errors, and invented-fact reasons; β 0.1, one epoch | Kept only if nothing in the report gets worse |
| D11 | In-game check | PC | 200-day Dev Mode game with the fine-tuned director | The director log reads like a sensible story outline |

D1, D2, D3 and D5 need no GPU and no API spend, so they can run alongside the Stage 5 proof run. D0 needs the PC and
should happen early, since a no-go there makes the rest moot.

## 6. New rules for `CLAUDE.md`

The training spec's rules, adjusted to R1 and R5:

- Python lives only under `training/` and is never a runtime dependency of the mod.
- The director prompt has one source (`prompts/director/`) and one renderer (`WorldSummarizer`, C#). Python never renders prompts.
- Never train or tune on the test split; editing it bumps its version.
- All randomness is seeded and recorded in a manifest.
- Every teacher call is cached; nothing already labelled is labelled again. Claude labelling above $5 needs `--confirm`.
- A model becomes a preset only after its report is committed to `harness/reports/`.

## 7. Risks

| Risk | Effect | Mitigation |
| --- | --- | --- |
| Fine-tuned Gemma 4 E-models don't convert to GGUF or load in Ollama | The track can't ship | D0 passed (§11): merged GGUF loads. Ollama refuses LoRA adapters, so merged GGUF is the only route; fallback is the prompted base (training stays an upgrade) |
| Windows-native Unsloth on Blackwell | Install pain, slow iteration | Recommend WSL2 with CUDA 12.8; document it in `training/README.md` and `docs/local-setup.md` |
| Synthetic cases unlike real games | Model good on synthetic, worse in play | D3 exported cases in train and test; per-source metrics in the report |
| Teacher too inconsistent (low self-agreement) | Weak labels | Pilot measures it first; rubric tuned on the pilot before the full run |
| Option vocabulary changes (new arcs, Stage 5 fixes) | Model learned stale ids | Retrain trigger: any change to arc transitions or the director prompt (training spec §2); descriptions and ID renaming reduce id memorising |
| API credits | Labelling stalls | Local teacher for the bulk, Claude for the test set (see §4) |

## 8. Recording real decisions (D3)

1. In game, with Development mode on: Options → Mod settings → Rim Chronicles → **Dev: record director decisions for
   training**. Every director decision with a choice is saved as a case (with or without a model set up), to
   `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\RimChronicles\director-cases\`.
2. Play as usual. Each file is one decision, named `dir-game-<colony>-day<N>-<plot>.json`.
3. Import them on the PC, then push nothing: the cases stay local (git-ignored); only the manifest is committed.

```powershell
dotnet run --project harness/RimChronicles.Eval -- director-import --from "$env:USERPROFILE\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\RimChronicles\director-cases"
```

## 9. Labelling pilot (D4, 2026-11-22)

Teacher: Claude Code (Opus 5.5) in session, on the subscription (option B). Pack `training/data/labels/pilot`: 50 cases
from seed 1, round-robin over the 7 arc/beat groups, 10 shown twice with the options reshuffled; teacher prompt
`director-teacher/v1`.

- All 60 labels pass `director-labels-check` (every option scored, best = top score, reasons ≤30 words, no names outside
  the briefing). Self-agreement 10/10, inflated: the repeats were labelled in the same session.
- Wait: picked in 20/20 wait-right cases, avoided in 7/7 wait-wrong ones, after one fix. The labels offered a treaty or
  a governorship to reeling colonies (relief, not piling on), and the generator now leaves `waitExpected` open there
  (generator v4); the rubric says so explicitly.
- Generator fixes the pilot found: empty tale objects, "Maimed" vs "MaimedColonist", friendly factions raiding,
  duplicate side plots, contradictory background events (generator v2–v3).
- Base-model gap this targets (director v2 run `20261122-052929`): in a desperate colony just after a raid, every Gemma
  acted where Claude waited.

## 10. Dataset (D5)

`director-dataset` renders each labelled case through the **director prompt** (the student's, `prompts/director/`) at
a tier's budget and writes `training/data/splits/<tier>/{train,val,test}.jsonl` plus a manifest. Each row has the exact
`system` and `user` text the game would send, the allowed ids, and the `target` JSON (plain escaping). Python only
wraps these in the Gemma chat template.

- **Splits:** test = generator seed **9001** (500 cases, test version 1) and 30% of in-game colonies; val = 10% of the rest
  by a hash of the case id (in-game cases: of the colony); train = the rest. A case and its copies stay in one split.
- **Checks:** a label whose case changed since labelling (best not allowed, or different options) is left out and
  fails the build; a case labelled in two folders keeps its first label.
- **Augmentation (train only):** the case plus two reshuffled copies; ids renamed to C1, C2… in ~20% of copies; a decoy
  mention of a not-allowed option in ~5%.
- **Tiers:** build once per tier trained (`--tier Minimum` for E2B, `Recommended` for E4B).

## 11. Feasibility spike (D0): passed

Run on the reference PC on 2026-11-22: 30 QLoRA steps on the pilot (44 train / 6 val cases, tier Minimum) on
`unsloth/gemma-4-E2B-it` (loaded as `unsloth/gemma-4-E2B-it-unsloth-bnb-4bit`), merged, Q4_K_M GGUF, `ollama create`,
then harness run `20261122-143935` (director task, 23 snapshots, grammar on and off).

| Bar | Result |
| --- | --- |
| Loads in Ollama | Merged GGUF: yes. LoRA adapter on `gemma4:e2b`: **refused** (Ollama 0.40.17: "LoRA adapters are no longer supported") |
| Valid JSON under grammar | 100% (and 100% without grammar) |
| Valid choice | 100% |
| Median latency | 0.2 s (p90 0.3 s); first call 8.1 s while the model loads |
| VRAM | 1.7 GB |

**Go.** Consequences for D6–D7:

- **Merged GGUF is the only export route.** Every trained director is merged, converted and quantised; there is no
  adapter-on-base option, so each tier ships as a full model file.
- **Conversion needs host RAM, not VRAM.** Converting E2B was killed with WSL's default memory; it passed with
  `.wslconfig` `memory=24GB`, `swap=32GB`. E4B will need at least as much.
- **Gemma 4 specifics:** turn markers are `<|turn>user` / `<|turn>model` (not Gemma 3's `<start_of_turn>`); Unsloth
  writes GGUFs to `merged_gguf/` plus a vision `mmproj` file the director doesn't need; torchvision must come from the
  same CUDA index as torch.

## 12. Bulk labels from the local teacher (D4b)

`director-label` has a model from `models.json` (default `gemma4-12b`) fill a pack in the hand-labelled format, so
`director-labels-check` and `director-dataset` treat both alike. Each call is grammar-constrained to a score for
exactly the shown options and a `best` among them, checked like a hand label (best = top score, ≤30 words, names from
the briefing), cached, and appended as it finishes; up to 3 attempts per item. Defaults: 4,000 cases round-robin over
arc and beat, 3 samples per case (not the training spec's 5, for GPU time), temperature 0.7. Cases already labelled in
another folder are skipped, so Claude's labels are never duplicated or outranked, and test-seed folders are refused.

- **Calibration first:** `--recheck training/data/labels/pilot` labels exactly the pilot's cases into `results/` and
  reports agreement with Claude, overall and per arc and beat, before the long run.
- **Wait cap (dataset):** at most 40% of training cases have Wait as the best answer (`--max-wait-share`), chosen by a
  hash of the id. Val and test keep the real mix (test: 65% Wait), so the report can compare against always-Wait.
- **Calibration v1 (2026-11-22):** 1.4 s per item (about 4.7 h for 4,000 cases × 3), 0 failures, but the 12B picked
  Wait in 1/50 pilot cases (Claude 23/50) and in 1/20 wait-right ones: it agreed with Claude on 20/27 when Claude
  acted and 1/23 when Claude waited, picking "high-stakes" beats right after deaths and raids. Its labels would have
  taught the student never to pause. Two fixes: **rubric v2** puts pacing first as explicit rules (reeling and young
  beat: Wait 5, harsh options at most 2, relief allowed), and `director-label` **rejects and re-asks** any label that
  contradicts the case's `waitExpected` (the generator's rule, which Claude's labels matched 20/20 and 222/222); open
  cases stay the teacher's call. `--no-wait-rule` turns the check off.
