namespace Gmtk2026.GameBalance
{
    /// <summary>The three player/AI curses (checklist stages 3.8-3.10).</summary>
    public enum CurseType { Rupture, EngineSeal, SoulSwap }

    public static class CurseCatalog
    {
        /// <summary>Canonical order used by <see cref="CurseSelectionMode.OrderedRotation"/>.</summary>
        public static readonly CurseType[] All =
            { CurseType.Rupture, CurseType.EngineSeal, CurseType.SoulSwap };
    }
}
