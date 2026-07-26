using UnityEngine;

namespace Gmtk2026.GameBalance
{
    /// <summary>
    /// Engine-light state for remembering a vehicle's last supported track position and deciding
    /// when it has fallen far enough below that point to be respawned.
    /// </summary>
    public sealed class FallRespawnState
    {
        public bool HasLastTrackPosition { get; private set; }
        public Vector3 LastTrackPosition { get; private set; }

        /// <summary>Seconds since the car last had track under it.</summary>
        public float AirborneSeconds { get; private set; }

        public void RecordTrackPosition(Vector3 position)
        {
            LastTrackPosition = position;
            HasLastTrackPosition = true;
            AirborneSeconds = 0f;
        }

        /// <summary>Counts time with nothing supporting the car.</summary>
        public void TickAirborne(float deltaSeconds)
        {
            if (deltaSeconds > 0f) AirborneSeconds += deltaSeconds;
        }

        public bool ShouldRespawn(Vector3 currentPosition, float fallDistanceBelowTrack)
        {
            return ShouldRespawn(currentPosition, fallDistanceBelowTrack, 0f);
        }

        /// <summary>
        /// A car is recovered once it is far enough below the track it came from, or once it has
        /// been unsupported for too long. The second rule matters because a car flung off the map
        /// can sail along above its last safe height for a long time, and a car sliding down a
        /// slope can otherwise keep falling forever while the drop is measured against itself.
        /// </summary>
        public bool ShouldRespawn(
            Vector3 currentPosition,
            float fallDistanceBelowTrack,
            float maximumAirborneSeconds)
        {
            if (!HasLastTrackPosition) return false;

            if (fallDistanceBelowTrack > 0f &&
                currentPosition.y < LastTrackPosition.y - fallDistanceBelowTrack)
                return true;

            return maximumAirborneSeconds > 0f && AirborneSeconds >= maximumAirborneSeconds;
        }
    }
}
