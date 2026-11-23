namespace RimChronicles.Core.Crisis;

/// <summary>What became of the High Stellarch (crisis spec §5.1, C13).</summary>
public static class StellarchFate
{
    /// <summary>Not yet met.</summary>
    public const string None = "";

    public const string Alive = "alive";
    public const string Dead = "dead";
    public const string Captured = "captured";

    /// <summary>The Stellarch is out of the war: the Empire's last leader is gone.</summary>
    public static bool Fallen(string fate) => fate is Dead or Captured;
}

/// <summary>
/// The High Stellarch's rules (crisis spec §5.3.1, C13): there's one, it leads the final assault and the Citadel's honor
/// guard, and it saves itself once by a scripted Farskip when badly hurt or downed. Once dead or captured it never
/// returns, and its fall carries the colony's assault on the capital (the Resistance ending).
/// </summary>
public static class StellarchRules
{
    /// <summary>Below this share of its health (or downed, but alive) the Stellarch skips away, once.</summary>
    public const float EscapeHealth = 0.4f;

    public static bool ShouldEscape(CrisisState state, float healthShare, bool downed, bool dead) =>
        !dead && !state.StellarchEscaped && !StellarchFate.Fallen(state.StellarchFate) && (downed || healthShare < EscapeHealth);

    /// <summary>The Stellarch skipped away: it never does again.</summary>
    public static void Escaped(CrisisState state) => state.StellarchEscaped = true;

    /// <summary>A Stellarch can lead (the final assault, the keep) while it hasn't fallen.</summary>
    public static bool CanLead(CrisisState state) => !StellarchFate.Fallen(state.StellarchFate);

    /// <summary>The Stellarch died or was taken. Returns true the first time (to announce it and carry the assault).</summary>
    public static bool Fell(CrisisState state, string fate, int today)
    {
        if (StellarchFate.Fallen(state.StellarchFate) || !StellarchFate.Fallen(fate))
        {
            return false;
        }

        state.StellarchFate = fate;
        state.StellarchFellDay = today;
        return true;
    }
}
