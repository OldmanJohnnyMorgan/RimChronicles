# Rim Chronicles

RimWorld 1.7 mod adding a story mode narrated by a local LLM.

- Design spec: `docs/Rim Chronicles — Design Spec.md`
- Technical spec: https://claude.ai/artifact/AMTEFZMpdn9ELPEJd5biXB

## Commands

```bash
dotnet build RimChronicles.slnx          # builds everything; Game builds empty without a RimWorld install
dotnet test RimChronicles.slnx           # Llm, Core and harness tests; no RimWorld or GPU needed
dotnet run --project harness/RimChronicles.Eval -- --help   # Stage 0 eval harness (rimchron-eval)
dotnet run --project harness/RimChronicles.Eval -- validate  # snapshots, prompts and models.json agree
dotnet run --project harness/RimChronicles.Eval -- check     # preflight model servers: names, test calls, thinking off, VRAM
dotnet run --project harness/RimChronicles.Eval -- run --config harness/models.stub.json   # smoke run, no server
dotnet run --project harness/RimChronicles.Eval -- run --models gemma4-e4b --tasks director  # real run (needs the server)
dotnet run --project harness/RimChronicles.Eval -- run --judge     # generate, then judge prose with Claude (needs RIMCHRONICLES_ANTHROPIC_API_KEY)
# Since 2026-11-21 routine runs skip --judge and claude-ref to save API credits; Claude Code reviews raw/ outputs instead.
dotnet run --project harness/RimChronicles.Eval -- run --judge --temperature default   # local models at the server's default temperature
dotnet run --project harness/RimChronicles.Eval -- judge latest    # judge an existing run; rewrites summary.html
dotnet run --project harness/RimChronicles.Eval -- report latest --compare <runId>   # rebuild summary.html with deltas
dotnet run --project harness/RimChronicles.Eval -- spotcheck latest # blind 10% sample to hand-score; run again to score agreement
dotnet run --project harness/RimChronicles.Eval -- director-report --models gemma4-e2b --count 50   # D8: score models on the frozen director test set
dotnet run --project harness/RimChronicles.Eval -- director-review --models <id>   # D9: blind review page; then --answers <file> to score it
```

The real evaluation runs on the reference PC, not in the codespace: set the PC up with `docs/local-setup.md`, then follow `docs/stage0-runbook.md`.

Run output goes to `results/<runId>/` (`results.csv`, `summary.csv`, `run.json`, `raw/`, and after judging `judge.csv`, `judgments/`, plus `summary.html`). Pass bars live in `harness/passbars.json`; responses are cached in `results/cache/`, keyed by model settings, the Ollama weights digest (so a retrained model under the same name misses), the full request and the repeat number. Both are git-ignored.

Summarizer golden files live in `tests/RimChronicles.Tests/Golden/`. After an intended change to prompt text, run `UPDATE_GOLDEN=1 dotnet test`, then review the diff before committing.

To build the real mod, copy `Directory.Build.props.user.example` to `Directory.Build.props.user` and set the RimWorld and Harmony paths. In the codespace they point at `/workspaces/reference/dlls`, so the Game project builds here too. Stage 1–4 in-game tests: `docs/stage1-runbook.md` to `docs/stage4-runbook.md`; what 1.7 changed: `docs/ENGINE_NOTES.md`.

The decompiled 1.7 source is in `/workspaces/reference/src/` (Assembly-CSharp, RimWorld, Waylith, UnityStubs, 0Harmony). The repo is public: never commit game DLLs or decompiled code.

## Layout

| Folder | Holds | Needs RimWorld? |
| --- | --- | --- |
| `src/RimChronicles.Llm` | LLM client, prompt loading, validator, template fallbacks | No |
| `src/RimChronicles.Core` | Snapshot model, summarizer, plot state machines, seed detectors, director | No |
| `src/RimChronicles.Llm.Anthropic` | Claude provider (official Anthropic SDK); harness only, not shipped | No |
| `src/RimChronicles.Game` | Engine adapter, Harmony patches, `GameComponent`, letters and UI | Yes |
| `harness/` | Stage 0 eval app and snapshots | No |
| `tests/` | Unit tests for Llm, Core and harness | No |
| `prompts/` | Versioned prompt templates | No |
| `training/` | Director-model fine-tuning (Python, WSL2 on the PC; never shipped) | No |
| `About/`, `1.7/Assemblies/` | Mod metadata; built DLLs (git-ignored) | Loaded by the game |

Package versions are central in `Directory.Packages.props`. Llm, Core and Game must not reference any NuGet package (`LayeringTests` enforces this for Llm and Core).

## Rules for AI-written code

1. Check every RimWorld API against the decompiled 1.7 source in `reference/` (outside the repo). Never trust memory of 1.4–1.6.
2. Target `net10.0` and use Harmony 3 from the Workshop mod. Never bundle an old `0Harmony.dll`.
3. Never call Unity APIs in new code; the 1.7 shims exist only for old mods. The one exception is UI code (the settings page and windows), which may use the stub types RimWorld's UI needs, such as `Rect` (decided 2026-11-21; see `docs/ENGINE_NOTES.md`).
4. Never touch game state off the main thread. LLM results go through the main-thread queue.
5. Every saved class implements `ExposeData` (Core types implement `IArchivable.Expose`), and loading tolerates missing or null data (the mod may be added to or removed from a save).
6. Game-engine calls live only in `RimChronicles.Game`. Story logic never calls RimWorld directly.
7. Every LLM feature has a template fallback and a Dev Mode action.
8. Prompt templates live in versioned files under `prompts/`, not in C# strings, and any change re-runs the Stage 0 harness.
9. The Llm and Core libraries have unit tests that run without RimWorld, and every change keeps them passing.

## Secrets

The judge and Claude reference read `RIMCHRONICLES_ANTHROPIC_API_KEY` (named by `apiKeyEnv` in `harness/models.json`), set as a Codespaces secret. Don't use `ANTHROPIC_API_KEY`: Claude Code reads that too, and it switches Claude Code off the claude.ai sign-in, which removes the claude.ai connectors. Never commit keys or put them in `harness/models.json`.

## Director-model training (training/)

Plan: `docs/director-model-tech-spec.md`; steps: `training/README.md`.

- Python lives only under `training/` and is never a runtime dependency of the mod. Its tests (`cd training && python3 -m unittest discover -s tests`) need no GPU or torch.
- The director prompt has one source (`prompts/director/`) and one renderer (`WorldSummarizer`, C#). Python never renders prompts: `director-dataset` writes the rendered text.
- Never train or tune on the test split (generator seed 9001); changing it bumps its test version.
- All randomness is seeded and recorded in a manifest. Labels in `training/data/labels/` are committed; cases regenerate from their seed.
- A model becomes a preset only after its report is committed to `harness/reports/`.
