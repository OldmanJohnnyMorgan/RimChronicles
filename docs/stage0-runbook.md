# Stage 0 runbook: the real model evaluation (M5)

This is how to run the Stage 0 evaluation on the reference PC (RTX 5060 Ti, 16 GB). It has to run there, not in the codespace: the local models need the GPU, and `harness/models.json` points at `localhost` servers on the PC.

Commands are for PowerShell on Windows. On Linux the commands are the same apart from setting the environment variables.

## 1. One-time setup

Follow `docs/local-setup.md` first. It installs the driver, tools, code, Ollama and its models, the optional Raon-1 nano server and the API key, and ends with the same `check` as step 2.

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

One repeat over the first nine snapshots, judged straight away:

```powershell
dotnet run --project harness/RimChronicles.Eval -- run --repeats 1 --snapshots "snap-0*" --judge
```

Open the `summary.html` it prints at the end and check that every model produced real outputs (the *Fell back* column should be low) before spending hours on the full run.

## 4. Full run (a few hours; overnight is easiest)

```powershell
dotnet run --project harness/RimChronicles.Eval -- run --judge
```

This is 20 snapshots, 3 repeats and 8 models: about 1,500 outputs. The judge reads about 550 of them (bulletins and handoff scenes). The Claude reference and the judge together should cost roughly $5–15.

- **Safe to interrupt.** Ctrl+C stops the run; every finished answer is cached, so running the same command again carries on where it stopped.
- **Don't game during the run.** Other programs on the GPU change the latency numbers and the VRAM readings.

## 5. Hand spot-check

```powershell
dotnet run --project harness/RimChronicles.Eval -- spotcheck latest
```

This writes `spotcheck.csv` into the run folder: about 55 outputs with no model names and no judge scores. Open it in Excel. For each row, read the briefing and the output, then fill in:

- `human_invented_count`: how many names, numbers or events in the output aren't in the briefing
- `human_overall`: 1–5, using the rubric in `prompts/judge/v1/system.md`

Save it as CSV, then run the same command again to see how often the judge agreed with you.

## 6. Send me the results

The `results/` folder is git-ignored, so push the finished run folder on a branch. The response cache lives in `results/cache`, outside the run folder, so it isn't included:

```powershell
git checkout -b m5-results
git add -f results/<runId>
git commit -m "Stage 0 results: run <runId>"
git push -u origin m5-results
```

Replace `<runId>` with the run's folder name, for example `20261122-031500`. Then tell me, and I'll analyse the run and write up the model picks.
