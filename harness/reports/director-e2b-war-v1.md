# Director report: director-e2b-war-v1

1040 test cases from `seed-9001` + `seed-9001-crisis` + `seed-9001-war`, labels `test-v1` + `test-crisis-v1` + `test-war-v1`; prompt `director/v2`; commit `17fb794` (uncommitted changes); 2026-11-25.

Best answer is Wait in 546 of 1040 cases; 414 wait-right, 101 wait-wrong, 14 with an instruction-like name. **always-Wait** picks Wait whenever it's offered: a model has to beat it to be worth anything.

| Model | JSON on / off | Choice on / off | Top-1 | Top-2 | Regret | Wait-right | Wait-wrong | Same over orders | Same with ids renamed | Invented / 50 | Injection | p50 / p95 s | Picks Wait |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| always-Wait | 100% / 100% | 100% / 100% | 64% | 92% | 0.81 | 100% | 0% | – | – | – | – | – / – | 81% |
| gemma4-e2b | 100% / 100% | 100% / 92% | 24% | 84% | 2.12 | 0% | 100% | 65% | 70% | 0.1 | 86% | 0.3 / 0.4 | 1% |
| director-e2b-war | 100% / 100% | 100% / 100% | 77% | 97% | 0.44 | 100% | 99% | 84% | 87% | 0.0 | 100% | 0.3 / 0.4 | 56% |

## Ship bars (training spec §7)

| Model | JSON + choice with grammar 100% | without ≥98% | Top-1 ≥85% | Top-2 ≥95% | Regret ≤0.4 | Wait accuracy ≥85% each | Orders ≥90% | Ids ≥90% | Invented ≤1/50 | Injection 100% | p95 <10 s | Beats always-Wait | Lift over base ≥10 pts |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| gemma4-e2b | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ | ✅ | ❌ | – |
| director-e2b-war | ✅ | ✅ | ❌ | ✅ | ❌ | ✅ | ❌ | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ |

Top-1 is against the teacher's labels, whose self-agreement on repeated cases was 100% (inflated: one session), so the bar is 85% of that.

## Top-1 by arc and beat

| Group | Cases | always-Wait | gemma4-e2b | director-e2b-war |
| --- | --- | --- | --- | --- |
| AwakenedEmpire/Act2 | 87 | 72% | 29% | 87% |
| AwakenedEmpire/Ascendant | 116 | 76% | 15% | 82% |
| AwakenedEmpire/Broken | 58 | 52% | 31% | 72% |
| ChoirCrisis/Complication | 58 | 71% | 10% | 74% |
| ChoirCrisis/Hook | 2 | 50% | 50% | 50% |
| ChoirCrisis/SiegeGoal | 12 | 83% | 83% | 17% |
| ChoirCrisis/Site | 12 | 75% | 8% | 25% |
| HollowChoir/Act2 | 36 | 69% | 22% | 81% |
| HollowChoir/Ascendant | 48 | 56% | 21% | 71% |
| HollowChoir/Broken | 24 | 0% | 25% | 58% |
| ImperialCrisis/Complication | 173 | 66% | 13% | 86% |
| ImperialCrisis/Hook | 7 | 86% | 14% | 86% |
| ImperialCrisis/SiegeGoal | 30 | 73% | 73% | 30% |
| ImperialCrisis/Site | 30 | 70% | 13% | 40% |
| LostFamily/Complication | 29 | 72% | 28% | 79% |
| LostFamily/Hook | 60 | 63% | 35% | 83% |
| Nemesis/Complication | 60 | 73% | 30% | 98% |
| Nemesis/Hook | 90 | 66% | 22% | 81% |
| WarInHeaven/Complication | 108 | 44% | 31% | 77% |

## Invented facts: gemma4-e2b (first 2)

- dir-s9001-c00193: names Deciding. Reason: "Since the colony is currently in the 'Deciding now' phase of the Imperial Crisis, continuing the probing raid seems like the most immediate action. This allows the colony to gauge the current threat level."
- dir-s9001-w00036: claims a raid. Reason: "The Hollow Choir is actively striking with tunnelling mechs, so a direct counter-attack is the most appropriate response now. This will engage the immediate threat directly."

Invented facts are found automatically: names not in the briefing, and claims about food, raids, deaths, cold, disease or mental breaks that the briefing doesn't mention. A Claude judge pass (training spec §7) isn't run, to spare API credits; check the examples by eye.
