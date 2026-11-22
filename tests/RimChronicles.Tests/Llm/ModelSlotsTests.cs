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
}
