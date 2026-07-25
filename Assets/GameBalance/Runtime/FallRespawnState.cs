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

        public void RecordTrackPosition(Vector3 position)
        {
            LastTrackPosition = position;
            HasLastTrackPosition = true;
        }

        public bool ShouldRespawn(Vector3 currentPosition, float fallDistanceBelowTrack)
        {
            return HasLastTrackPosition
                   && fallDistanceBelowTrack > 0f
                   && currentPosition.y < LastTrackPosition.y - fallDistanceBelowTrack;
        }
    }
}
