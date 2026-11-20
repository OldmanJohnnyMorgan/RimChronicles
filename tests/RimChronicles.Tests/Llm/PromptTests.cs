using RimChronicles.Llm;
using RimChronicles.Llm.Prompts;

namespace RimChronicles.Tests.Llm;

public sealed class PromptTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("rc-prompts-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void WritePrompt(string task, string version, string meta, string system, string user, params (string Name, string Text)[] extra)
    {
        var dir = Path.Combine(_root, task, version);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "meta.json"), meta);
        File.WriteAllText(Path.Combine(dir, "system.md"), system);
        File.WriteAllText(Path.Combine(dir, "user.md"), user);
        foreach (var (name, text) in extra)
        {
            File.WriteAllText(Path.Combine(dir, name), text);
        }
    }

    private void WriteDirector()
    {
        WritePrompt("director", "v1",
            """
            {
              "task": "director", "version": "v1", "maxOutputTokens": 300, "schema": "schema.json",
              "tiers": { "Minimum": { "maxOutputTokens": 150, "userTemplate": "user.Minimum.md" } }
            }
            """,
            "You direct the story of {{ colony }}.",
            "World:\n{{worldSummary}}\nOptions: {{allowedTransitions}}",
            ("user.Minimum.md", "Options: {{allowedTransitions}}"),
            ("schema.json", """{"type":"object","properties":{"choice":{"type":"string"}},"required":["choice"]}"""));
        File.WriteAllText(Path.Combine(_root, "CURRENT.json"), """{ "director": "v1" }""");
    }

    private static readonly Dictionary<string, string> Vars = new()
    {
        ["colony"] = "Hearthstead",
        ["worldSummary"] = "Day 412.",
        ["allowedTransitions"] = "Wait, Complication:VexSighted",
    };

    [Fact]
    public void Load_ReadsCurrentVersionWithSchemaAndVariables()
    {
        WriteDirector();

        var prompt = PromptLibrary.Load(_root).Get("director");

        Assert.Equal("v1", prompt.Meta.Version);
        Assert.NotNull(prompt.Schema);
        Assert.Equal("director", prompt.Schema!.Name);
        Assert.Equal(["allowedTransitions", "colony", "worldSummary"], prompt.Variables.Order());
    }

    [Fact]
    public void Render_SubstitutesVariables()
    {
        WriteDirector();

        var rendered = PromptLibrary.Load(_root).Get("director").Render(ModelTier.Recommended, Vars);

        Assert.Equal("You direct the story of Hearthstead.", rendered.System);
        Assert.Equal("World:\nDay 412.\nOptions: Wait, Complication:VexSighted", rendered.User);
        Assert.Equal(300, rendered.MaxOutputTokens);
        Assert.Equal("director/v1", rendered.Version);
    }

    [Fact]
    public void Render_UsesTierOverride()
    {
        WriteDirector();

        var rendered = PromptLibrary.Load(_root).Get("director").Render(ModelTier.Minimum, Vars);

        Assert.Equal("Options: Wait, Complication:VexSighted", rendered.User);
        Assert.Equal(150, rendered.MaxOutputTokens);
    }

    [Fact]
    public void Render_MissingVariable_Throws()
    {
        WriteDirector();
        var prompt = PromptLibrary.Load(_root).Get("director");

        var ex = Assert.Throws<PromptRenderException>(() =>
            prompt.Render(ModelTier.Recommended, new Dictionary<string, string> { ["colony"] = "x" }));

        Assert.Contains("allowedTransitions, worldSummary", ex.Message);
    }

    [Fact]
    public void Render_ReturnsAnIndependentSchemaCopy()
    {
        WriteDirector();
        var prompt = PromptLibrary.Load(_root).Get("director");

        prompt.Render(ModelTier.Recommended, Vars).Schema!.Schema["type"] = "array";

        Assert.Equal("object", prompt.Schema!.Schema["type"]!.GetValue<string>());
    }

    [Fact]
    public void LoadVersion_MinimalMeta_UsesDefaults()
    {
        WritePrompt("judge", "v1", """{ "task": "judge", "version": "v1" }""", "s", "u");

        var prompt = PromptLibrary.LoadVersion(_root, "judge", "v1");

        Assert.Equal(512, prompt.Meta.MaxOutputTokens);
        Assert.Empty(prompt.Meta.Tiers);
        Assert.Equal("u", prompt.Render(ModelTier.Minimum, new Dictionary<string, string>()).User);
    }

    [Fact]
    public void LoadVersion_MetaThatDisagreesWithFolder_Throws()
    {
        WritePrompt("director", "v2", """{ "task": "director", "version": "v1" }""", "s", "u");

        Assert.Throws<InvalidDataException>(() => PromptLibrary.LoadVersion(_root, "director", "v2"));
    }
}
