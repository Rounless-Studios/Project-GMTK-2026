using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using GMTK.Kit;
using Gmtk2026.GameBalance;

namespace GMTK.Rccp
{
    /// <summary>
    /// RCCP vehicle spawner that preserves the Racing Starter Kit event, checkpoint, position,
    /// and restart contracts.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GmtkRccpPlayersSpawner : MonoBehaviour
    {
        [SerializeField] private GameEvents gameEvents;
        [SerializeField] private RCCP_CarController vehiclePrefab;
        [SerializeField] private CheckpointTracker checkpointTrackerPrefab;
        [SerializeField] private List<Transform> spawnPoints = new();
        [SerializeField] private PlayerSpawnIndex playerSpawnIndex = PlayerSpawnIndex.First;
        [SerializeField] private int customPlayerSpawnIndex;

        private readonly List<SpawnedVehicle> spawnedVehicles = new();
        private readonly List<CheckpointTracker> checkpointTrackers = new();
        private static GmtkRccpPlayersSpawner authority;
        private bool subscribed;

        private readonly struct SpawnedVehicle
        {
            public readonly GameObject GameObject;
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;

            public SpawnedVehicle(GameObject gameObject, Vector3 position, Quaternion rotation)
            {
                GameObject = gameObject;
                Position = position;
                Rotation = rotation;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ReplaceLegacySpawner()
        {
            PlayersSpawner[] legacySpawners =
                Object.FindObjectsByType<PlayersSpawner>(FindObjectsSortMode.None);

            PlayersSpawner source = null;
            foreach (PlayersSpawner legacy in legacySpawners)
            {
                if (source == null || legacy.spawnPoints.Count > source.spawnPoints.Count)
                    source = legacy;
            }

            GmtkRccpPlayersSpawner[] replacements =
                Object.FindObjectsByType<GmtkRccpPlayersSpawner>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            GmtkRccpPlayersSpawner replacement =
                source != null ? source.GetComponent<GmtkRccpPlayersSpawner>() : null;

            if (replacement == null && source != null)
                replacement = source.gameObject.AddComponent<GmtkRccpPlayersSpawner>();

            foreach (PlayersSpawner legacy in legacySpawners)
            {
                UnsubscribeLegacy(legacy);
                legacy.enabled = false;
                Object.Destroy(legacy);
            }

            foreach (GmtkRccpPlayersSpawner candidate in replacements)
            {
                if (candidate == replacement)
                    continue;

                candidate.enabled = false;
                Object.Destroy(candidate);
            }

            if (replacement == null)
                return;

            replacement.CopyFromLegacy(source);
            authority = replacement;
            replacement.Subscribe();
        }

        private void Start()
        {
            if (authority == null && spawnPoints.Count > 0)
                authority = this;
            Subscribe();
        }

        private void OnDestroy()
        {
            if (subscribed && gameEvents != null)
            {
                gameEvents.SpawnPlayersEvent.RemoveListener(OnSpawnPlayers);
                gameEvents.RestartRaceEvent.RemoveListener(OnRestartRace);
                gameEvents.OnClickPlayRaceEvent.RemoveListener(OnSpawnPlayers);
            }

            if (authority == this)
                authority = null;
        }

        public void CopyFromLegacy(PlayersSpawner legacy)
        {
            gameEvents = legacy.gameEvents;
            spawnPoints = new List<Transform>(legacy.spawnPoints);
            playerSpawnIndex = legacy.playerSpawnIndex;
            customPlayerSpawnIndex = legacy.customPlayerSpawnIndex;

            if (legacy.playerPrefab != null)
                checkpointTrackerPrefab =
                    legacy.playerPrefab.GetComponentInChildren<CheckpointTracker>(true);
        }

        private static void UnsubscribeLegacy(PlayersSpawner legacy)
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            MethodInfo spawnMethod = typeof(PlayersSpawner).GetMethod("OnSpawnPlayers", flags);
            MethodInfo restartMethod = typeof(PlayersSpawner).GetMethod("OnRestartRace", flags);

            if (spawnMethod != null)
            {
                UnityAction spawnAction =
                    (UnityAction)System.Delegate.CreateDelegate(
                        typeof(UnityAction),
                        legacy,
                        spawnMethod);
                legacy.gameEvents.SpawnPlayersEvent.RemoveListener(spawnAction);
            }

            if (restartMethod != null)
            {
                UnityAction restartAction =
                    (UnityAction)System.Delegate.CreateDelegate(
                        typeof(UnityAction),
                        legacy,
                        restartMethod);
                legacy.gameEvents.RestartRaceEvent.RemoveListener(restartAction);
            }
        }

        private void Subscribe()
        {
            if (subscribed || gameEvents == null)
                return;

            if (spawnPoints.Count == 0)
                Debug.LogError("RCCP migration: no race spawn points assigned.");

            gameEvents.SpawnPlayersEvent.AddListener(OnSpawnPlayers);
            gameEvents.RestartRaceEvent.AddListener(OnRestartRace);

            // The kit's GameEvents is a ScriptableObject whose runtime listeners survive play
            // sessions in the editor, so a dead listener earlier in SpawnPlayersEvent can abort the
            // chain before this spawner runs. OnClickPlayRaceEvent fires first (MenuGUI) and the
            // spawn is idempotent, so the grid still gets built.
            gameEvents.OnClickPlayRaceEvent.AddListener(OnSpawnPlayers);
            subscribed = true;

            foreach (Transform spawnPoint in spawnPoints)
            {
                if (spawnPoint.TryGetComponent(out Renderer renderer))
                    renderer.enabled = false;
            }
        }

        private void OnSpawnPlayers()
        {
            // ScriptableObject event assets can retain runtime listeners between editor play
            // sessions. Only the canonical scene spawner may ever own a grid, so stale callbacks
            // cannot create duplicate cars that the ranking list does not know about.
            if (authority != this)
                return;

            if (spawnPoints.Count == 0)
            {
                Debug.LogError("RCCP migration: no race spawn points assigned, nothing spawned.", this);
                return;
            }

            // both OnClickPlayRaceEvent and SpawnPlayersEvent land here for one race start
            if (spawnedVehicles.Count > 0)
                return;

            RCCP_CarController source = ResolveVehiclePrefab();

            if (source == null)
            {
                Debug.LogError("RCCP migration: no RCCP vehicle prefab is available.");
                return;
            }

            spawnedVehicles.Clear();
            checkpointTrackers.Clear();
            RemoveOrphanedRccpGrid();

            int aiCount = Mathf.Min(GameBalance.Current.race.aiCount, spawnPoints.Count - 1);
            int playerIndex = ResolvePlayerSpawnIndex(aiCount);
            GmtkRccpWaypointPath waypointPath = GmtkRccpWaypointPath.GetOrCreate();

            // RCCP hands the chase camera to the vehicle that registered last, which would be an AI
            // car since the player spawns first.
            RCCP_SceneManager sceneManager = RCCP_SceneManager.Instance;

            if (sceneManager != null)
                sceneManager.registerLastVehicleAsPlayer = false;

            GmtkRccpVehicle playerAdapter = null;

            for (int raceIndex = 0; raceIndex <= aiCount; raceIndex++)
            {
                bool isPlayer = raceIndex == 0;
                int spawnIndex = isPlayer
                    ? playerIndex
                    : Mathf.Clamp(
                        raceIndex <= playerIndex ? raceIndex - 1 : raceIndex,
                        0,
                        spawnPoints.Count - 1);
                Transform spawnPoint = spawnPoints[spawnIndex];
                Vector3 spawnPosition = spawnPoint.position;

                GameObject vehicle = Instantiate(
                    source.gameObject,
                    spawnPosition,
                    spawnPoint.rotation);
                vehicle.name = isPlayer
                    ? "GMTK_RCCP_Player"
                    : $"GMTK_RCCP_AI_{raceIndex}";
                vehicle.SetActive(false);

                GmtkRccpVehicle adapter = vehicle.GetComponent<GmtkRccpVehicle>();

                if (adapter == null)
                    adapter = vehicle.AddComponent<GmtkRccpVehicle>();

                CheckpointTracker tracker = AttachCheckpointTracker(vehicle, raceIndex);
                vehicle.SetActive(true);
                adapter.Initialize(isPlayer, waypointPath, raceIndex);

                if (isPlayer)
                    playerAdapter = adapter;

                spawnedVehicles.Add(
                    new SpawnedVehicle(vehicle, spawnPosition, spawnPoint.rotation));

                if (tracker != null)
                    checkpointTrackers.Add(tracker);

                if (isPlayer)
                    AssignKitCameras(vehicle);
            }

            // re-claim the player slot now that every AI car has registered itself with RCCP
            if (playerAdapter != null)
                playerAdapter.BindAsPlayer();

            gameEvents.PlayersCheckpointTrackersAssignedEvent.Invoke(checkpointTrackers);

            Debug.Log($"RCCP grid: vehicle={source.name}, spawned={spawnedVehicles.Count} " +
                      $"(player + {aiCount} AI), trackers={checkpointTrackers.Count}, " +
                      $"playerSpawn={spawnPoints[playerIndex].name} at {spawnPoints[playerIndex].position:F1}");
        }

        private static void RemoveOrphanedRccpGrid()
        {
            GmtkRccpVehicle[] existing =
                Object.FindObjectsByType<GmtkRccpVehicle>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            foreach (GmtkRccpVehicle vehicle in existing)
            {
                if (!vehicle.gameObject.name.StartsWith("GMTK_RCCP_"))
                    continue;

                vehicle.gameObject.SetActive(false);
                Object.Destroy(vehicle.gameObject);
            }
        }

        private RCCP_CarController ResolveVehiclePrefab()
        {
            if (vehiclePrefab != null)
                return vehiclePrefab;

            RCCP_DemoVehicles demoVehicles = RCCP_DemoVehicles.Instance;

            if (demoVehicles == null ||
                demoVehicles.vehicles == null ||
                demoVehicles.vehicles.Length == 0)
            {
                return null;
            }

            return demoVehicles.vehicles[0];
        }

        private CheckpointTracker AttachCheckpointTracker(GameObject vehicle, int raceIndex)
        {
            CheckpointTracker tracker =
                vehicle.GetComponentInChildren<CheckpointTracker>(true);

            if (tracker == null && checkpointTrackerPrefab != null)
            {
                tracker = Instantiate(
                    checkpointTrackerPrefab,
                    vehicle.transform,
                    false);
            }

            if (tracker == null)
            {
                Debug.LogError($"RCCP migration: checkpoint tracker missing for race index {raceIndex}.");
                return null;
            }

            tracker.SetCarRacePositionIndex(raceIndex);
            return tracker;
        }

        private int ResolvePlayerSpawnIndex(int aiCount)
        {
            return playerSpawnIndex switch
            {
                PlayerSpawnIndex.Last => Mathf.Clamp(aiCount, 0, spawnPoints.Count - 1),
                PlayerSpawnIndex.Custom =>
                    Mathf.Clamp(customPlayerSpawnIndex, 0, spawnPoints.Count - 1),
                _ => 0,
            };
        }

        private static void AssignKitCameras(GameObject player)
        {
            PlayerCarCameraController[] cameras =
                Object.FindObjectsByType<PlayerCarCameraController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            foreach (PlayerCarCameraController cameraController in cameras)
                cameraController.playerCar = player;
        }

        private void OnRestartRace()
        {
            foreach (SpawnedVehicle vehicle in spawnedVehicles)
            {
                if (vehicle.GameObject == null)
                    continue;

                vehicle.GameObject.transform.SetPositionAndRotation(
                    vehicle.Position,
                    vehicle.Rotation);

                Rigidbody[] rigidbodies =
                    vehicle.GameObject.GetComponentsInChildren<Rigidbody>(true);

                foreach (Rigidbody body in rigidbodies)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                    body.position = vehicle.Position;
                    body.rotation = vehicle.Rotation;
                }
            }
        }
    }
}
