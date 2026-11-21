# Stage 4 runbook: the plot director and quests in game

In-game testing happens once, at the end of the stage. Each milestone adds its checks here, and the M6 proof run
gathers them into one session. Before testing:

```powershell
cd C:\RimworldMods\RimChronicles\RimChronicles
git pull
dotnet build RimChronicles.slnx
```

Then restart RimWorld, with **Development mode** on.

## M1: the director

When a plot has more than one way to go, the director picks, with a reason. **Plot: director log** lists the
decisions. Nemesis now chooses at two points:
- **After the sighting:** a raid, another sighting (at most two in all), or a quiet stretch.
- **After the raid:** the choice letter, or a quiet stretch.

A plot waits at most once per beat. Single-option steps fire as before, logged as `Only`.

| # | Do | Pass when |
| --- | --- | --- |
| 1 | **Nemesis: force seed**, then **Plot: advance beat** a few times, letting raids leave. Then **Plot: director log**. | Decisions logged with the options offered, the choice, the source (`Model`, or `Only` for single options) and a reason that fits the story. |
| 2 | Pick **Off**, and repeat. | The decisions say `Random`, with "picked by weight: no model is set up". The arc still finishes. |
| 3 | Save while a decision is in progress (right after **Plot: advance beat** with the model on), then load. | No error. The plot decides again on a later day. |

## M2: the quest framework

Quests are built in C# (the pattern of vanilla's `QuestNode_Root_*` quests), linked to the plot that started them. A
quest's outcome (success, failure, an ignored offer) comes back to the plot as the fact `quest.<template>`, and the plot
decides its next beat the day after. **Quest: test (oldest plot)** offers a framework test quest, and **Quest: show
links** lists every linked quest.

| # | Do | Pass when |
| --- | --- | --- |
| 1 | **Nemesis: force seed**, then **Quest: test (oldest plot)**. | A quest-available letter, and the quest shows in the **Quests** tab with its name and description. |
| 2 | Accept it, and play an hour, then a day. | The quest succeeds (the game's quest-completed letter). The next day, **Show plots** has `quest.Test=Success`, and **Quest: show links** says `Success`. |
| 3 | Offer another test quest and leave it unaccepted for a day. | It expires. The next day: `quest.Test=Ignored`. |
| 4 | Save with a test quest offered, then load. | The quest and its link survive, and the outcome still comes back. |
| 5 | Open the **Quests** tab after playing a while. | No Rim Chronicles quests appear on their own: only plots start them. |
