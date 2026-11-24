using RimChronicles.Llm;

namespace RimChronicles.Tests.Llm;

public class ModelSlotsTests
{
    private static readonly ModelSlots.Inputs Main = new()
    {
        Tier = ModelTier.Recommended, Endpoint = "http://localhost:11434/", Model = "gemma4:e4b", ApiKey = "",
    };

    [Fact]
    public void WithoutADirectorModel_TheMainModelDecides_AtTheMainTier()
    {
        var slot = ModelSlots.Director(Main)!;
        Assert.Equal("gemma4:e4b", slot.Model);
        Assert.Equal(ModelTier.Recommended, slot.PromptTier);
        Assert.Equal(TierPresets.Temperature, slot.Temperature);
        Assert.False(ModelSlots.DirectorSeparate(Main));
    }

    [Fact]
    public void ADirectorModel_DecidesAtItsOwnTier_AndBorrowsTheMainServer()
    {
        var s = Main with { DirectorModel = " rimchron-director-e2b ", DirectorTier = ModelTier.Minimum };
        var slot = ModelSlots.Director(s)!;
        Assert.Equal("rimchron-director-e2b", slot.Model);
        Assert.Equal("http://localhost:11434/", slot.Endpoint);
        Assert.Equal(ModelTier.Minimum, slot.PromptTier); // the tier it was trained on, not the main Recommended
        Assert.Equal(TierPresets.DirectorTemperature, slot.Temperature);

        var elsewhere = ModelSlots.Director(s with { DirectorEndpoint = "http://gpu-box:11434/", DirectorApiKey = "k" })!;
        Assert.Equal("http://gpu-box:11434/", elsewhere.Endpoint);
        Assert.Equal("k", elsewhere.ApiKey);
    }

    [Fact]
    public void NothingIsCalled_WhenTheTierIsOff_DirectorModelIncluded()
    {
        Assert.Null(ModelSlots.Director(Main with { Tier = ModelTier.Off, DirectorModel = "rimchron-director-e2b" }));
    }

    [Fact]
    public void ABadDirectorAddress_FallsBackToTheMainModel()
    {
        var s = Main with { DirectorModel = "rimchron-director-e2b", DirectorEndpoint = "localhost:11434" };
        Assert.False(ModelSlots.DirectorSeparate(s));
        Assert.Equal("gemma4:e4b", ModelSlots.Director(s)!.Model);
    }

    [Theory]
    [InlineData("rimchron-director-e2b", ModelTier.Minimum)]
    [InlineData("rimchron-director-e4b", ModelTier.Recommended)]
    [InlineData("RimChron-Director-E4B:latest", ModelTier.Recommended)]
    [InlineData("gemma4:12b", ModelTier.High)]
    public void SuggestedDirectorTier_ComesFromTheName(string model, ModelTier tier) =>
        Assert.Equal(tier, TierPresets.SuggestedDirectorTier(model));

    [Fact]
    public void SuggestedDirectorTier_IsNullWhenTheNameDoesntSay() =>
        Assert.Null(TierPresets.SuggestedDirectorTier("my-director"));

    [Theory]
    [InlineData("rimchron-director-e2b", "rimchron-director-e2b", ModelTier.Minimum)]
    [InlineData(" RimChron-Director-E2B \n", "rimchron-director-e2b", ModelTier.Minimum)]
    [InlineData("rimchron-director-e4b", "rimchron-director-e4b", ModelTier.Recommended)]
    [InlineData(" RimChron-Director-E4B ", "rimchron-director-e4b", ModelTier.Recommended)]
    public void DirectorPreset_MatchesTheName_AndCarriesItsTrainingTier(string model, string expected, ModelTier tier)
    {
        var preset = TierPresets.DirectorPreset(model);
        Assert.NotNull(preset);
        Assert.Equal(expected, preset.Value.Model);
        Assert.Equal(tier, preset.Value.Tier);
    }

    [Fact]
    public void DirectorPresets_AreTheTiersTheirNamesSuggest()
    {
        foreach (var preset in TierPresets.DirectorPresets)
        {
            Assert.Equal(preset.Tier, TierPresets.SuggestedDirectorTier(preset.Model));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("my-director")]
    public void DirectorPreset_IsNullForBlankOrCustomNames(string? model) =>
        Assert.Null(TierPresets.DirectorPreset(model));

    [Theory]
    [InlineData("", true)]                                   // the main (prompted) model decides
    [InlineData("gemma4:12b", true)]                         // a prompted director reads the options' descriptions
    [InlineData("rimchron-director-e2b", false)]             // fine-tuned before the crisis arc (C11)
    [InlineData(" RimChron-Director-E4B ", false)]
    [InlineData("rimchron-director-e4b-v2", false)]          // an unlisted fine-tune counts as trained before it
    public void TheCrisisReachesTheDirector_OnlyWhenItsModelKnowsIt(string directorModel, bool knows) =>
        Assert.Equal(knows, ModelSlots.DirectorKnowsCrisis(Main with { DirectorModel = directorModel, DirectorTier = ModelTier.Minimum }));

    [Fact]
    public void WithNoModel_TheCrisisDoesntReachTheDirector() =>
        Assert.False(ModelSlots.DirectorKnowsCrisis(Main with { Tier = ModelTier.Off }));

    [Fact]
    public void EveryDirectorPreset_SaysWhetherItKnowsTheCrisis() =>
        Assert.All(TierPresets.DirectorPresets, p => Assert.Equal(p.KnowsCrisis, TierPresets.KnowsCrisis(p.Model)));
}
