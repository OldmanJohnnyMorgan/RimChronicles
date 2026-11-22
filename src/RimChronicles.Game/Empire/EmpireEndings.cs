using LudeonTK;
using RimChronicles.Core.Empire;
using RimChronicles.Core.News;
using RimChronicles.Core.Plots;
using RimChronicles.Core.Plots.LostFamily;
using RimChronicles.Core.Plots.Nemesis;
using RimChronicles.Core.Snapshots;
using RimChronicles.Game.Plots;
using RimChronicles.Llm.Chat;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Empire;

/// <summary>The major plot's endings (Stage 5 M5): the plot ends, the news reports it, and the epilogue (written by the
/// model, or the template) is shown on the victory screen and kept as a letter. The game goes on afterwards, as after
/// vanilla's royal ascent. Main thread only.</summary>
public static class EmpireEndings
{
    private static string Colony => Faction.OfPlayer.Name;

    public static void Finish(RimChroniclesGameComponent component, Plot plot, string ending, Faction empire, int today)
    {
        var saga = component.Empire;
        saga.Ending = ending;
        saga.Note(today, $"ending: {ending}");
        if (ending == EmpireEnding.Resistance)
        {
            saga.Reckon(ascendant: false, today);
        }

        if (ending == EmpireEnding.Independence && empire.PlayerGoodwill < 0)
        {
            empire.TryAffectGoodwillWith(Faction.OfPlayer, -empire.PlayerGoodwill, canSendMessage: true, canSendHostilityLetter: false);
        }

        component.News.Add(new NewsEvent(today, EmpireSaga.NewsKind, EmpireLetters.EndingNews(ending, empire.Name, Colony), empire.Name));
        var c = CaseFor(component, plot, ending, empire);
        component.Plots.End(plot, today, ending);

        var template = EpilogueTask.Template(c, Colony);
        var colony = Colony;
        PlotNarrator.Run(component, "the epilogue", template, text => Show(c.Ending, text),
            (prompts, tier, fallback) => EpilogueTask.Build(prompts, c, colony, tier, ThinkingMode.Off, fallback).Definition);
    }

    private static void Show(string ending, string text)
    {
        var title = $"Rim Chronicles: {EpilogueTask.Headline(ending)}";
        Find.LetterStack.ReceiveLetter(title, text, LetterDefOf.PositiveEvent);
        GameVictoryUtility.ShowCredits($"{title}\n\n{text}", SongDefOf.EndCreditsSong, exitToMainMenu: false);
    }

    /// <summary>The epilogue's facts: the ending, up to four colonists who mattered (a title holder, the protagonists of
    /// the colony's stories, then its most skilled hands), and the story's main events.</summary>
    public static EpilogueCase CaseFor(RimChroniclesGameComponent component, Plot plot, string ending, Faction empire)
    {
        var people = new List<EpiloguePerson>();
        var named = new HashSet<string>();
        void Add(Pawn? pawn, string deed)
        {
            if (pawn is null || people.Count >= 4 || !named.Add(pawn.ThingID))
            {
                return;
            }

            people.Add(new EpiloguePerson { Name = pawn.LabelShort, Pronoun = NemesisTracker.Pronoun(pawn), Deed = deed, Dead = pawn.Dead });
        }

        var colonists = PawnsFinder.AllMaps_FreeColonists.ToList();
        if (colonists.Select(p => (Pawn: p, Title: p.royalty?.GetCurrentTitle(empire))).Where(x => x.Title is not null)
                .OrderByDescending(x => x.Title!.seniority).FirstOrDefault() is { Pawn: { } titled, Title: { } title })
        {
            Add(titled, $"rose in the Empire's service to the title of {title.GetLabelFor(titled)}");
        }

        foreach (var story in component.Plots.Active.Concat(component.Plots.Finished).Where(p => p.Protagonist.Kind == ProtagonistKind.Pawn).OrderBy(p => p.StartDay))
        {
            var pawn = component.TrackedOrFind(story.Protagonist.PawnId);
            var deed = story.Template switch
            {
                NemesisPlot.TemplateId => $"never forgot the raider {story.Fact(NemesisPlot.NemesisName) ?? "who wronged the colony"}",
                LostFamilyArc.TemplateId when story.Outcome == "reunited" => $"brought {story.Fact(LostFamilyArc.KinName) ?? "family"} home",
                LostFamilyArc.TemplateId => $"searched for {story.Fact(LostFamilyArc.KinName) ?? "lost family"}",
                _ => null,
            };
            if (deed is not null)
            {
                Add(pawn, deed);
            }
        }

        foreach (var (pawn, skill) in colonists
                     .Select(p => (Pawn: p, Skill: p.skills?.skills.Where(s => !s.TotallyDisabled).OrderByDescending(s => s.Level).FirstOrDefault()))
                     .Where(x => x.Skill is not null)
                     .OrderByDescending(x => x.Skill!.Level).ThenBy(x => x.Pawn.ThingID, StringComparer.Ordinal))
        {
            Add(pawn, $"was the colony's best hand at {skill!.def.label}");
        }

        var saga = component.Empire;
        var events = new List<string> { $"The Empire woke because {EmpireText.TriggerWhy(saga.Trigger, saga.TriggerDetail, Colony)}." };
        events.AddRange(saga.Expansions.Where(e => e.Result != ExpansionResult.Repelled).Take(3)
            .Select(e => e.Result == ExpansionResult.Conquered ? $"{empire.Name} took {e.Settlement} from {e.Faction}." : $"{empire.Name} razed {e.Settlement}."));
        events.AddRange(saga.Expansions.Where(e => e.Result == ExpansionResult.Repelled).Take(2)
            .Select(e => $"{empire.Name} was thrown back at {e.Settlement}."));
        // Promotions: "Day 80: promoted nemesis-2: Kael became an Imperial officer" → "Kael became an Imperial officer."
        events.AddRange(saga.Log.Select(line => line.IndexOf(": promoted ", StringComparison.Ordinal) is var at and >= 0
                ? line[(at + 2)..] : null)
            .OfType<string>()
            .Select(line => line[(line.IndexOf(": ", StringComparison.Ordinal) + 2)..])
            .Select(line => char.ToUpperInvariant(line[0]) + line[1..] + ".")
            .Take(2));
        events.Add(EmpireLetters.EndingNews(ending, empire.Name, Colony) + ".");
        return new EpilogueCase
        {
            Ending = ending, Empire = empire.Name, Stance = EmpireArc.StanceOf(plot), People = people, Events = events,
        };
    }

    [DebugAction("Rim Chronicles", "Empire: reach an ending", allowedGameStates = AllowedGameStates.Playing)]
    public static void DevEnding()
    {
        if (RimChroniclesGameComponent.Current is not { } component || EmpireBeats.Current(component) is not { } plot || EmpireDriver.EmpireFaction is not { } empire)
        {
            Messages.Message("Rim Chronicles: no Empire plot running (Empire: trigger awakening).", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        Find.WindowStack.Add(new FloatMenu(new[] { EmpireEnding.Resistance, EmpireEnding.Imperial, EmpireEnding.Independence }
            .Select(ending => new FloatMenuOption(ending, () => Finish(component, plot, ending, empire, GenDate.DaysPassed)))
            .ToList()));
    }
}
