namespace RimChronicles.Core.Engine;

/// <summary>A named piece of work, such as one daily driver.</summary>
public sealed record Step(string Name, Action Run);

/// <summary>Runs steps in order so one that fails can't stop the rest: each failure goes to the report and the next
/// step runs.</summary>
public static class Steps
{
    /// <returns>The names of the steps that failed.</returns>
    public static List<string> RunAll(IEnumerable<Step> steps, Action<string, Exception> onError)
    {
        var failed = new List<string>();
        foreach (var step in steps)
        {
            try
            {
                step.Run();
            }
            catch (Exception ex)
            {
                failed.Add(step.Name);
                onError(step.Name, ex);
            }
        }

        return failed;
    }
}
