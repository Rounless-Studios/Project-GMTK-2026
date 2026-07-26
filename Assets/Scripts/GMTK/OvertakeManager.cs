using System.Collections.Generic;
using UnityEngine;
using Gmtk2026.GameBalance;

namespace GMTK
{
    /// <summary>
    /// Rewards ordinary player overtakes with boost and curse recovery. The older directed
    /// challenge can still be enabled in settings, but is off by default so the purge race itself
    /// creates the objective instead of a parallel random mission.
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
        private bool passiveTrackingReady;
        private bool[] playerWasAhead;
        private float[] lastPassiveRewardAt;

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
            GMTK.Kit.GameEvents e = Race.Events;
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

        private void OnRaceFinished(GMTK.Kit.RaceFinishType _) => armed = false;

        private void OnRaceStarted()
        {
            Challenge = new OvertakeChallengeState(O);
            RivalIndex = -1;
            IsBound = false;
            armed = true;
            passiveTrackingReady = false;
            int carCount = Mathf.Max(0, Race.CarCount);
            playerWasAhead = new bool[carCount];
            lastPassiveRewardAt = new float[carCount];
            nextCheckTime = Time.time + O.checkIntervalSeconds;
        }

        private void Update()
        {
            if (!armed || !Race.IsRaceInProgress || Challenge == null) return;

            if (!O.enableDirectedChallenges)
            {
                UpdatePassiveOvertakes();
                return;
            }

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
            RewardOvertake(RivalIndex);
            RivalIndex = -1;
        }

        private void RewardOvertake(int rivalIndex)
        {
            GameObject player = Race.CarByIndex(PlayerIndex);
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
            ChallengeSucceeded?.Invoke(rivalIndex);
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

        private void UpdatePassiveOvertakes()
        {
            int count = Race.CarCount;
            if (playerWasAhead == null || playerWasAhead.Length != count)
            {
                playerWasAhead = new bool[count];
                lastPassiveRewardAt = new float[count];
                passiveTrackingReady = false;
            }

            double playerScore = Race.ScoreOf(PlayerIndex);
            var elimination = EliminationManager.Instance;
            for (int i = 0; i < count; i++)
            {
                if (i == PlayerIndex ||
                    (elimination != null && elimination.IsEliminated(i)))
                    continue;

                bool isAhead = playerScore > Race.ScoreOf(i);
                if (passiveTrackingReady && isAhead && !playerWasAhead[i] &&
                    Time.time - lastPassiveRewardAt[i] >=
                    O.passiveRewardCooldownSeconds)
                {
                    lastPassiveRewardAt[i] = Time.time;
                    RewardOvertake(i);
                }
                playerWasAhead[i] = isAhead;
            }
            passiveTrackingReady = true;
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
