using System.Collections.Generic;
using UnityEngine;
using Gmtk2026.GameBalance;

namespace GMTK
{
    /// <summary>
    /// Player overtake challenge (checklist stage 10). Every <c>checkIntervalSeconds</c> it picks
    /// a random active rival the player is currently behind (skipping the challenge when the
    /// player already leads them all), then runs an <see cref="OvertakeChallengeState"/>. Success
    /// refunds a boost charge and resets/reduces the curse cooldown; failure binds the player with
    /// a hard slowdown for <c>bindDurationSeconds</c>. Falling to last place immediately releases
    /// the bind and cancels any active challenge. Self-attaches to the GMTK host. Camera / UI /
    /// chain VFX and the slowdown actuation (via <see cref="BindSpeedMultiplier"/>) layer on top.
    /// </summary>
    public class OvertakeManager : MonoBehaviour
    {
        public const int PlayerIndex = 0;

        public static OvertakeManager Instance { get; private set; }

        public OvertakeChallengeState Challenge { get; private set; }
        public int RivalIndex { get; private set; } = -1;
        public bool IsBound { get; private set; }
        public float BindSpeedMultiplier => IsBound ? GameBalance.Current.overtake.bindSpeedMultiplier : 1f;

        public static event System.Action<int> ChallengeStarted;   // rival index
        public static event System.Action<int> ChallengeSucceeded; // rival index
        public static event System.Action<int> ChallengeFailed;    // rival index
        public static event System.Action BindReleased;

        private OvertakeSettings O => GameBalance.Current.overtake;
        private float nextCheckTime;
        private float bindEndTime;
        private bool armed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            var host = GMTKGameMode.GetOrCreate();
            if (host.GetComponent<OvertakeManager>() == null)
                host.AddComponent<OvertakeManager>();
        }

        private void Awake() => Instance = this;
        private void OnDestroy()
        {
            SpinMotion.GameEvents e = Race.Events;
            if (e != null)
            {
                e.RaceStartedEvent.RemoveListener(OnRaceStarted);
                e.RestartRaceEvent.RemoveListener(OnRaceStarted);
                e.RaceFinishedEvent.RemoveListener(OnRaceFinished);
            }
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            var e = Race.Events;
            if (e == null) return;
            e.RaceStartedEvent.AddListener(OnRaceStarted);
            e.RestartRaceEvent.AddListener(OnRaceStarted);
            e.RaceFinishedEvent.AddListener(OnRaceFinished);
        }

        private void OnRaceFinished(SpinMotion.RaceFinishType _) => armed = false;

        private void OnRaceStarted()
        {
            Challenge = new OvertakeChallengeState(O);
            RivalIndex = -1;
            IsBound = false;
            armed = true;
            nextCheckTime = Time.time + O.checkIntervalSeconds;
        }

        private void Update()
        {
            if (!armed || !Race.IsRaceInProgress || Challenge == null) return;

            if (PlayerIsLast())
            {
                if (IsBound) ReleaseBind();
                if (Challenge.Status == OvertakeStatus.Active) { Challenge.Cancel(); RivalIndex = -1; }
            }

            if (IsBound && Time.time >= bindEndTime) ReleaseBind();

            if (Challenge.Status == OvertakeStatus.Active && RivalIndex >= 0)
            {
                var st = Challenge.Tick(Time.deltaTime, PlayerAhead(RivalIndex));
                if (st == OvertakeStatus.Succeeded) OnSuccess();
                else if (st == OvertakeStatus.Failed) OnFail();
            }
            else if (!IsBound && Challenge.Status != OvertakeStatus.Active && Time.time >= nextCheckTime)
            {
                nextCheckTime = Time.time + O.checkIntervalSeconds;
                TryBeginChallenge();
            }
        }

        private void TryBeginChallenge()
        {
            int rival = PickRivalAhead();
            if (rival < 0) return;            // already leading everyone -> skip this check
            RivalIndex = rival;
            Challenge.Begin();
            ChallengeStarted?.Invoke(rival);
        }

        private void OnSuccess()
        {
            var player = Race.CarByIndex(PlayerIndex);
            if (player != null)
            {
                player.GetComponent<BoostController>()?.RewardOvertake();
                var curse = player.GetComponent<CurseController>();
                if (curse != null)
                {
                    if (O.cooldownRewardMode == OvertakeCooldownRewardMode.Reduce)
                        curse.ReduceCooldown(O.cooldownReductionSeconds);
                    else
                        curse.ResetCooldown();
                }
            }
            ChallengeSucceeded?.Invoke(RivalIndex);
            RivalIndex = -1;
        }

        private void OnFail()
        {
            IsBound = true;
            bindEndTime = Time.time + O.bindDurationSeconds;
            ChallengeFailed?.Invoke(RivalIndex);
            RivalIndex = -1;
        }

        private void ReleaseBind()
        {
            IsBound = false;
            BindReleased?.Invoke();
        }

        // a random active rival the player is currently behind; -1 if the player leads them all
        private int PickRivalAhead()
        {
            var elim = Object.FindAnyObjectByType<EliminationManager>();
            double playerScore = Race.ScoreOf(PlayerIndex);
            var ahead = new List<int>();
            int count = Race.CarCount;
            for (int i = 0; i < count; i++)
            {
                if (i == PlayerIndex) continue;
                if (elim != null && elim.IsEliminated(i)) continue;
                if (Race.ScoreOf(i) > playerScore) ahead.Add(i);
            }
            if (ahead.Count == 0) return -1;
            return ahead[Random.Range(0, ahead.Count)];
        }

        private bool PlayerAhead(int rival)
        {
            if (rival < 0) return false;
            return Race.ScoreOf(PlayerIndex) > Race.ScoreOf(rival);
        }

        private bool PlayerIsLast()
        {
            var elim = Object.FindAnyObjectByType<EliminationManager>();
            double playerScore = Race.ScoreOf(PlayerIndex);
            int count = Race.CarCount;
            for (int i = 0; i < count; i++)
            {
                if (i == PlayerIndex) continue;
                if (elim != null && elim.IsEliminated(i)) continue;
                if (Race.ScoreOf(i) < playerScore) return false;
            }
            return true;
        }
    }
}
