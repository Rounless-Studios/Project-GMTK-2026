using System.Collections.Generic;
using System.Text;
using Gmtk2026.GameBalance;
using SpinMotion;
using UnityEngine;

namespace GMTK
{
    /// <summary>
    /// Measures how far every car has driven along the AI waypoint path (see
    /// <see cref="TrackProgress"/>), so ranking and elimination can stop depending on hundreds of
    /// checkpoint trigger gates. This stage only measures: it runs beside the kit's checkpoint
    /// scoring and reports whether the two produce the same running order, so the switch-over can be
    /// verified on a real lap before the gates are removed. Self-attaches to the game-mode host, so
    /// no scene wiring is needed.
    /// </summary>
    public class GmtkRaceProgress : MonoBehaviour
    {
        // diagnostics cadence only: the comparison formats a string, so it must not run every frame
        private const float ReportIntervalSeconds = 3f;

        private TrackProgress track;
        private Transform[] carRoots;
        private int[] raceIndices;
        private TrackProgress.CarCursor[] cursors;
        private int[] progressOrder;
        private int[] checkpointOrder;
        private readonly StringBuilder report = new();
        private float nextReportTime;

        public bool HasPath => track != null && track.IsUsable;
        public float TrackLengthMetres => track != null ? track.Length : 0f;
        public int TrackedCarCount => carRoots != null ? carRoots.Length : 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            var host = GMTKGameMode.GetOrCreate();
            if (host.GetComponent<GmtkRaceProgress>() == null)
                host.AddComponent<GmtkRaceProgress>();
        }

        /// <summary>Distance driven along the path, or -1 while the car is not tracked.</summary>
        public double TotalDistanceOf(int raceIndex)
        {
            int slot = SlotOf(raceIndex);
            return slot < 0 || track == null ? -1d : track.TotalDistance(cursors[slot]);
        }

        /// <summary>Completed laps, or -1 while the car is not tracked.</summary>
        public int LapOf(int raceIndex)
        {
            int slot = SlotOf(raceIndex);
            return slot < 0 ? -1 : cursors[slot].lap;
        }

        private void Start()
        {
            GameEvents events = Race.Events;
            if (events == null) return;

            events.PlayersCheckpointTrackersAssignedEvent.AddListener(OnPlayersAssigned);
            events.RaceStartedEvent.AddListener(OnRaceStarted);
        }

        private void OnDestroy()
        {
            GameEvents events = Race.Events;
            if (events == null) return;

            events.PlayersCheckpointTrackersAssignedEvent.RemoveListener(OnPlayersAssigned);
            events.RaceStartedEvent.RemoveListener(OnRaceStarted);
        }

        private void OnPlayersAssigned(List<CheckpointTracker> trackers)
        {
            if (trackers == null) return;

            int count = 0;
            foreach (CheckpointTracker tracker in trackers)
                if (tracker != null) count++;

            carRoots = new Transform[count];
            raceIndices = new int[count];
            cursors = new TrackProgress.CarCursor[count];
            progressOrder = new int[count];
            checkpointOrder = new int[count];

            int slot = 0;
            foreach (CheckpointTracker tracker in trackers)
            {
                if (tracker == null) continue;
                // the kit parents a tracker under the vehicle root, whichever spawner built the car
                carRoots[slot] = tracker.transform.root;
                raceIndices[slot] = tracker.GetCarRacePositionIndex();
                slot++;
            }

            BuildTrack();
        }

        private void OnRaceStarted()
        {
            if (cursors == null) return;

            // a restart puts the cars back on the grid: forget where they were on the path
            for (int i = 0; i < cursors.Length; i++)
                TrackProgress.Unplace(ref cursors[i]);

            nextReportTime = 0f;
        }

