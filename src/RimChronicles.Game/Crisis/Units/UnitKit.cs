using RimChronicles.Core.Choir;
using RimChronicles.Core.Crisis;
using RimChronicles.Core.Crisis.Units;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Crisis.Units;

/// <summary>
/// Makes and fixes up crisis units (crisis C3): psylink level and fixed psycast kit by stage (vanilla's random psycasts
/// are removed, the ban list applies), gear quality, bionics, psychic sensitivity and starting psyfocus. Only crisis
/// code generates these pawn kinds, so nothing patches vanilla generation. Main thread only.
/// </summary>
public static class UnitKit
{
    private static readonly (string Hediff, string Part)[] Bionics = [("BionicArm", "Shoulder"), ("BionicLeg", "Leg"), ("BionicEye", "Eye")];

    public static UnitExtension? Unit(PawnKindDef? kind) => kind?.GetModExtension<UnitExtension>();

    /// <summary>The Empire's crisis units. The Hollow Choir's mechanitors (W7) are <see cref="ChoirKinds"/>, kept apart
    /// so no Imperial force, check or captive ever picks one up.</summary>
    public static IEnumerable<PawnKindDef> Kinds => DefDatabase<PawnKindDef>.AllDefsListForReading.Where(k => Unit(k) is { } u && !ChoirRoster.IsMechanitor(u.role));

    /// <summary>The Hollow Choir's mechanitor units (War in Heaven W7).</summary>
    public static IEnumerable<PawnKindDef> ChoirKinds => DefDatabase<PawnKindDef>.AllDefsListForReading.Where(k => Unit(k) is { } u && ChoirRoster.IsMechanitor(u.role));

