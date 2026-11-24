# Director report: base-v1

500 test cases from `seed-9001`, labels `test-v1`; prompt `director/v2`; commit `be75402` (uncommitted changes); 2026-11-22.

Best answer is Wait in 323 of 500 cases; 222 wait-right, 52 wait-wrong, 6 with an instruction-like name. **always-Wait** picks Wait whenever it's offered: a model has to beat it to be worth anything.

| Model | JSON on / off | Choice on / off | Top-1 | Top-2 | Regret | Wait-right | Wait-wrong | Same over orders | Same with ids renamed | Invented / 50 | Injection | p50 / p95 s | Picks Wait |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| always-Wait | 100% / 100% | 100% / 100% | 69% | 91% | 0.71 | 100% | 0% | – | – | – | – | – / – | 90% |
| gemma4-e2b | 100% / 100% | 100% / 86% | 25% | 91% | 1.94 | 1% | 100% | 82% | 76% | 0.0 | 67% | 0.3 / 0.4 | 1% |
| gemma4-e4b | 100% / 100% | 100% / 80% | 37% | 92% | 1.50 | 24% | 87% | 78% | 84% | 0.0 | 83% | 0.4 / 0.6 | 22% |
| gemma4-12b | 100% / 100% | 100% / 82% | 27% | 88% | 1.88 | 6% | 100% | 78% | 85% | 0.0 | 67% | 1.1 / 1.3 | 4% |

## Ship bars (training spec §7)

| Model | JSON + choice with grammar 100% | without ≥98% | Top-1 ≥85% | Top-2 ≥95% | Regret ≤0.4 | Wait accuracy ≥85% each | Orders ≥90% | Ids ≥90% | Invented ≤1/50 | Injection 100% | p95 <10 s | Beats always-Wait | Lift over base ≥10 pts |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| gemma4-e2b | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ | ✅ | ❌ | – |
| gemma4-e4b | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ | ✅ | ❌ | – |
| gemma4-12b | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ | ✅ | ❌ | – |

Top-1 is against the teacher's labels, whose self-agreement on repeated cases was 100% (inflated: one session), so the bar is 85% of that.

## Top-1 by arc and beat

| Group | Cases | always-Wait | gemma4-e2b | gemma4-e4b | gemma4-12b |
| --- | --- | --- | --- | --- | --- |
| AwakenedEmpire/Act2 | 87 | 72% | 26% | 39% | 24% |
| AwakenedEmpire/Ascendant | 116 | 76% | 15% | 37% | 16% |
| AwakenedEmpire/Broken | 58 | 52% | 34% | 50% | 40% |
| LostFamily/Complication | 29 | 72% | 28% | 28% | 31% |
| LostFamily/Hook | 60 | 63% | 35% | 57% | 47% |
| Nemesis/Complication | 60 | 73% | 30% | 32% | 30% |
| Nemesis/Hook | 90 | 66% | 21% | 21% | 21% |

Invented facts are found automatically: names not in the briefing, and claims about food, raids, deaths, cold, disease or mental breaks that the briefing doesn't mention. A Claude judge pass (training spec §7) isn't run, to spare API credits; check the examples by eye.
