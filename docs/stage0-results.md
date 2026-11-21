# Stage 0 results

Stage 0 closed on 2026-11-21. The tier presets are the three Gemma 4 models below, all run on Ollama with thinking off
and temperature 0.4 (see `harness/models.json`).

| Tier | Model | Ollama tag | Role |
| --- | --- | --- | --- |
| Minimum | Gemma 4 E2B | `gemma4:e2b` | Fallback for small GPUs |
| Recommended | Gemma 4 E4B | `gemma4:e4b` | The default |
| High | Gemma 4 12B | `gemma4:12b` | Best local prose and grounding |

Qwen3.5 and Raon-1 nano are out of the project. The Qwen models invented major facts at 10–37 per 10 bulletins across
four prompt versions, against 2–3 for Gemma at the time. Raon-1 nano needs Krafton's llama.cpp fork, which is too much
setup to ask of players for one preset.

## Final run

Run `20261121-141140`, at commit `be0f771`, with a clean tree. It used prompts director v1, news-bulletin v8,
inheritance-handoff v3 and judge v3. Each of the 20 snapshots ran 3 times, for 756 cells in all. The judge was
Claude Sonnet 5.5 and the reference was Claude Opus 5.5. The judge scored all 276 prose cells with no errors. The run's
output is on the `m5-results` branch under `results/20261121-141140/`.

| Metric (bar) | E2B | E4B | 12B | Claude ref |
| --- | --- | --- | --- | --- |
| Director: JSON valid with and without grammar (100% / ≥95%) | 100% / 100% | 100% / 100% | 100% / 100% | 100% / 100% |
| Director: allowed choice (100%) | 100% | 100% | 100% | 100% |
| Director: median latency (<10 s) | 0.3 s | 0.4 s | 0.7 s | 2.0 s |
| Bulletin: major invented facts per 10 (≤1) | **2.7** | 0.7 | 0.7 | 0.3 |
| Bulletin: overall prose, 1–5 (≥3.5 for the default) | 2.2 | **2.8** | 3.2 | 3.5 |
| Bulletin: length within range (≥90%) | 97% | 100% | 98% | 100% |
| Bulletin: median latency (<20 s) | 0.5 s | 0.5 s | 1.1 s | 6.5 s |
| Handoff: major invented facts per 10 (≤1) | **1.1** | **2.2** | 0.0 | 0.0 |
| Handoff: overall prose, 1–5 (≥3.5 for the default) | 2.0 | **2.7** | 3.1 | 3.4 |
| Handoff: length within range (≥90%) | **67%** | 100% | 100% | 100% |

Bold marks a missed bar. The bulletin rows cover 60 outputs per model, but the handoff rows cover only 9 (3 snapshots ×
3 repeats), so a single bad output moves the handoff rate by 1.1 per 10.

## Why these picks stand despite the missed bars

- **Prose quality bar waived.** No model reaches 3.5 on the handoff, not even the Claude reference (3.4), and Claude
  only just reaches it on bulletins. The project owner read the blind spot-check sample and rated the Gemma bulletins'
  voice far above the judge (about 5 against 2.9), so the owner's call replaces the hand-scored 10% spot-check. The
  judge's score is kept as a relative ranking (12B > E4B > E2B), not as a pass/fail test.
- **E4B's handoff invented facts are two outputs on one snapshot.** Both are on `snap-18-late-desperate`. One is a
  workbench, an object outside the set the prompt allows. The other is a real slip: the heir sets off for the pass
  where the dead character died. On bulletins, the larger sample, E4B passes.
- **E2B is a fallback, not a default.** It invents more in bulletins, and 3 of its 9 handoffs ran 44–47 words, under
  the 60-word floor. The validator catches these, and the template fallback covers them. Its director decisions are
  flawless.
- **12B has one repeated slip.** On `snap-20-late-strained`, all 3 bulletins invented a target for the Empire's final
  demands. Otherwise it is the best local model on every prose metric, and well inside the latency bars.

## Known issues carried into later stages

- The harness's own grounding check failed the Claude reference on 4 of 9 handoffs, though the judge found no
  invented facts in them. The check is likely too strict on good prose, and should be reviewed before Stage 2 relies
  on it in the game.
- The judge counts many minor embellishments, such as an added cause or old news told as new. These have no bar,
  because the count depends on the judge model, but they are what prompt work should target next.
- From here on, the harness is the regression test: any prompt change re-runs it (`docs/stage0-runbook.md`), and
  the results are compared with this run using `report latest --compare 20261121-141140`.
