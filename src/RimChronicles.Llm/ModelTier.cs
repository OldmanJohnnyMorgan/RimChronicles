namespace RimChronicles.Llm;

/// <summary>Model size tiers from the settings screen. Prompts are written for <see cref="Minimum"/> first.</summary>
public enum ModelTier
{
    /// <summary>No model; templates everywhere.</summary>
    Off,

    /// <summary>About 2B parameters.</summary>
    Minimum,

    /// <summary>About 4B parameters; the default.</summary>
    Recommended,

    /// <summary>12B+ local, or a cloud model.</summary>
    High,
}
