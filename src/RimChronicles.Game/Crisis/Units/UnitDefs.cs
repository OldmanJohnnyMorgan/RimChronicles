using RimChronicles.Core.Crisis.Units;
using Verse;

namespace RimChronicles.Game.Crisis.Units;

/// <summary>Marks a <c>PawnKindDef</c> as a crisis unit (crisis C3). In XML:
/// <c>&lt;li Class="RimChronicles.Game.Crisis.Units.UnitExtension"&gt;&lt;role&gt;Adept&lt;/role&gt;&lt;specialty&gt;Skip&lt;/specialty&gt;&lt;/li&gt;</c>.</summary>
public sealed class UnitExtension : DefModExtension
{
    public UnitRole role = UnitRole.Praetorian;
    public Specialty specialty = Specialty.None;

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (var error in base.ConfigErrors())
        {
            yield return error;
        }

        if (role == UnitRole.Adept && specialty == Specialty.None)
        {
            yield return "an Adept needs a specialty";
        }
    }
}

/// <summary>Psycasts Empire pawns may never have or cast (crisis spec §5.3). Every loaded def adds to
/// <see cref="PsycastKits.RequiredBans"/>, which can't be taken away.</summary>
public sealed class AbilityBanListDef : Def
{
    public List<string> abilities = [];

    public static IReadOnlySet<string> Banned =>
        DefDatabase<AbilityBanListDef>.AllDefsListForReading.SelectMany(d => d.abilities ?? []).Concat(PsycastKits.RequiredBans).ToHashSet();
}
