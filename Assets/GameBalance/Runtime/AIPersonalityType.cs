namespace Gmtk2026.GameBalance
{
    /// <summary>
    /// The four confirmed AI personalities (2026-07-26). This lives in the balance assembly so
    /// <see cref="AiPersonalityProfile"/> can be keyed by it; the gameplay layer reaches it through
    /// <c>using Gmtk2026.GameBalance;</c>, the same way <see cref="CurseType"/> is shared.
    /// <para>
    /// Declaration order is deliberately unchanged from the original gameplay-side enum because the
    /// value is serialized as an int. The grid composition is data, not enum order: it comes from
    /// <see cref="AISettings.personalityAssignments"/>.
    /// </para>
    /// </summary>
    public enum AIPersonalityType
    {
        /// <summary>생존자 — tidy lines, no aggression, best at shrugging curses off.</summary>
        CleanRacer,

        /// <summary>난폭자 — pulls its aim point toward the player to force contact.</summary>
        Rammer,

        /// <summary>봉쇄자 — gives up some pace to slide across the player's lane.</summary>
        Blocker,

        /// <summary>폭주광 — fastest, brakes least for corners, weakest curse defence.</summary>
        Reckless,
    }
}
