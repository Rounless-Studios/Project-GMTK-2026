using UnityEngine;

namespace Gmtk2026.GameBalance
{
    public enum OvertakeStatus { Idle, Active, Succeeded, Failed }

    /// <summary>
    /// Overtake-challenge lifecycle (checklist stage 3.12 / stage 10) as a plain, unit-testable
    /// class. Once begun the player has <c>challengeDurationSeconds</c> to get and stay ahead of
    /// the chosen rival for <c>requiredLeadHoldSeconds</c> of continuous lead. Dropping behind
    /// resets the held time; running out of time fails. Rival selection, reward payout and the
    /// failure bind live in the manager — this owns only the timed hold rule.
    /// </summary>
    public class OvertakeChallengeState
    {
        private readonly OvertakeSettings s;

        public OvertakeStatus Status { get; private set; } = OvertakeStatus.Idle;
        public float TimeRemaining { get; private set; }
        public float LeadHeld { get; private set; }

        public OvertakeChallengeState(OvertakeSettings settings) { s = settings; }

        public void Begin()
        {
            Status = OvertakeStatus.Active;
            TimeRemaining = s.challengeDurationSeconds;
            LeadHeld = 0f;
        }

        /// <summary>Advance an active challenge; returns the resulting status.</summary>
        public OvertakeStatus Tick(float deltaTime, bool playerAheadOfRival)
        {
            if (Status != OvertakeStatus.Active) return Status;

            if (playerAheadOfRival)
            {
                LeadHeld += deltaTime;
                if (LeadHeld >= s.requiredLeadHoldSeconds)
                {
                    Status = OvertakeStatus.Succeeded;
                    return Status;
                }
            }
            else
            {
                LeadHeld = 0f;   // lead must be held continuously
            }

            TimeRemaining = Mathf.Max(0f, TimeRemaining - deltaTime);
            if (TimeRemaining <= 0f) Status = OvertakeStatus.Failed;
            return Status;
        }

        /// <summary>Abandon the challenge (e.g. the player fell to last place).</summary>
        public void Cancel()
        {
            Status = OvertakeStatus.Idle;
            TimeRemaining = 0f;
            LeadHeld = 0f;
        }
    }
}
