using UnityEngine;

namespace Gmtk2026.GameBalance
{
    /// <summary>
    /// Shared curse cooldown + selection rule (checklist stage 3.7), as a plain, unit-testable
    /// class. All three curses share one <c>sharedCooldownSeconds</c> timer. Under
    /// OrderedRotation the next curse advances only after a *successful* cast; a failed attempt
    /// (wrong answer / timeout) still spends the cooldown but keeps the same next curse so the
    /// player is not punished with a different curse. An overtake win clears the cooldown at once.
    /// The rule is caster-agnostic — the player casts it via a quiz, AI casts it directly.
    /// </summary>
    public class CurseCooldownState
    {
        private readonly CurseSettings s;
        private float cooldownTimer;
        private int rotationIndex;

        public bool IsReady => cooldownTimer <= 0f;
        public float CooldownRemaining => Mathf.Max(0f, cooldownTimer);

        public CurseCooldownState(CurseSettings settings) { s = settings; }

        /// <summary>The curse an OrderedRotation cast would use next.</summary>
        public CurseType NextOrdered() => CurseCatalog.All[rotationIndex % CurseCatalog.All.Length];

        /// <summary>Spend the cooldown and advance the rotation after a successful cast.</summary>
        public void OnCastSucceeded()
        {
            rotationIndex = (rotationIndex + 1) % CurseCatalog.All.Length;
            cooldownTimer = s.sharedCooldownSeconds;
        }

        /// <summary>Spend the cooldown after a wrong answer / timeout (rotation unchanged).</summary>
        public void OnCastFailed() => cooldownTimer = s.sharedCooldownSeconds;

        /// <summary>Overtake reward: make a curse available immediately.</summary>
        public void ResetCooldown() => cooldownTimer = 0f;

        public void Tick(float deltaTime)
        {
            if (cooldownTimer > 0f) cooldownTimer = Mathf.Max(0f, cooldownTimer - deltaTime);
        }

        public void ResetForRace()
        {
            cooldownTimer = 0f;
            rotationIndex = 0;
        }
    }
}
