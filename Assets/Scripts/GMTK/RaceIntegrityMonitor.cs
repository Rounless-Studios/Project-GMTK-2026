using System.Collections.Generic;
using System.Text;
using Gmtk2026.GameBalance;
using GMTK.Rccp;
using SpinMotion;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace GMTK
{
    /// <summary>
    /// Development safety net for the race's most important invariant: the cars visible in the
    /// world and the cars ranked by the starter-kit facade must be the same unique set.
    /// It removes orphaned RCCP clones, reports malformed tracker lists, and provides an F8
    /// standings/performance panel in editor and development builds.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RaceIntegrityMonitor : MonoBehaviour
    {
        private readonly StringBuilder report = new();
        private readonly HashSet<GameObject> rankedRoots = new();
        private float nextAuditAt;
        private float smoothedFps;
        private bool showDiagnostics;
        private bool racersRegistered;
        private string lastProblem;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            GameObject host = GMTKGameMode.GetOrCreate();
            if (host.GetComponent<RaceIntegrityMonitor>() == null)
                host.AddComponent<RaceIntegrityMonitor>();
        }

        private void Start()
        {
            GameEvents events = Race.Events;
            if (events == null) return;
            events.PlayersCheckpointTrackersAssignedEvent.AddListener(OnPlayersAssigned);
            events.RaceStartedEvent.AddListener(AuditNow);
            events.RestartRaceEvent.AddListener(OnRestartRace);
        }

        private void OnDestroy()
        {
            GameEvents events = Race.Events;
            if (events == null) return;
            events.PlayersCheckpointTrackersAssignedEvent.RemoveListener(OnPlayersAssigned);
            events.RaceStartedEvent.RemoveListener(AuditNow);
            events.RestartRaceEvent.RemoveListener(OnRestartRace);
        }

        private void OnPlayersAssigned(List<CheckpointTracker> _)
        {
            racersRegistered = true;
            AuditNow();
        }

        private void OnRestartRace()
        {
            // The starter kit clears and rebuilds its tracker list during restart. Auditing that
            // transient state would classify every freshly spawned car as an orphan.
            racersRegistered = false;
            lastProblem = string.Empty;
        }

        private void Update()
        {
            float instantaneous = 1f / Mathf.Max(0.001f, Time.unscaledDeltaTime);
            smoothedFps = Mathf.Lerp(smoothedFps <= 0f ? instantaneous : smoothedFps,
                instantaneous, 0.08f);

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame)
                showDiagnostics = !showDiagnostics;
#endif

            if (racersRegistered && Time.unscaledTime >= nextAuditAt)
            {
                nextAuditAt = Time.unscaledTime + 2f;
                AuditNow();
            }
        }

        public void AuditNow()
        {
            RealTimeRacePositions positions = Race.Positions;
            if (positions == null) return;

            int expected = GameBalance.Current.race.TotalRacerCount;
            if (!racersRegistered)
            {
                if (positions.CarCheckpointTrackers.Count != expected)
                    return;

                // Covers scene configurations where the assignment event was broadcast before this
                // runtime service subscribed. A complete tracker set is an equivalent snapshot.
                racersRegistered = true;
            }

            // An empty list is always a lifecycle state, never a six-racer integrity failure.
            if (positions.CarCheckpointTrackers.Count == 0)
                return;

            rankedRoots.Clear();
            var indices = new HashSet<int>();
            int nullTrackers = 0;
            int duplicateIndices = 0;

            foreach (CheckpointTracker tracker in positions.CarCheckpointTrackers)
            {
                if (tracker == null)
                {
                    nullTrackers++;
                    continue;
                }

                rankedRoots.Add(tracker.transform.root.gameObject);
                if (!indices.Add(tracker.GetCarRacePositionIndex()))
                    duplicateIndices++;
            }

            int removedOrphans = 0;
            GmtkRccpVehicle[] vehicles =
                FindObjectsByType<GmtkRccpVehicle>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            foreach (GmtkRccpVehicle vehicle in vehicles)
            {
                GameObject root = vehicle.transform.root.gameObject;
                if (rankedRoots.Contains(root) || !root.name.StartsWith("GMTK_RCCP_"))
                    continue;

                root.SetActive(false);
                Destroy(root);
                removedOrphans++;
            }

            bool valid = positions.CarCheckpointTrackers.Count == expected &&
                         rankedRoots.Count == expected &&
                         nullTrackers == 0 &&
                         duplicateIndices == 0;
            if (valid && removedOrphans == 0)
            {
                lastProblem = string.Empty;
                return;
            }

            lastProblem =
                $"Race integrity repaired/failed: expected={expected}, " +
                $"trackers={positions.CarCheckpointTrackers.Count}, uniqueCars={rankedRoots.Count}, " +
                $"nullTrackers={nullTrackers}, duplicateIndices={duplicateIndices}, " +
                $"orphanCarsRemoved={removedOrphans}";
            Debug.LogError(lastProblem, this);
        }

        private void OnGUI()
        {
            if (!Debug.isDebugBuild && !Application.isEditor) return;
            if (!showDiagnostics && string.IsNullOrEmpty(lastProblem)) return;

            BuildReport();
            GUI.Box(new Rect(12f, 190f, 430f, 30f + report.Length / 2.1f), report.ToString());
        }

        private void BuildReport()
        {
            report.Clear();
            report.AppendLine("RACE DIAGNOSTICS  [F8]");
            report.Append("FPS ").Append(smoothedFps.ToString("0"))
                .Append("  Cars ").Append(Race.CarCount)
                .Append("  Cameras ")
                .Append(FindObjectsByType<Camera>(FindObjectsSortMode.None).Length)
                .AppendLine();
            report.Append("Difficulty ")
                .Append(DifficultyManager.Instance != null
                    ? DifficultyManager.Instance.Current
                    : DifficultyPreset.Normal)
                .AppendLine("  [F6 cycles]");

            if (!string.IsNullOrEmpty(lastProblem))
                report.AppendLine(lastProblem);

            var progress = FindFirstObjectByType<GmtkRaceProgress>();
            var order = new List<int>(Race.AllCarIndices());
            order.Sort((a, b) =>
            {
                int score = Race.ScoreOf(b).CompareTo(Race.ScoreOf(a));
                return score != 0 ? score : a.CompareTo(b);
            });

            for (int place = 0; place < order.Count; place++)
            {
                int index = order[place];
                GameObject car = Race.CarByIndex(index);
                report.Append(place + 1).Append(". ")
                    .Append(index == 0 ? "PLAYER" : $"AI_{index:00}")
                    .Append("  lap ").Append(progress != null ? progress.LapOf(index) : -1)
                    .Append("  score ").Append(Race.ScoreOf(index).ToString("0.0"))
                    .Append("  ").Append(car != null && car.activeInHierarchy ? "ACTIVE" : "INACTIVE")
                    .AppendLine();
            }
        }
    }
}
