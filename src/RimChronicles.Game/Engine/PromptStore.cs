using RimChronicles.Llm.Prompts;
using Verse;

namespace RimChronicles.Game.Engine;

/// <summary>The prompts shipped with the mod, loaded once and shared by every LLM feature.</summary>
public static class PromptStore
{
    private static PromptLibrary? _library;
    private static bool _failed;

    /// <summary>The library, or null (templates only) if the prompts folder can't be read.</summary>
    public static PromptLibrary? Library
    {
        get
        {
            if (_library is null && !_failed && RimChroniclesMod.PromptsRoot is { } root)
            {
                try
                {
                    _library = PromptLibrary.Load(root);
                }
                catch (Exception ex)
                {
                    _failed = true;
                    Log.Error($"[Rim Chronicles] Could not load prompts from {root}; all text will use templates. {ex.Message}");
                }
            }

            return _library;
        }
    }
}