    /// <summary>Generates a unit for <paramref name="faction"/> at <paramref name="stage"/>, kitted out. Not spawned.</summary>
    public static Pawn Generate(PawnKindDef kind, Faction faction, CrisisStage stage)
    {
        var pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction, forceGenerateNewPawn: true, mustBeCapableOfViolence: true));
        Apply(pawn, stage);
        if (pawn.HasPsylink && RelaySevered is { } severed && RimChroniclesGameComponent.Current is { } component
            && component.Crisis.IsDown(CrisisCapability.Psykers, GenDate.DaysPassed))
        {
            pawn.health.AddHediff(severed); // a destroyed relay site weakens the psykers that come while it's down (C9)
        }

        return pawn;
    }

    private static HediffDef? RelaySevered => DefDatabase<HediffDef>.GetNamedSilentFail("RimChronicles_RelaySevered");

    /// <summary>Applies the stage's tier and the kit to a freshly generated unit.</summary>
    public static void Apply(Pawn pawn, CrisisStage stage)
    {
        if (Unit(pawn.kindDef) is not { } unit)
        {
            return;
        }

        var tier = PsycastKits.Tier(unit.role, stage);
        var psylink = PsycastKits.Psylink(unit.role, unit.specialty, stage, Rand.Value);
        if (psylink > 0)
        {
            SetPsylink(pawn, psylink);
            SetPsychicSensitivity(pawn);
        }

        SetKit(pawn, unit, pawn.GetPsylinkLevel());
        if (psylink > 0)
        {
            SetPsyfocus(pawn, tier.Psyfocus);
        }

        RaiseGear(pawn, (QualityCategory)(int)tier.Gear);
        AddBionics(pawn, tier.Bionics);
    }

    /// <summary>Removes every psycast that isn't in the unit's kit at its psylink level, or that is banned, and grants
    /// what's missing. Player pawns (a recruited defector) are left alone: the kit and the ban list are for enemies.</summary>
    public static void Enforce(Pawn pawn)
    {
        if (Unit(pawn.kindDef) is { } unit && pawn.abilities is not null && pawn.Faction != Faction.OfPlayer)
        {
            SetKit(pawn, unit, pawn.GetPsylinkLevel());
        }
    }

    /// <summary>What's wrong with a unit, or nothing: psycasts outside its kit, banned ones, missing ones, a psylink
    /// outside its stage's range.</summary>
    public static IReadOnlyList<string> Problems(Pawn pawn, CrisisStage stage)
    {
        if (Unit(pawn.kindDef) is not { } unit)
        {
            return [$"{pawn.kindDef.defName} isn't a crisis unit"];
        }

        var problems = new List<string>();
        var has = Psycasts(pawn).Select(a => a.def.defName).ToHashSet();
        var expected = Expected(unit, pawn.GetPsylinkLevel()).ToHashSet();
        var banned = AbilityBanListDef.Banned;
        problems.AddRange(has.Where(banned.Contains).Select(d => $"has banned psycast {d}"));
        problems.AddRange(has.Except(expected).Where(d => !banned.Contains(d)).Select(d => $"has {d}, not in its kit"));
        problems.AddRange(expected.Except(has).Select(d => $"is missing {d}"));
        if (PsycastKits.IsPsyker(unit.role))
        {
            var level = pawn.GetPsylinkLevel();
            var lowest = PsycastKits.Psylink(unit.role, unit.specialty, stage, 0);
            var highest = PsycastKits.Psylink(unit.role, unit.specialty, stage, 0.999);
            if (level < lowest || level > highest)
            {
                problems.Add($"psylink {level}, expected {lowest}–{highest}");
            }

            if (expected.Count == 0)
            {
                problems.Add("has nothing to cast");
            }
        }

        return problems;
    }

    public static IEnumerable<Ability> Psycasts(Pawn pawn) => pawn.abilities?.abilities.Where(a => a.def.IsPsycast) ?? [];

    private static IEnumerable<string> Expected(UnitExtension unit, int psylink) =>
        PsycastKits.Grant(unit.role, unit.specialty, psylink, AbilityBanListDef.Banned, d => DefDatabase<AbilityDef>.GetNamedSilentFail(d) is not null);

    private static void SetKit(Pawn pawn, UnitExtension unit, int psylink)
    {
        if (pawn.abilities is null)
        {
            return;
        }

        var kit = Expected(unit, psylink).ToHashSet();
        foreach (var ability in Psycasts(pawn).Where(a => !kit.Contains(a.def.defName)).ToList())
        {
            pawn.abilities.RemoveAbility(ability.def);
        }

        foreach (var def in kit.Select(d => DefDatabase<AbilityDef>.GetNamed(d)).Where(d => pawn.abilities.GetAbility(d) is null))
        {
            pawn.abilities.GainAbility(def);
        }
    }

    /// <summary>Sets the psylink to <paramref name="level"/> without letters. Vanilla adds a random psycast per level
    /// gained; <see cref="SetKit"/> removes them after.</summary>
    private static void SetPsylink(Pawn pawn, int level)
    {
        if (pawn.GetMainPsylinkSource() is not { } psylink)
        {
            psylink = (Hediff_Psylink)HediffMaker.MakeHediff(HediffDefOf.PsychicAmplifier, pawn, pawn.health.hediffSet.GetBrain());
            pawn.health.AddHediff(psylink);
        }

        var change = level - psylink.level;
        if (change > 0)
        {
            psylink.ChangeLevel(change, sendLetter: false);
        }
        else if (change < 0)
        {
            psylink.ChangeLevel(change); // lowering never adds psycasts or letters
        }
    }

    /// <summary>Psychically hypersensitive (degree 2), replacing any other psychic sensitivity trait.</summary>
    private static void SetPsychicSensitivity(Pawn pawn)
    {
        if (pawn.story?.traits is not { } traits || DefDatabase<TraitDef>.GetNamedSilentFail("PsychicSensitivity") is not { } def)
        {
            return;
        }

        if (traits.GetTrait(def) is { } current)
        {
            if (current.Degree == 2)
            {
                return;
            }

            traits.RemoveTrait(current);
        }

        traits.GainTrait(new Trait(def, 2, forced: true), suppressConflicts: true);
    }

    private static void SetPsyfocus(Pawn pawn, float target)
    {
        if (pawn.psychicEntropy is not { } entropy)
        {
            return;
        }

        if (entropy.CurrentPsyfocus < 0)
        {
            entropy.SetInitialPsyfocusLevel(); // vanilla leaves it at -1 until something sets it (C0)
        }

        entropy.OffsetPsyfocusDirectly(target - entropy.CurrentPsyfocus);
    }

    private static void RaiseGear(Pawn pawn, QualityCategory quality)
    {
        var gear = (pawn.apparel?.WornApparel ?? []).Cast<Thing>().Concat(pawn.equipment?.AllEquipmentListForReading ?? []);
        foreach (var thing in gear)
        {
            if (thing.TryGetComp<CompQuality>() is { } comp && comp.Quality < quality)
            {
                comp.SetQuality(quality, ArtGenerationContext.Outsider);
            }
        }
    }

    private static void AddBionics(Pawn pawn, int count)
    {
        var added = 0;
        foreach (var (hediffName, partName) in Bionics)
        {
            if (added >= count)
            {
                return;
            }

            if (DefDatabase<HediffDef>.GetNamedSilentFail(hediffName) is not { } hediff
                || DefDatabase<BodyPartDef>.GetNamedSilentFail(partName) is not { } partDef)
            {
                continue;
            }

            var part = pawn.health.hediffSet.GetNotMissingParts()
                .FirstOrDefault(p => p.def == partDef && !pawn.health.hediffSet.PartOrAnyAncestorHasDirectlyAddedParts(p));
            if (part is not null)
            {
                pawn.health.AddHediff(hediff, part);
                added++;
            }
        }
    }
}
