# Stage 0 runbook: the real model evaluation (M5)

This is how to run the Stage 0 evaluation on the reference PC (RTX 5060 Ti, 16 GB). It has to run there, not in the codespace: the local models need the GPU, and `harness/models.json` points at `localhost` servers on the PC.

Commands are for PowerShell on Windows. On Linux the commands are the same apart from setting the environment variables.

## 1. One-time setup

Follow `docs/local-setup.md` first. It installs the driver, tools, code, Ollama and its models and the API key, and ends with the same `check` as step 2.

## 2. Preflight check

```powershell
dotnet run --project harness/RimChronicles.Eval -- check
```

`check` makes two small calls per model (one director decision, one news bulletin) and prints a table: server, quantization, VRAM, and how each call went. Fix every **PROBLEM** line before going on. The **WARNING** lines are the things to read:

- *thinking is set Off but the model still thought*: the server ignores the setting that switches the director's thinking off. Send me the output; the right field differs between servers.
- *invalid JSON with grammar on*: the server may ignore the JSON schema. We'd then set `"supportsGrammar": false` for that model.
- *runs on the CPU* or *over the 13 GB budget*: the model is too big for the card as configured. Pull a smaller quantization.

Two Claude calls are included, which cost a few cents.

## 3. Trial run (about 15–30 minutes)

One repeat over the first nine snapshots for the three local models, judged straight away. The Claude reference is left out: its outputs only need regenerating for a full run, and it costs about as much as the judge.

```powershell
dotnet run --project harness/RimChronicles.Eval -- run --repeats 1 --snapshots "snap-0*" --models gemma4-e2b,gemma4-e4b,gemma4-12b --judge
```

Open the `summary.html` it prints at the end and check that every model produced real outputs (the *Fell back* column should be low) before spending hours on the full run.

### Prompt rounds

When a trial shows a prompt problem, change the prompt (a new version folder under `prompts/`), run the same trial again and compare it with the last one:

```powershell
dotnet run --project harness/RimChronicles.Eval -- run --repeats 1 --snapshots "snap-0*" --models gemma4-e2b,gemma4-e4b,gemma4-12b --judge
dotnet run --project harness/RimChronicles.Eval -- report latest --compare <previous runId>
```

`summary.html` then shows the change in every number next to the old one. The judge (v3) splits invented facts into *major* ones (a new person, place, number, quote or event, a character put at a past event they weren't at, or a contradiction), held to at most 1 per 10 outputs, and *minor* ones (an added cause, status or detail about a briefed fact), allowed up to 5 per 10. Word choice is never counted, and handoff scenes may stage the moment (gestures, ordinary objects, the body, the weather) without it counting. Each round takes about 20–30 minutes and costs about $1.50 for the judge (Claude Sonnet 5.5). Only compare runs scored by the same judge model and judge prompt version.

The local models sample at temperature 0.4 (set in `harness/models.json`). To see whether that setting helps, run the same trial with the servers' own defaults and compare the two runs:

```powershell
dotnet run --project harness/RimChronicles.Eval -- run --repeats 1 --snapshots "snap-0*" --models gemma4-e2b,gemma4-e4b,gemma4-12b --judge --temperature default
```

## 4. Full run (a few hours; overnight is easiest)

```powershell
dotnet run --project harness/RimChronicles.Eval -- run --judge
```

This is 20 snapshots, 3 repeats and 4 models (Gemma 4 E2B, E4B and 12B, plus the Claude reference): 756 outputs. The judge reads 276 of them (bulletins and handoff scenes). The Claude reference (Opus 5.5) and the judge (Sonnet 5.5) together should cost roughly $5–8.

- **Safe to interrupt.** Ctrl+C stops the run; every finished answer is cached, so running the same command again carries on where it stopped.
- **Don't game during the run.** Other programs on the GPU change the latency numbers and the VRAM readings.

## 5. Hand spot-check

```powershell
dotnet run --project harness/RimChronicles.Eval -- spotcheck latest
```

This writes `spotcheck.csv` into the run folder: about 55 outputs with no model names and no judge scores. Open it in Excel. For each row, read the briefing and the output, then fill in:

- `human_invented_count`: how many names, numbers, quotes or events in the output aren't in the briefing, plus claims that contradict it. These are the judge's *major* invented facts; leave out minor additions such as an implied cause, and never count word choice or, in handoff scenes, staging of the moment.
- `human_overall`: 1–5, using the rubric in `prompts/judge/v3/system.md`

Save it as CSV, then run the same command again to see how often the judge agreed with you.

## 6. Send me the results

The `results/` folder is git-ignored, so push the finished run folder on the `m5-results` branch. Commit from a second folder (a git worktree) so the main checkout stays on `main`: a run started while the checkout is on `m5-results` tests that branch's old prompts. Each run's `run.json` records the commit it ran from. The response cache lives in `results/cache`, outside the run folder, so it isn't included.

```powershell
git worktree add ..\rc-results m5-results     # once
cd ..\rc-results
git pull
Copy-Item -Recurse ..\RimChronicles\results\<runId> results\<runId>
git add -f results\<runId>
git commit -m "Stage 0 results: run <runId>"
git push
cd ..\RimChronicles
```

Replace `<runId>` with the run's folder name, for example `20261122-031500`. To re-judge a run after a judge prompt change, without regenerating anything, run `dotnet run --project harness/RimChronicles.Eval -- judge <runId>`; it rewrites `judge.csv` and `summary.html`. Then tell me, and I'll analyse the run and write up the model picks.
