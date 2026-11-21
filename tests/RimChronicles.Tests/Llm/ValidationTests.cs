using System.Text.Json.Nodes;
using RimChronicles.Llm.Validation;

namespace RimChronicles.Tests.Llm;

public class JsonSchemaValidatorTests
{
    private static readonly JsonObject Schema = (JsonObject)JsonNode.Parse("""
        {
          "type": "object",
          "properties": {
            "choice": { "type": "string", "enum": ["Wait", "Complication:VexSighted"] },
            "reason": { "type": "string", "minLength": 3, "maxLength": 40 },
            "weight": { "type": "integer", "minimum": 0, "maximum": 10 },
            "tags":   { "type": "array", "items": { "type": "string" }, "maxItems": 2 }
          },
          "required": ["choice", "reason"],
          "additionalProperties": false
        }
        """)!;

    [Fact]
    public void ValidObject_HasNoErrors() =>
        Assert.Empty(JsonSchemaValidator.Validate(JsonNode.Parse("""{"choice":"Wait","reason":"quiet week","weight":3,"tags":["a"]}"""), Schema));

    [Theory]
    [InlineData("""{"reason":"quiet week"}""", "$.choice is required")]
    [InlineData("""{"choice":"Attack","reason":"quiet week"}""", "$.choice must be one of")]
    [InlineData("""{"choice":"Wait","reason":"no"}""", "$.reason is shorter than 3")]
    [InlineData("""{"choice":"Wait","reason":"quiet week","extra":1}""", "$.extra is not an allowed property")]
    [InlineData("""{"choice":"Wait","reason":"quiet week","weight":2.5}""", "$.weight should be \"integer\"")]
    [InlineData("""{"choice":"Wait","reason":"quiet week","weight":11}""", "$.weight is above the maximum")]
    [InlineData("""{"choice":"Wait","reason":"quiet week","tags":["a",1]}""", "$.tags[1] should be \"string\"")]
    [InlineData("""{"choice":"Wait","reason":"quiet week","tags":["a","b","c"]}""", "$.tags has more than 2 items")]
    [InlineData("""["Wait"]""", "$ should be \"object\"")]
    public void InvalidObject_ReportsTheProblem(string json, string expected)
    {
        var errors = JsonSchemaValidator.Validate(JsonNode.Parse(json), Schema);

        Assert.Contains(errors, e => e.StartsWith(expected, StringComparison.Ordinal));
    }
}

public class CheckTests
{
    [Fact]
    public void JsonSchemaCheck_AcceptsJsonInsideCodeFences()
    {
        var schema = (JsonObject)JsonNode.Parse("""{"type":"object","required":["choice"]}""")!;

        var result = new JsonSchemaCheck(schema).Check("```json\n{\"choice\":\"Wait\"}\n```");

        Assert.True(result.Passed);
    }

    [Fact]
    public void JsonSchemaCheck_NotJson_Fails() =>
        Assert.False(new JsonSchemaCheck(new JsonObject()).Check("I choose Wait").Passed);

    [Theory]
    [InlineData("""{"choice":"Wait"}""", true)]
    [InlineData("""{"choice":"wait"}""", false)]
    [InlineData("""{"choice":"Raid"}""", false)]
    [InlineData("""{"choice":3}""", false)]
    [InlineData("""{"reason":"x"}""", false)]
    [InlineData("""["Wait"]""", false)]
    public void AllowedChoiceCheck_IsExact(string output, bool passes) =>
        Assert.Equal(passes, new AllowedChoiceCheck("choice", ["Wait", "Complication:VexSighted"]).Check(output).Passed);

    [Theory]
    [InlineData(3, 5, "one two three", true)]
    [InlineData(3, 5, "one two", false)]
    [InlineData(3, 5, "one two three four five six", false)]
    public void WordCountCheck_EnforcesRange(int min, int max, string text, bool passes) =>
        Assert.Equal(passes, new WordCountCheck(min, max).Check(text).Passed);

    [Fact]
    public void WordCountCheck_FailureNamesTheCount() =>
        Assert.Contains("this has 2", new WordCountCheck(3, 5).Check("one two").Failures[0]);

    private static readonly GroundingCheck Grounding = new(["Hearthstead", "Mara", "Raider 'Vex'", "The Shattered Empire"]);

    [Fact]
    public void GroundingCheck_AllowsNamesFromTheBriefing() =>
        Assert.True(Grounding.Check("Traders say Mara of Hearthstead still hunts Vex, while the Shattered Empire stirs this Aprimay.").Passed);

    [Fact]
    public void GroundingCheck_FlagsInventedNamesMidSentence()
    {
        var unknown = Grounding.FindUnknownNames("Word from the north is that Captain Orlov met Mara near Port Silas.");

        Assert.Equal(["Orlov", "Port", "Silas"], unknown);
    }

    [Fact]
    public void GroundingCheck_IgnoresSentenceStartsAndPossessives()
    {
        var unknown = Grounding.FindUnknownNames("Rumours spread. Meanwhile Mara's guns stayed quiet.\nTrade resumed.");

        Assert.Empty(unknown);
    }

    [Fact]
    public void BannedPhraseCheck_CatchesMetaText()
    {
        var check = new BannedPhraseCheck();

        Assert.False(check.Check("Here is the bulletin you asked for: rain again.").Passed);
        Assert.True(check.Check("Rain again over Hearthstead.").Passed);
    }

    [Theory]
    [InlineData("Heard since your last bulletin (lead with these): the pirates left.")]
    [InlineData("**News since your last bulletin:** the pirates left.")]
    [InlineData("The pirates left.\n\nAlready reported: the war goes on.")]
    public void BannedPhraseCheck_CatchesCopiedBriefingHeadings(string output) =>
        Assert.False(new BannedPhraseCheck().Check(output).Passed);
}