        private void BuildTrack()
        {
            AIWaypoints source = FindAnyObjectByType<AIWaypoints>();
            if (source == null)
            {
                Debug.LogWarning("GmtkRaceProgress: no AI waypoint path in the scene, " +
                    "so race progress cannot be measured.");
                return;
            }

            var points = new List<Vector3>();
            Transform[] candidates = source.GetComponentsInChildren<Transform>(true);

            // same selection rule as GmtkRccpWaypointPath, so the AI and the ranking read one path
            for (int i = 1; i < candidates.Length; i++)
                if (candidates[i].TryGetComponent(out MeshRenderer _))
                    points.Add(candidates[i].position);

            track = new TrackProgress(points);

            if (!track.IsUsable)
            {
                Debug.LogWarning($"GmtkRaceProgress: the waypoint path has only {points.Count} usable " +
                    "points, so race progress cannot be measured.");
                return;
            }

            Debug.Log($"GmtkRaceProgress: path {points.Count} points, {track.Length:0} m, " +
                $"{carRoots.Length} cars tracked.");
        }

        private void Update()
        {
            if (track == null || !track.IsUsable || cursors == null) return;
            if (!Race.IsRaceInProgress) return;

            for (int i = 0; i < carRoots.Length; i++)
            {
                if (carRoots[i] == null) continue;
                track.Advance(ref cursors[i], carRoots[i].position);
            }

            if (Time.time < nextReportTime) return;
            nextReportTime = Time.time + ReportIntervalSeconds;
            Report();
        }

        private void Report()
        {
            if (Race.Positions == null) return;

            int count = carRoots.Length;
            SortByScore(progressOrder, count, true);
            SortByScore(checkpointOrder, count, false);

            bool sameOrder = true;
            for (int i = 0; i < count; i++)
            {
                if (progressOrder[i] == checkpointOrder[i]) continue;
                sameOrder = false;
                break;
            }

            report.Clear();
            report.Append(sameOrder
                ? "race progress: waypoint and checkpoint order agree"
                : "race progress: ORDER DIFFERS");
            report.Append(" | waypoint:");
            AppendOrder(progressOrder, count);
            report.Append(" | checkpoint:");
            AppendOrder(checkpointOrder, count);

            for (int i = 0; i < count; i++)
            {
                int slot = progressOrder[i];
                report.AppendLine();
                report.Append("  car ").Append(raceIndices[slot])
                    .Append(" lap ").Append(cursors[slot].lap)
                    .Append(" at ").Append(cursors[slot].progressMetres.ToString("0"))
                    .Append(" m, driven ").Append(track.TotalDistance(cursors[slot]).ToString("0"))
                    .Append(" m | kit score ").Append(Race.ScoreOf(raceIndices[slot]).ToString("0"));
            }

            Debug.Log(report.ToString());
        }

        private void AppendOrder(int[] order, int count)
        {
            for (int i = 0; i < count; i++)
                report.Append(' ').Append(raceIndices[order[i]]);
        }

        /// <summary>Leading car first. Insertion sort: the grid is at most a handful of cars.</summary>
        private void SortByScore(int[] order, int count, bool useWaypointProgress)
        {
            for (int i = 0; i < count; i++)
                order[i] = i;

            for (int i = 1; i < count; i++)
            {
                int slot = order[i];
                double score = ScoreOfSlot(slot, useWaypointProgress);
                int j = i - 1;

                while (j >= 0 && ScoreOfSlot(order[j], useWaypointProgress) < score)
                {
                    order[j + 1] = order[j];
                    j--;
                }

                order[j + 1] = slot;
            }
        }

        private double ScoreOfSlot(int slot, bool useWaypointProgress)
        {
            return useWaypointProgress
                ? track.TotalDistance(cursors[slot])
                : Race.ScoreOf(raceIndices[slot]);
        }

        private int SlotOf(int raceIndex)
        {
            if (raceIndices == null) return -1;

            for (int i = 0; i < raceIndices.Length; i++)
                if (raceIndices[i] == raceIndex) return i;

            return -1;
        }
    }
}
