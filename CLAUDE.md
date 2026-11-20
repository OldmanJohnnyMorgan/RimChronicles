# Rim Chronicles

RimWorld 1.7 mod adding a story mode narrated by a local LLM.

- Design spec: `docs/Rim Chronicles — Design Spec.md`
- Technical spec: https://claude.ai/artifact/AMTEFZMpdn9ELPEJd5biXB

## Commands

```bash
dotnet build RimChronicles.slnx          # builds everything; Game builds empty without a RimWorld install
dotnet test RimChronicles.slnx           # Llm, Core and harness tests; no RimWorld or GPU needed
dotnet run --project harness/RimChronicles.Eval -- --help   # Stage 0 eval harness (rimchron-eval)
```

To build the real mod, copy `Directory.Build.props.user.example` to `Directory.Build.props.user` and set the RimWorld and Harmony paths.

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

Package versions are central in `Directory.Packages.props`. Llm, Core and Game must not reference any NuGet package (`LayeringTests` enforces this for Llm and Core).

## Rules for AI-written code

1. Check every RimWorld API against the decompiled 1.7 source in `reference/` (outside the repo). Never trust memory of 1.4–1.6.
2. Target `net10.0` and use Harmony 3 from the Workshop mod. Never bundle an old `0Harmony.dll`.
3. Never call Unity APIs in new code; the 1.7 shims exist only for old mods.
4. Never touch game state off the main thread. LLM results go through the main-thread queue.
5. Every saved class implements `ExposeData` (Core types implement `IArchivable.Expose`), and loading tolerates missing or null data (the mod may be added to or removed from a save).
6. Game-engine calls live only in `RimChronicles.Game`. Story logic never calls RimWorld directly.
7. Every LLM feature has a template fallback and a Dev Mode action.
8. Prompt templates live in versioned files under `prompts/`, not in C# strings, and any change re-runs the Stage 0 harness.
9. The Llm and Core libraries have unit tests that run without RimWorld, and every change keeps them passing.

## Secrets

The judge and Claude reference read `ANTHROPIC_API_KEY` from the environment. Never commit keys or put them in `harness/models.json`.
