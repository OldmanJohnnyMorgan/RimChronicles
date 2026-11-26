using LudeonTK;
using RimWorld;
using Verse;

namespace RimChronicles.Game;

/// <summary>
/// Dev Mode: drops a squad of six veteran colonists by the colony, to speed up in-game testing of the crises and the
/// War in Heaven (storming the Citadel or the Cathedral). Every skill at 20 with a major passion, legendary gear,
/// medicine and food for a caravan; one of them a level-6 psycaster with every psycast and psychic gear. Without
/// Royalty the psycaster is a plain fighter and the armour is marine armour. Main thread only.
/// </summary>
public static class TestSquad
{
    private const int Size = 6;
    private const QualityCategory Quality = QualityCategory.Legendary;

    /// <summary>Gear by role: (weapon, apparel worn in order). Missing defs (no Royalty) are skipped.</summary>
    private static readonly string[] Base = ["Apparel_CollarShirt", "Apparel_Pants"];
    private static readonly string[] Heavy = ["Apparel_ArmorCataphract", "Apparel_ArmorHelmetCataphract"];
    private static readonly string[] Marine = ["Apparel_PowerArmor", "Apparel_PowerArmorHelmet"];
    private static readonly string[] Psychic = ["Apparel_PsyfocusShirt", "Apparel_PsyfocusRobe", "Apparel_EltexSkullcap"];

    [DebugAction("Rim Chronicles", "Spawn test squad (6 veterans, 1 psycaster)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static void Spawn()
    {
        if (Find.CurrentMap is not { } map)
        {
            return;
        }

        var pawns = new List<Pawn>();
        for (var i = 0; i < Size; i++)
        {
            var psycaster = i == 0 && ModsConfig.RoyaltyActive;
            var melee = i == 1;
            var pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer,
                forceGenerateNewPawn: true, canGeneratePawnRelations: false, mustBeCapableOfViolence: true, colonistRelationChanceFactor: 0f));
            Skills(pawn);
            Heal(pawn);
            Gear(pawn, psycaster, melee);
            if (psycaster)
            {
                Psycaster(pawn);
            }

            Supplies(pawn);
            pawns.Add(pawn);
        }

        DropPodUtility.DropThingsNear(DropCellFinder.TradeDropSpot(map), map, pawns, canRoofPunch: false, forbid: false);
        Messages.Message($"Rim Chronicles: test squad dropping in ({string.Join(", ", pawns.Select(p => p.LabelShort))}; "
            + $"{(ModsConfig.RoyaltyActive ? $"{pawns[0].LabelShort} is the psycaster" : "no Royalty, so no psycaster")}).",
            new LookTargets(pawns), MessageTypeDefOf.PositiveEvent, historical: false);
    }

    private static void Skills(Pawn pawn)
    {
        foreach (var skill in pawn.skills?.skills ?? [])
        {
            skill.Level = 20;
            skill.xpSinceLastLevel = 0f;
            skill.passion = Passion.Major;
        }
    }

    /// <summary>No old scars, missing parts or illnesses from generation: the squad starts fit to fight.</summary>
    private static void Heal(Pawn pawn)
    {
        foreach (var hediff in pawn.health.hediffSet.hediffs.Where(h => h.def.isBad && h is not Hediff_AddedPart).ToList())
        {
            pawn.health.RemoveHediff(hediff);
        }
    }

    private static void Gear(Pawn pawn, bool psycaster, bool melee)
    {
        pawn.apparel?.DestroyAll();
        pawn.equipment?.DestroyAllEquipment();
        pawn.inventory?.DestroyAll();

        var armour = Exists(Heavy) ? Heavy : Marine;
        IEnumerable<string> apparel = psycaster ? ["Apparel_Pants", .. Psychic] : [.. Base, .. armour];
        if (melee)
        {
            apparel = apparel.Append("Apparel_ShieldBelt"); // a shield belt stops its wearer shooting, so only the melee fighter
        }

        foreach (var name in apparel)
        {
            if (Make(name) is Apparel item && pawn.apparel is { } worn && ApparelUtility.HasPartsToWear(pawn, item.def) && item.PawnCanWear(pawn, ignoreGender: true))
            {
                worn.Wear(item, dropReplacedApparel: false);
            }
        }

        var weapon = melee ? (Exists("MeleeWeapon_Zeushammer") ? "MeleeWeapon_Zeushammer" : "MeleeWeapon_LongSword")
            : pawn.thingIDNumber % 3 == 0 ? "Gun_ChargeLance" : "Gun_ChargeRifle";
        if (Make(weapon) is ThingWithComps gun && pawn.equipment is { } equipment)
        {
            equipment.AddEquipment(gun);
        }
    }

    /// <summary>Psylink 6, every psycast up to level 6, psychically hypersensitive, full psyfocus.</summary>
    private static void Psycaster(Pawn pawn)
    {
        if (pawn.GetMainPsylinkSource() is not { } psylink)
        {
            psylink = (Hediff_Psylink)HediffMaker.MakeHediff(HediffDefOf.PsychicAmplifier, pawn, pawn.health.hediffSet.GetBrain());
            pawn.health.AddHediff(psylink);
        }

        if (psylink.level < 6)
        {
            psylink.ChangeLevel(6 - psylink.level, sendLetter: false);
        }

        if (pawn.abilities is { } abilities)
        {
            foreach (var def in DefDatabase<AbilityDef>.AllDefsListForReading.Where(d => d.IsPsycast && d.level <= 6))
            {
                if (abilities.GetAbility(def) is null)
                {
                    abilities.GainAbility(def);
                }
            }
        }

        if (pawn.story?.traits is { } traits && DefDatabase<TraitDef>.GetNamedSilentFail("PsychicSensitivity") is { } sensitivity)
        {
            if (traits.GetTrait(sensitivity) is { } current)
            {
                traits.RemoveTrait(current);
            }

            traits.GainTrait(new Trait(sensitivity, 2, forced: true), suppressConflicts: true);
        }

        if (pawn.psychicEntropy is { } entropy)
        {
            if (entropy.CurrentPsyfocus < 0)
            {
                entropy.SetInitialPsyfocusLevel();
            }

            entropy.OffsetPsyfocusDirectly(1f - entropy.CurrentPsyfocus);
        }
    }

    /// <summary>Enough to caravan to a capital and patch up after.</summary>
    private static void Supplies(Pawn pawn)
    {
        foreach (var (name, count) in new[] { ("MealSurvivalPack", 10), ("MedicineUltratech", 5) })
        {
            if (Make(name) is { } thing)
            {
                thing.stackCount = Math.Min(count, thing.def.stackLimit);
                pawn.inventory?.innerContainer.TryAdd(thing);
            }
        }
    }

    private static bool Exists(params string[] names) => names.All(n => DefDatabase<ThingDef>.GetNamedSilentFail(n) is not null);

    private static Thing? Make(string defName)
    {
        if (DefDatabase<ThingDef>.GetNamedSilentFail(defName) is not { } def)
        {
            return null;
        }

        var thing = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
        thing.TryGetComp<CompQuality>()?.SetQuality(Quality, ArtGenerationContext.Colony);
        return thing;
    }
}
