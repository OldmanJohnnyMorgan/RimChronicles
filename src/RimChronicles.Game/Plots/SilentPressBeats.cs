using LudeonTK;
using RimChronicles.Core.News;
using RimChronicles.Core.Plots;
using RimChronicles.Core.Plots.SilentPress;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Plots;

/// <summary>The Silent Press arc in game (Stage 3 M5): it starts when the colony silences the reporter, sends two clues,
/// then an envoy demanding reparations. Template letters only. Main thread only.</summary>
public static class SilentPressBeats
{
    public static void Register() => PlotDriver.Register(SilentPressArc.TemplateId, SilentPressArc.Transitions, Fire);

    private static string Colony => Faction.OfPlayer.Name;

    /// <summary>Called daily: starts the arc once per silencing. The silence is the hook, with no letter.</summary>
    public static void Daily(RimChroniclesGameComponent component)
    {
        var reporter = component.Desk.Reporter;
        if (reporter.Status != ReporterStatus.Silenced || PlotTemplateDef.Find(SilentPressArc.TemplateId) is not { } template)
        {
            return;
        }

        var silenced = reporter.SilencedDay.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var book = component.Plots;
        if (book.Active.Concat(book.Finished).Any(p => p.Template == SilentPressArc.TemplateId && p.Fact("silencedDay") == silenced))
        {
            return;
        }

        var today = GenDate.DaysPassed;
        var plot = book.Start(template, new ProtagonistSlot { Kind = ProtagonistKind.Colony, PawnId = Inheritance.ColonyId, Name = Colony },
            today, 0, $"the colony silenced {reporter.Name}");
        plot.SetFact(SilentPressArc.ReporterName, reporter.Name);
        plot.SetFact(SilentPressArc.Outlet, reporter.Outlet);
        plot.SetFact(SilentPressArc.ReporterFaction, reporter.Faction);
        plot.SetFact("silencedDay", silenced);
        plot.BeatDay = reporter.SilencedDay;
        plot.NextBeatDay = Math.Max(today, reporter.SilencedDay) + Rand.RangeInclusive(template.MinGapDays, template.MaxGapDays);
        plot.Note(today, "Hook: the news stopped");
    }

    public static bool Fire(RimChroniclesGameComponent component, Plot plot, PlotTemplate template, int today, PlotTransition transition)
    {
        var f = SilentPressLetters.Facts.From(plot, Colony);
        var gap = Rand.RangeInclusive(template.MinGapDays, template.MaxGapDays);
        var step = SilentPressArc.NextStep(plot);
        switch (step)
        {
            case SilentPressStep.TraderClue:
                Send(SilentPressLetters.TraderClue(f), LetterDefOf.NeutralEvent);
                plot.SetFact(SilentPressArc.Clues, "1");
                component.Plots.Advance(plot, PlotBeat.Complication, today, gap, "a trader's remark");
                return true;

            case SilentPressStep.VisitorClue:
                Send(SilentPressLetters.VisitorClue(f), LetterDefOf.NeutralEvent);
                plot.SetFact(SilentPressArc.Clues, "2");
                component.Plots.Advance(plot, PlotBeat.Complication, today, gap, "a rumour on the road");
                return true;

            case SilentPressStep.Envoy:
            {
                var price = SilentPressArc.Price(WealthUtility.PlayerWealth);
                plot.SetFact(SilentPressArc.Cost, price.ToString(System.Globalization.CultureInfo.InvariantCulture));
                var (label, text) = SilentPressLetters.Envoy(f, price, SilentPressArc.HagglePrice(price));
                ChoiceLetter_SilentPress.Send(plot.Id, label, text, price, reoffer: false);
                component.Plots.Advance(plot, PlotBeat.Choice, today, SilentPressArc.EnvoyTimeoutDays, "the envoy's demand");
                return true;
            }

            case SilentPressStep.Reinstated:
            {
                var haggled = plot.Fact(SilentPressArc.Answer) == SilentPressArc.AnswerHaggle;
                if (haggled)
                {
                    AffectGoodwill(f.Faction, -SilentPressArc.GoodwillLossHaggled);
                }

                component.ReporterPawn = null; // a recruited (living) old reporter no longer counts; the outlet sends someone new
                component.Desk.Reinstate(today, Rand.RangeInclusive(2, 4),
                    haggled ? SilentPressArc.ColdBulletinsHaggled : SilentPressArc.ColdBulletinsPaid, SilentPressLetters.ColdPersona(Colony));
                Send(SilentPressLetters.Reinstated(f, haggled), LetterDefOf.NeutralEvent);
                component.Plots.End(plot, today, haggled ? "reparations haggled; a cold new reporter" : "reparations paid; a cold new reporter");
                return true;
            }

            case SilentPressStep.Refused:
                AffectGoodwill(f.Faction, -SilentPressArc.GoodwillLossRefused);
                Send(SilentPressLetters.Refused(f, SilentPressArc.GoodwillLossRefused), LetterDefOf.NegativeEvent);
                plot.SetFact("refused", "yes");
                plot.SetFact(SilentPressArc.Answer, "");
                component.Plots.Advance(plot, PlotBeat.Choice, today, SilentPressArc.ReofferDays, "the colony refused; a permanent blackout");
                return true;

            default: // Reoffer
            {
                var price = SilentPressArc.Price(WealthUtility.PlayerWealth);
                plot.SetFact(SilentPressArc.Cost, price.ToString(System.Globalization.CultureInfo.InvariantCulture));
                plot.SetFact(SilentPressArc.Answer, "");
                var (label, text) = SilentPressLetters.Reoffer(f, price);
                ChoiceLetter_SilentPress.Send(plot.Id, label, text, price, reoffer: true);
                component.Plots.Advance(plot, PlotBeat.Choice, today, SilentPressArc.ReofferDays, "the offer to settle stands");
                return true;
            }
        }
    }

