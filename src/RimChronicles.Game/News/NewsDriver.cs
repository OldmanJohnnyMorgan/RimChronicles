using RimChronicles.Core.News;
using RimChronicles.Game.Engine;
using RimChronicles.Game.Settings;
using RimChronicles.Llm;
using RimChronicles.Llm.Chat;
using RimChronicles.Llm.Prompts;
using RimChronicles.Llm.Tasks;
using RimWorld;
using Verse;

namespace RimChronicles.Game.News;

/// <summary>Runs the news desk in game (Stage 2 M3): checks the schedule once a day, writes the next bulletin on a
/// background task, and delivers it as a letter on its due day. Every call here is on the main thread except the body
/// of the background task, which works only on plain data and hands its result back through the main-thread queue.</summary>
public static class NewsDriver
{
    private static PromptLibrary? _prompts;

    /// <summary>The prompts shipped with the mod, loaded once. Null (templates only) if they can't be read.</summary>
    private static PromptLibrary? Prompts
    {
        get
        {
            if (_prompts is null && RimChroniclesMod.PromptsRoot is { } root)
            {
                try
                {
                    _prompts = PromptLibrary.Load(root);
                }
                catch (Exception ex)
                {
                    Log.ErrorOnce($"[Rim Chronicles] Could not load prompts from {root}; bulletins will use templates. {ex.Message}", 0x52434e01);
                }
            }

            return _prompts;
        }
    }

    /// <summary>Called once per in-game day by the daily patch.</summary>
    public static void Tick(RimChroniclesGameComponent component)
    {
        var desk = component.Desk;
        var today = GenDate.DaysPassed;
        switch (desk.Plan(today, RimChroniclesMod.Settings.NewsCadenceDays, component.WritingBulletin))
        {
            case DeskAction.Generate:
                StartWriting(component);
                break;
            case DeskAction.Deliver:
                Deliver(component);
                break;
        }
    }

    /// <summary>Dev Mode: bring the next bulletin forward to today.</summary>
    public static void Force(RimChroniclesGameComponent component)
    {
        component.Desk.DueNow(GenDate.DaysPassed);
        if (component.Desk.Pending is not null)
        {
            Deliver(component);
        }
        else if (!component.WritingBulletin)
        {
            StartWriting(component);
        }
    }

    private static void StartWriting(RimChroniclesGameComponent component)
    {
        var desk = component.Desk;
        var settings = RimChroniclesMod.Settings;
        var today = GenDate.DaysPassed;

        // Main thread: read the game into plain data.
        var (snapshot, fresh) = desk.PrepareSnapshot(WorldReader.Instance.Read(), component.News);
        var reporter = new ReporterState { Name = desk.Reporter.Name, Outlet = desk.Reporter.Outlet, Persona = desk.Reporter.Persona };
        var fallback = new DelegateFallback<string>(() => BulletinTemplates.Compose(reporter, fresh, today));
        var provider = ProviderFactory.Create(settings);
        var tier = settings.Tier == ModelTier.Off ? ModelTier.Recommended : settings.Tier;
        var prompts = Prompts;
        var mainThread = component.MainThread;
        var token = component.GameToken;

        component.WritingBulletin = true;
        Task.Run(async () =>
        {
            Bulletin bulletin;
            try
            {
                if (prompts is null || provider is null)
                {
                    bulletin = new Bulletin { Text = fallback.Create(), Source = nameof(OutcomeSource.Template) };
                }
                else
                {
                    var (definition, _) = BulletinTask.Build(prompts, snapshot, tier, ThinkingMode.Off, fallback);
                    var outcome = await new LlmTaskRunner(provider).RunAsync(definition, token).ConfigureAwait(false);
                    bulletin = new Bulletin
                    {
                        Text = outcome.Value,
                        Source = outcome.Source.ToString(),
                        PromptVersion = outcome.Source == OutcomeSource.Template ? "" : $"news-bulletin/{outcome.PromptVersion}",
                    };
                    if (outcome.Source == OutcomeSource.Template)
                    {
                        var why = outcome.Attempts.LastOrDefault() is { } last
                            ? last.Result.Ok ? string.Join("; ", last.Failures) : last.Result.Error
                            : "no attempt";
                        mainThread.Post(() => Log.Warning($"[Rim Chronicles] Bulletin fell back to a template: {why}"));
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return; // the game ended; this component is gone
            }
            catch (Exception ex)
            {
                mainThread.Post(() => Log.Error($"[Rim Chronicles] Writing a bulletin failed; using a template. {ex}"));
                bulletin = new Bulletin { Text = fallback.Create(), Source = nameof(OutcomeSource.Template) };
            }

            bulletin.NewsCovered = fresh;
            mainThread.Post(() => Written(component, bulletin));
        }, token);
    }

    private static void Written(RimChroniclesGameComponent component, Bulletin bulletin)
    {
        component.WritingBulletin = false;
        if (!component.Desk.SetPending(bulletin))
        {
            return;
        }

        if (Prefs.DevMode)
        {
            Log.Message($"[Rim Chronicles] Bulletin written ({bulletin.Source}), due day {bulletin.DueDay}.");
        }

        if (GenDate.DaysPassed >= component.Desk.NextDueDay)
        {
            Deliver(component);
        }
    }

    private static void Deliver(RimChroniclesGameComponent component)
    {
        var desk = component.Desk;
        if (desk.Deliver(GenDate.DaysPassed, RimChroniclesMod.Settings.NewsCadenceDays) is not { } bulletin)
        {
            return;
        }

        var footer = Prefs.DevMode
            ? $"\n\n<color=#a0a0a0>({bulletin.Source}{(bulletin.PromptVersion.Length > 0 ? ", " + bulletin.PromptVersion : "")})</color>"
            : "";
        LetterSink.Instance.Send(LetterLabel(desk.Reporter, bulletin.DeliveredDay), bulletin.Text + footer);
    }

    /// <summary>"The Rim Dispatch, day 21". The world reader leaves letters with this label out of a snapshot's events.</summary>
    public static string LetterLabel(ReporterState reporter, int day) => $"{reporter.Outlet.CapitalizeFirst()}, day {day}";
}
