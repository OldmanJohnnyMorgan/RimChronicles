using RimChronicles.Eval.Config;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Harness;

public class TrainingConfigTests
{
    [Theory]
    [InlineData("models.d0.json", 1)]
    [InlineData("models.director.json", 4)]
    public void TrainingModelConfigs_LoadAndValidate(string file, int models)
    {
        var config = HarnessConfig.Load(Repo.Path("training", file));
        Assert.Equal(models, config.Models.Count);
        Assert.All(config.Models, m => Assert.Empty(m.Validate()));
    }
}