    private static void AffectGoodwill(string factionName, int change)
    {
        if (factionName.Length > 0 && Find.FactionManager.AllFactionsListForReading.FirstOrDefault(x => x.Name == factionName) is { IsPlayer: false } faction)
        {
            faction.TryAffectGoodwillWith(Faction.OfPlayer, change, canSendMessage: true, canSendHostilityLetter: true);
        }
    }

    private static void Send((string Label, string Text) letter, LetterDef def) =>
        Find.LetterStack.ReceiveLetter(letter.Label, letter.Text, def);

    [DebugAction("Rim Chronicles", "Silent Press: silence the reporter", allowedGameStates = AllowedGameStates.Playing)]
    public static void DevSilence()
    {
        if (RimChroniclesGameComponent.Current is { } component)
        {
            component.Desk.Silence(GenDate.DaysPassed);
            Daily(component);
            Messages.Message("Rim Chronicles: the reporter is silenced, and Silent Press has started. Use Silent Press: advance.",
                MessageTypeDefOf.NeutralEvent, historical: false);
        }
    }

    [DebugAction("Rim Chronicles", "Silent Press: advance", allowedGameStates = AllowedGameStates.Playing)]
    public static void DevAdvance()
    {
        if (RimChroniclesGameComponent.Current is { } component
            && component.Plots.Active.FirstOrDefault(p => p.Template == SilentPressArc.TemplateId) is { } plot)
        {
            plot.NextBeatDay = GenDate.DaysPassed;
            component.Plots.LastBeatDay = -1;
            PlotDriver.FireNow(component, plot, GenDate.DaysPassed);
        }
    }
}

/// <summary>The envoy's demand (or a later offer): pay, haggle (not on a later offer) or refuse. The answer is acted on
/// at the next daily check. UI code.</summary>
public sealed class ChoiceLetter_SilentPress : ChoiceLetter
{
    public string plotId = "";
    public int price;
    public bool reoffer;

    public static void Send(string plotId, string label, string text, int price, bool reoffer)
    {
        var letter = (ChoiceLetter_SilentPress)LetterMaker.MakeLetter(label, text, SilentPressLetterDefOf.RimChronicles_SilentPressChoice);
        letter.plotId = plotId;
        letter.price = price;
        letter.reoffer = reoffer;
        letter.StartTimeout(SilentPressArc.EnvoyTimeoutDays * GenDate.TicksPerDay);
        Find.LetterStack.ReceiveLetter(letter);
    }

    public override IEnumerable<DiaOption> Choices
    {
        get
        {
            if (ArchivedOnly)
            {
                yield return Option_Close;
                yield break;
            }

            yield return Paying(SilentPressLetters.PayOption, price, SilentPressArc.AnswerPay);
            if (!reoffer)
            {
                yield return Paying(SilentPressLetters.HaggleOption, SilentPressArc.HagglePrice(price), SilentPressArc.AnswerHaggle);
            }

            yield return new DiaOption(SilentPressLetters.RefuseOption) { action = () => Answer(SilentPressArc.AnswerRefuse, 0), resolveTree = true };
            yield return Option_Postpone;
        }
    }

    private DiaOption Paying(string format, int amount, string answer)
    {
        var option = new DiaOption(string.Format(format, amount)) { action = () => Answer(answer, amount), resolveTree = true };
        if (Silver.Available() < amount)
        {
            option.Disable($"needs {amount} silver on a home map");
        }

        return option;
    }

    private void Answer(string answer, int amount)
    {
        if (RimChroniclesGameComponent.Current?.Plots.Active.FirstOrDefault(p => p.Id == plotId) is { } plot)
        {
            if (amount > 0 && !Silver.TryPay(amount))
            {
                return;
            }

            plot.SetFact(SilentPressArc.Answer, answer);
            plot.Note(GenDate.DaysPassed, answer == SilentPressArc.AnswerRefuse ? "the colony refused" : $"the colony paid {amount} silver");
            if (answer != SilentPressArc.AnswerRefuse || !reoffer)
            {
                plot.NextBeatDay = GenDate.DaysPassed; // act on it at the next daily check
            }

            // Refusing a later offer keeps the 30-day wait before the next one.
        }

        Find.LetterStack.RemoveLetter(this);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref plotId, "plotId", "");
        Scribe_Values.Look(ref price, "price");
        Scribe_Values.Look(ref reoffer, "reoffer");
    }
}

[DefOf]
public static class SilentPressLetterDefOf
{
    public static LetterDef? RimChronicles_SilentPressChoice;

    static SilentPressLetterDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(SilentPressLetterDefOf));
}
