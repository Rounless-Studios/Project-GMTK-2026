using System.Collections;
using System.Collections.Generic;
using SpinMotion;
using UnityEngine;

namespace GMTK
{
    /// <summary>
    /// Keeps exactly one <see cref="AudioListener"/> enabled.
    /// <para>
    /// The kit's menu and finish cameras each carry one and switch it on for their own phase, written
    /// on the assumption that no other listener exists. The RCCP camera rig brought its own and never
    /// switches it off. Both are enabled from scene load, so Unity logs "There are 2 audio listeners
    /// in the scene" every frame — which also flushes real errors out of the 800-line console buffer
    /// within seconds. Neither side can be fixed where it lives (both are third-party), so the
    /// game-mode host arbitrates: the rig keeps the listener and the kit's are switched back off after
    /// every phase change. Self-attaches, so no scene wiring is needed.
    /// </para>
    /// </summary>
    public class AudioListenerGuard : MonoBehaviour
    {
        private readonly List<AudioListener> listeners = new();
        private AudioListener kept;

        /// <summary>The listener left enabled, for diagnostics.</summary>
        public AudioListener KeptListener => kept;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            var host = GMTKGameMode.GetOrCreate();
            if (host.GetComponent<AudioListenerGuard>() == null)
                host.AddComponent<AudioListenerGuard>();
        }

        private void Start()
        {
            Collect();
            Enforce();

            GameEvents events = Race.Events;
            if (events == null) return;

            events.OnClickPlayRaceEvent.AddListener(EnforceAfterThisFrame);
            events.RaceStartedEvent.AddListener(EnforceAfterThisFrame);
            events.RestartRaceEvent.AddListener(EnforceAfterThisFrame);
            events.RaceFinishedEvent.AddListener(OnRaceFinished);
            events.PlayersCheckpointTrackersAssignedEvent.AddListener(OnPlayersAssigned);
        }

        private void OnDestroy()
        {
            GameEvents events = Race.Events;
            if (events == null) return;

            events.OnClickPlayRaceEvent.RemoveListener(EnforceAfterThisFrame);
            events.RaceStartedEvent.RemoveListener(EnforceAfterThisFrame);
            events.RestartRaceEvent.RemoveListener(EnforceAfterThisFrame);
            events.RaceFinishedEvent.RemoveListener(OnRaceFinished);
            events.PlayersCheckpointTrackersAssignedEvent.RemoveListener(OnPlayersAssigned);
        }

        private void OnRaceFinished(RaceFinishType type) => EnforceAfterThisFrame();

        private void OnPlayersAssigned(List<CheckpointTracker> trackers)
        {
            // spawned cars can bring cameras of their own, so the roster is rebuilt once per grid
            Collect();
            Enforce();
        }

        /// <summary>
        /// The kit switches its listener on inside the event we are reacting to, and listener order is
        /// not defined, so the correction waits until every handler for this frame has run.
        /// </summary>
        private void EnforceAfterThisFrame()
        {
            Enforce();
            if (isActiveAndEnabled) StartCoroutine(EnforceNextFrame());
        }

        private IEnumerator EnforceNextFrame()
        {
            yield return null;
            Enforce();
        }

        private void Collect()
        {
            listeners.Clear();
            listeners.AddRange(
                FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            kept = Choose();
        }

        /// <summary>
        /// Prefers a listener no kit phase camera owns: those are switched on and off as the race moves
        /// between menu, race and results, which is exactly what must stop deciding who hears the game.
        /// </summary>
        private AudioListener Choose()
        {
            AudioListener fallback = null;

            foreach (AudioListener listener in listeners)
            {
                if (listener == null) continue;
                fallback ??= listener;

                bool ownedByKitCamera = listener.GetComponent<StartMenuCamera>() != null ||
                    listener.GetComponent<RaceFinishCamera>() != null;
                if (ownedByKitCamera) continue;
                if (!listener.gameObject.activeInHierarchy) continue;

                return listener;
            }

            return fallback;
        }

        private void Enforce()
        {
            if (kept == null) kept = Choose();

            foreach (AudioListener listener in listeners)
            {
                if (listener == null) continue;
                bool shouldHear = listener == kept;
                if (listener.enabled != shouldHear) listener.enabled = shouldHear;
            }
        }
    }
}
