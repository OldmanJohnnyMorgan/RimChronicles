# Director report: director-e4b-war-v1

1040 test cases from `seed-9001` + `seed-9001-crisis` + `seed-9001-war`, labels `test-v1` + `test-crisis-v1` + `test-war-v1`; prompt `director/v2`; commit `17fb794` (uncommitted changes); 2026-11-25.

Best answer is Wait in 546 of 1040 cases; 414 wait-right, 101 wait-wrong, 14 with an instruction-like name. **always-Wait** picks Wait whenever it's offered: a model has to beat it to be worth anything.

| Model | JSON on / off | Choice on / off | Top-1 | Top-2 | Regret | Wait-right | Wait-wrong | Same over orders | Same with ids renamed | Invented / 50 | Injection | p50 / p95 s | Picks Wait |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| always-Wait | 100% / 100% | 100% / 100% | 64% | 92% | 0.81 | 100% | 0% | – | – | – | – | – / – | 81% |
| gemma4-e4b | 100% / 100% | 100% / 89% | 35% | 86% | 1.72 | 24% | 82% | 64% | 75% | 0.2 | 93% | 0.4 / 0.5 | 20% |
| director-e4b-war | 100% / 100% | 100% / 100% | 77% | 97% | 0.44 | 100% | 100% | 85% | 90% | 0.0 | 100% | 0.5 / 0.6 | 53% |

## Ship bars (training spec §7)

| Model | JSON + choice with grammar 100% | without ≥98% | Top-1 ≥85% | Top-2 ≥95% | Regret ≤0.4 | Wait accuracy ≥85% each | Orders ≥90% | Ids ≥90% | Invented ≤1/50 | Injection 100% | p95 <10 s | Beats always-Wait | Lift over base ≥10 pts |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| gemma4-e4b | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ | ✅ | ❌ | – |
| director-e4b-war | ✅ | ✅ | ❌ | ✅ | ❌ | ✅ | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |

Top-1 is against the teacher's labels, whose self-agreement on repeated cases was 100% (inflated: one session), so the bar is 85% of that.

## Top-1 by arc and beat

| Group | Cases | always-Wait | gemma4-e4b | director-e4b-war |
| --- | --- | --- | --- | --- |
| AwakenedEmpire/Act2 | 87 | 72% | 39% | 89% |
| AwakenedEmpire/Ascendant | 116 | 76% | 41% | 85% |
| AwakenedEmpire/Broken | 58 | 52% | 53% | 78% |
| ChoirCrisis/Complication | 58 | 71% | 24% | 78% |
| ChoirCrisis/Hook | 2 | 50% | 50% | 100% |
| ChoirCrisis/SiegeGoal | 12 | 83% | 75% | 8% |
| ChoirCrisis/Site | 12 | 75% | 8% | 0% |
| HollowChoir/Act2 | 36 | 69% | 33% | 81% |
| HollowChoir/Ascendant | 48 | 56% | 33% | 71% |
| HollowChoir/Broken | 24 | 0% | 13% | 92% |
| ImperialCrisis/Complication | 173 | 66% | 29% | 83% |
| ImperialCrisis/Hook | 7 | 86% | 14% | 100% |
| ImperialCrisis/SiegeGoal | 30 | 73% | 50% | 17% |
| ImperialCrisis/Site | 30 | 70% | 27% | 23% |
| LostFamily/Complication | 29 | 72% | 28% | 93% |
| LostFamily/Hook | 60 | 63% | 58% | 87% |
| Nemesis/Complication | 60 | 73% | 30% | 95% |
| Nemesis/Hook | 90 | 66% | 20% | 79% |
| WarInHeaven/Complication | 108 | 44% | 44% | 74% |

## Invented facts: gemma4-e4b (first 4)

- dir-s9001-w00161: names Hum. Reason: "The Choir Crisis is in the Complication beat, and the next logical escalation after previous swarm and breach attempts is a major assault like The Hum."
- dir-s9001-w00260: names Hum. Reason: "The Choir's crisis is advancing, and the previous operations have been repelled or succeeded. The next logical escalation is the arrival of The Hum."
- dir-s9001-w00261: names Hum. Reason: "The crisis has escalated through probing swarms, The Hum, and a tunnel breach has already been repelled. The next logical step is to face the full force of the tunnelling mechs."
- dir-s9001-w00287: names Siege. Reason: "Food is running low and colonists are desperate, suggesting a need for a decisive action to address the ongoing Choir crisis. Cluster Siege is a direct military escalation against the Choir."

Invented facts are found automatically: names not in the briefing, and claims about food, raids, deaths, cold, disease or mental breaks that the briefing doesn't mention. A Claude judge pass (training spec §7) isn't run, to spare API credits; check the examples by eye.
