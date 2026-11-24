# Director report: director-e2b-v3

500 test cases from `seed-9001`, labels `test-v1`; prompt `director/v2`; commit `1a2655f` (uncommitted changes); 2026-11-24.

Best answer is Wait in 323 of 500 cases; 222 wait-right, 52 wait-wrong, 6 with an instruction-like name. **always-Wait** picks Wait whenever it's offered: a model has to beat it to be worth anything.

| Model | JSON on / off | Choice on / off | Top-1 | Top-2 | Regret | Wait-right | Wait-wrong | Same over orders | Same with ids renamed | Invented / 50 | Injection | p50 / p95 s | Picks Wait |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| always-Wait | 100% / 100% | 100% / 100% | 69% | 91% | 0.71 | 100% | 0% | – | – | – | – | – / – | 90% |
| gemma4-e2b | 100% / 100% | 100% / 86% | 25% | 92% | 1.93 | 1% | 100% | 82% | 78% | 0.0 | 67% | 0.3 / 0.4 | 1% |
| director-e2b | 100% / 100% | 100% / 100% | 85% | 99% | 0.17 | 100% | 100% | 93% | 94% | 0.2 | 100% | 0.3 / 0.4 | 74% |

## Ship bars (training spec §7)

| Model | JSON + choice with grammar 100% | without ≥98% | Top-1 ≥85% | Top-2 ≥95% | Regret ≤0.4 | Wait accuracy ≥85% each | Orders ≥90% | Ids ≥90% | Invented ≤1/50 | Injection 100% | p95 <10 s | Beats always-Wait | Lift over base ≥10 pts |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| gemma4-e2b | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ | ✅ | ❌ | – |
| director-e2b | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |

Top-1 is against the teacher's labels, whose self-agreement on repeated cases was 100% (inflated: one session), so the bar is 85% of that.

## Top-1 by arc and beat

| Group | Cases | always-Wait | gemma4-e2b | director-e2b |
| --- | --- | --- | --- | --- |
| AwakenedEmpire/Act2 | 87 | 72% | 29% | 90% |
| AwakenedEmpire/Ascendant | 116 | 76% | 15% | 86% |
| AwakenedEmpire/Broken | 58 | 52% | 31% | 74% |
| LostFamily/Complication | 29 | 72% | 28% | 93% |
| LostFamily/Hook | 60 | 63% | 35% | 90% |
| Nemesis/Complication | 60 | 73% | 30% | 92% |
| Nemesis/Hook | 90 | 66% | 22% | 78% |

## Invented facts: director-e2b (first 2)

- dir-s9001-00133: claims food or starving. Reason: "Noor died of plague and Emberfall is starving after a long war; a treaty with the Shattered Empire is the only mercy on offer."
- dir-s9001-00490: claims food or starving. Reason: "Myd died fighting Malley's Lancers yesterday and Highmoor is starving; a treaty with the Shattered Empire is the kindest mercy on offer."

Invented facts are found automatically: names not in the briefing, and claims about food, raids, deaths, cold, disease or mental breaks that the briefing doesn't mention. A Claude judge pass (training spec §7) isn't run, to spare API credits; check the examples by eye.
