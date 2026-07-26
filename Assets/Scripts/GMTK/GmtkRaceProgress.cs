using System.Collections.Generic;
using Gmtk2026.GameBalance;
using GMTK.Kit;
using UnityEngine;

namespace GMTK
{
    /// <summary>
    /// Supplies the race standings from the distance each car has driven along the AI waypoint path
    /// (see <see cref="TrackProgress"/>) instead of the kit's checkpoint trigger scoring. The kit
    /// formula needs a full ring of gates to order cars, which is what put hundreds of trigger
    /// volumes on the track; measuring progress on the path leaves only the finish line to build.
    /// <para>
    /// The kit's <see cref="RealTimeRacePositions"/> stays the interface every consumer reads
    /// (<see cref="Race.ScoreOf"/>, elimination, the ranking HUD), so its per-frame recompute is
    /// switched off and this component writes the score list instead. Laps keep coming from the
    /// finish gate, which leaves the lap HUD and lap events untouched. Self-attaches to the
    /// game-mode host, so no scene wiring is needed.
    /// </para>
    /// </summary>
    public class GmtkRaceProgress : MonoBehaviour
    {
        /// <summary>Metres added to a pinned car's score: longer than any track we can author.</summary>
        private const double PinnedLeadMetres = 1000000d;

        private TrackProgress track;
        private Transform[] carRoots;
        private int[] raceIndices;
        private TrackProgress.CarCursor[] cursors;
        private TrackProgress.CarCursor[] startCursors;
        private double[] scoreBonus;
        private RealTimeRacePositions standings;
        private bool ownsStandings;

        public bool HasPath => track != null && track.IsUsable;
        public float TrackLengthMetres => track != null ? track.Length : 0f;
        public int TrackedCarCount => carRoots != null ? carRoots.Length : 0;

        /// <summary>True while the standings come from waypoint progress rather than the gates.</summary>
        public bool SuppliesStandings => ownsStandings;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            var host = GMTKGameMode.GetOrCreate();
            if (host.GetComponent<GmtkRaceProgress>() == null)
                host.AddComponent<GmtkRaceProgress>();
        }

        /// <summary>Metres driven since the grid, or -1 while the car is not tracked.</summary>
        public double TotalDistanceOf(int raceIndex)
        {
            int slot = SlotOf(raceIndex);
            return slot < 0 || track == null ? -1d : track.DistanceDriven(startCursors[slot], cursors[slot]);
        }

        /// <summary>Laps finished since the grid, or -1 while the car is not tracked.</summary>
        public int LapOf(int raceIndex)
        {
            int slot = SlotOf(raceIndex);
            if (slot < 0 || track == null || !track.IsUsable) return -1;

            double driven = track.DistanceDriven(startCursors[slot], cursors[slot]);
            return driven <= 0d ? 0 : (int)(driven / track.Length);
        }

        /// <summary>
        /// Parks a car at the front of the field regardless of where it drives. Automated playtests
        /// use it to keep the player out of the elimination cascade they are exercising.
        /// </summary>
        public void PinToLead(int raceIndex)
        {
            int slot = SlotOf(raceIndex);
            if (slot >= 0) scoreBonus[slot] = PinnedLeadMetres;
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
            ReleaseStandings();

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
            startCursors = new TrackProgress.CarCursor[count];
            scoreBonus = new double[count];

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
            if (HasPath) TakeOverStandings();
        }

        private void OnRaceStarted()
        {
            if (cursors == null) return;

            // a restart puts the cars back on the grid: forget where they were on the path, and let the
            // next frame record the new grid slots as the point every distance is measured from
            for (int i = 0; i < cursors.Length; i++)
            {
                TrackProgress.Unplace(ref cursors[i]);
                TrackProgress.Unplace(ref startCursors[i]);
                scoreBonus[i] = 0d;
            }

            if (HasPath) TakeOverStandings();
        }

        private void BuildTrack()
        {
            AIWaypoints source = FindAnyObjectByType<AIWaypoints>();
            if (source == null)
            {
                Debug.LogWarning("GmtkRaceProgress: no AI waypoint path in the scene, so the standings " +
                    "stay on the kit's checkpoint scoring.");
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
                    "points, so the standings stay on the kit's checkpoint scoring.");
                return;
            }

            Debug.Log($"GmtkRaceProgress: path {points.Count} points, {track.Length:0} m, " +
                $"{carRoots.Length} cars tracked.");
        }

        /// <summary>
        /// Stops the kit recomputing the score from checkpoint triggers. Its formula ranks cars by the
        /// gate they last crossed, which cannot order anyone once the track carries a single gate.
        /// </summary>
        private void TakeOverStandings()
        {
            if (ownsStandings) return;

            standings = Race.Positions;
            if (standings == null) return;

            standings.enabled = false;
            ownsStandings = true;
        }

        private void ReleaseStandings()
        {
            if (ownsStandings && standings != null) standings.enabled = true;
            ownsStandings = false;
        }

        private void Update()
        {
            if (track == null || !track.IsUsable || cursors == null) return;
            if (!Race.IsRaceInProgress) return;

            for (int i = 0; i < carRoots.Length; i++)
            {
                if (carRoots[i] == null) continue;

                bool onTheGrid = !cursors[i].placed;
                track.Advance(ref cursors[i], carRoots[i].position);
                if (onTheGrid) startCursors[i] = cursors[i];
            }

            PublishStandings();
        }

        /// <summary>
        /// Writes metres driven, and the laps that follow from them, into the lists the kit exposes to
        /// everything that ranks cars or shows lap counts. The kit fills those lists when the race
        /// starts, so a car is skipped until its slot exists.
        /// <para>
        /// Laps have to come from here too: with a single gate the kit's own counter resets its
        /// sequence on every crossing, so the next collider of the same car banks another lap, and the
        /// grid sitting behind the start line banks one before the race even moves.
        /// </para>
        /// </summary>
        private void PublishStandings()
        {
            if (!ownsStandings || standings == null) return;

            List<double> scores = standings.RacePositionTotalScores;
            List<int> laps = standings.LapScores;

            for (int i = 0; i < raceIndices.Length; i++)
            {
                int raceIndex = raceIndices[i];
                if (raceIndex < 0) continue;

                double driven = track.DistanceDriven(startCursors[i], cursors[i]);
                if (raceIndex < scores.Count) scores[raceIndex] = driven + scoreBonus[i];
                if (raceIndex < laps.Count) laps[raceIndex] = driven <= 0d ? 0 : (int)(driven / track.Length);
            }
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
