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
