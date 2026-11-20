using System.Reflection;

namespace RimChronicles.Tests.Architecture;

/// <summary>
/// Guards the layering rules from the design spec: the shipped story and LLM libraries
/// never depend on RimWorld, Harmony, Unity or third-party packages.
/// </summary>
public class LayeringTests
{
    private static readonly string[] ForbiddenPrefixes =
    [
        "Assembly-CSharp", "0Harmony", "Harmony", "UnityEngine", "UnityStubs", "Verse", "RimWorld",
    ];

    [Theory]
    [InlineData("RimChronicles.Llm")]
    [InlineData("RimChronicles.Core")]
    public void SharedLibraries_DoNotReferenceGameAssemblies(string assemblyName)
    {
        var refs = Assembly.Load(assemblyName).GetReferencedAssemblies().Select(a => a.Name!).ToList();

        var forbidden = refs.Where(r => ForbiddenPrefixes.Any(p => r.StartsWith(p, StringComparison.OrdinalIgnoreCase)));

        Assert.Empty(forbidden);
    }

    [Theory]
    [InlineData("RimChronicles.Llm", new string[0])]
    [InlineData("RimChronicles.Core", new[] { "RimChronicles.Llm" })]
    public void SharedLibraries_ReferenceOnlyTheBclAndAllowedProjects(string assemblyName, string[] allowedProjects)
    {
        var refs = Assembly.Load(assemblyName).GetReferencedAssemblies().Select(a => a.Name!);

        var unexpected = refs.Where(r =>
            !r.StartsWith("System", StringComparison.Ordinal) &&
            !r.StartsWith("Microsoft.", StringComparison.Ordinal) &&
            r != "netstandard" &&
            !allowedProjects.Contains(r));

        Assert.Empty(unexpected);
    }
}
