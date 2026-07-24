using System.Collections.Generic;
using UnityEngine;
using SpinMotion;

namespace GMTK
{
    /// <summary>
    /// Shared runtime access to Racing Starter Kit systems without inspector wiring.
    /// Everything is discovered from the live scene, so new game-mode managers can be
    /// spawned at runtime (see RaceBootstrap) and still reach the kit's event bus and
    /// race-position scoring.
    /// </summary>
    public static class Race
    {
        private static GameEvents _events;
        private static RaceManager _raceManager;
        private static RealTimeRacePositions _positions;

        public static GameEvents Events
        {
            get
            {
                if (_events == null)
                {
                    var rm = RaceManagerInstance;
                    if (rm != null) _events = rm.gameEvents;
                }
                return _events;
            }
        }

        public static RaceManager RaceManagerInstance
        {
            get
            {
                if (_raceManager == null)
                    _raceManager = Object.FindAnyObjectByType<RaceManager>();
                return _raceManager;
            }
        }

        public static RealTimeRacePositions Positions
        {
            get
            {
                if (_positions == null)
                    _positions = Object.FindAnyObjectByType<RealTimeRacePositions>();
                return _positions;
            }
        }

        public static bool IsRaceInProgress =>
            RaceManagerInstance != null && RaceManagerInstance.IsRaceInProgress();

        /// <summary>Number of cars registered for the current race (player + AI).</summary>
        public static int CarCount =>
            Positions != null ? Positions.CarCheckpointTrackers.Count : 0;

        /// <summary>Total race score for the car at the given race-position index (0 = player).</summary>
        public static double ScoreOf(int raceIndex)
        {
            var p = Positions;
            if (p == null || raceIndex < 0 || raceIndex >= p.RacePositionTotalScores.Count) return 0;
            return p.RacePositionTotalScores[raceIndex];
        }

        /// <summary>Root car GameObject for a race-position index, or null if not found.</summary>
        public static GameObject CarByIndex(int raceIndex)
        {
            var p = Positions;
            if (p == null) return null;
            foreach (var tracker in p.CarCheckpointTrackers)
            {
                if (tracker == null) continue;
                if (tracker.GetCarRacePositionIndex() == raceIndex)
                {
                    var controller = tracker.GetComponentInParent<CarController>();
                    return controller != null ? controller.gameObject : tracker.transform.root.gameObject;
                }
            }
            return null;
        }

        /// <summary>All car race-position indices that currently exist (0..CarCount-1).</summary>
        public static IEnumerable<int> AllCarIndices()
        {
            int count = CarCount;
            for (int i = 0; i < count; i++) yield return i;
        }

        /// <summary>Reset cached references (call on scene reload).</summary>
        public static void ClearCache()
        {
            _events = null;
            _raceManager = null;
            _positions = null;
        }
    }
}
