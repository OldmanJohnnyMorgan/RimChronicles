using LudeonTK;
using RimChronicles.Core.Empire;
using RimChronicles.Core.Powers;
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

        component.News.Add(new NewsEvent(today, Power.Empire.NewsKind, EmpireLetters.EndingNews(ending, empire.Name, Colony), empire.Name));
        var c = CaseFor(component, plot, ending, empire);
        component.Plots.End(plot, today, ending);
        Epilogue(component, plot, c);
    }

    /// <summary>The victory screen for a story's ending (the Empire's, the Choir's or the War in Heaven's, W11): the
    /// epilogue, written by the model or the template, shown with the credits and kept as a letter and in the plot.</summary>
    public static void Epilogue(RimChroniclesGameComponent component, Plot plot, EpilogueCase c)
    {
        var template = EpilogueTask.Template(c, Colony);
        var colony = Colony;
        if (Settings.ProviderFactory.CreateProse(RimChroniclesMod.Settings) is not null)
        {
            // The model can take a minute or two; without a word the player would wait for the credits in silence.
            Messages.Message($"The story of {colony} is over. The chronicler is writing its epilogue…", MessageTypeDefOf.NeutralEvent, historical: false);
        }

        PlotNarrator.Run(component, "the epilogue", template, text => Show(plot, c.Ending, text),
            (prompts, tier, fallback) => EpilogueTask.Build(prompts, c, colony, tier, ThinkingMode.Off, fallback).Definition);
    }

    private static void Show(Plot plot, string ending, string text)
    {
        var title = $"Rim Chronicles: {EpilogueTask.Headline(ending)}";
        plot.Tell(GenDate.DaysPassed, title, text); // the plot has ended; its story still shows in the Chronicle tab
        Engine.StoryLetters.Send(title, text, LetterDefOf.PositiveEvent, faction: Engine.StoryLetters.Empire);
        GameVictoryUtility.ShowCredits($"{title}\n\n{text}", SongDefOf.EndCreditsSong, exitToMainMenu: false);
    }

    /// <summary>The epilogue's facts: the ending, up to four colonists who mattered (a title holder, the protagonists of
    /// the colony's stories, then its most skilled hands), and the story's main events.</summary>
    public static EpilogueCase CaseFor(RimChroniclesGameComponent component, Plot plot, string ending, Faction empire) =>
        CaseFor(component, plot, ending, empire, Power.Empire, null, [], EmpireLetters.EndingNews(ending, empire.Name, Colony));

    /// <summary>The epilogue's facts for any power's ending (W11): as the Empire's, with the power's own rank holder
    /// (the Choir's high rank) and saga, the rival for a War in Heaven ending, the war's own events, and the closing line.</summary>
    public static EpilogueCase CaseFor(RimChroniclesGameComponent component, Plot plot, string ending, Faction empire, Power power, string? rival,
        IReadOnlyList<string> warEvents, string closing)
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
        if (power == Power.Choir)
        {
            if (component.Choir.HighTitleHolder is { Length: > 0 } holder && colonists.FirstOrDefault(p => p.LabelShort == holder || p.Name?.ToStringShort == holder) is { } chorister)
            {
                Add(chorister, "rose high in the Chorus");
            }
        }
        else if (colonists.Select(p => (Pawn: p, Title: p.royalty?.GetCurrentTitle(empire))).Where(x => x.Title is not null)
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

        var saga = component.SagaOf(power);
        var events = new List<string>
        {
            power == Power.Choir
                ? $"The Hollow Choir woke because {power.Text.TriggerWhy(saga.Trigger, saga.TriggerDetail, Colony)}."
                : $"The Empire woke because {EmpireText.TriggerWhy(saga.Trigger, saga.TriggerDetail, Colony)}.",
        };
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
        events.AddRange(warEvents);
        events.Add(closing + ".");
        return new EpilogueCase
        {
            Ending = ending, Empire = empire.Name, Rival = rival, Stance = component.Allegiance.StanceToward(power) is { Length: > 0 } s ? s : PowerArc.StanceOf(plot),
            People = people, Events = events,
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
