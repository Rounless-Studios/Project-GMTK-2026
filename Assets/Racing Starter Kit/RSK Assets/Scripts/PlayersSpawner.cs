using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// spawn player and AI bots, manages race start position, and restart race pos/rotations
/// </summary>
namespace SpinMotion
{
    public enum PlayerType
    {
        Player,
        AI
    }
    
    public enum PlayerSpawnIndex
    {
        First,
        Last,
        Custom
    }

    public class PlayersSpawner : MonoBehaviour
    {
        public GameEvents gameEvents;
        public GameObject playerPrefab;
        public GameObject aiCarPrefab;
        public GameObject aiWaypointTrackerPrefab;
        public List<Transform> spawnPoints = new();

        [Header("MVC Hybrid Player")]
        [Tooltip("Spawn the player as an MVC vehicle (AI stay kit cars).")]
        public bool useMvcPlayer = false;
        [Tooltip("MVC car prefab used for the player when useMvcPlayer is on.")]
        public GameObject mvcPlayerPrefab;
        [Tooltip("Kit CheckpointTracker prefab, attached to the MVC player so scoring tracks it.")]
        public GameObject checkpointTrackerPrefab;

        [Header("MVC AI")]
        [Tooltip("Spawn the AI as MVC vehicles that follow the aiPathObject.")]
        public bool useMvcAi = false;
        [Tooltip("MVC '- AI' car prefab used for AI when useMvcAi is on.")]
        public GameObject mvcAiPrefab;
        [Tooltip("Scene object holding the VehicleAIPath the AI follow (draw it in the editor).")]
        public GameObject aiPathObject;

        [Header("Player Spawn Settings")]
        public PlayerSpawnIndex playerSpawnIndex = PlayerSpawnIndex.First;
        public int customPlayerSpawnIndex = 0; // only used if playerSpawnIndex is Custom

        private List<(GameObject go, Vector3 spawnPos, Quaternion spawnRot)> spawnedPlayers = new();
        private List<CheckpointTracker> playersCheckpointTrackers = new();

        private void Awake()
        {
            if (spawnPoints.Count == 0)
            {
                Debug.LogError("No spawn points assigned");
            }

            gameEvents.SpawnPlayersEvent.AddListener(OnSpawnPlayers);
            gameEvents.RestartRaceEvent.AddListener(OnRestartRace);

            foreach (var spawnPoint in spawnPoints)
                if (spawnPoint.TryGetComponent<Renderer>(out var renderer)) { renderer.enabled = false; }
        }

        private void OnSpawnPlayers()
        {
            int totalSpawns = spawnPoints.Count;
            int aiCount = RaceData.AiBotsSelected;

            // determine player spawn index
            int playerSpawnIndex = 0;
            switch (this.playerSpawnIndex)
            {
                case PlayerSpawnIndex.Last:
                    playerSpawnIndex = Mathf.Clamp(aiCount, 0, totalSpawns - 1); // player spawns after all AI
                    break;
                case PlayerSpawnIndex.Custom:
                    playerSpawnIndex = Mathf.Clamp(customPlayerSpawnIndex, 0, totalSpawns - 1);
                    break;
                case PlayerSpawnIndex.First:
                default:
                    playerSpawnIndex = 0;
                    break;
            }

            for (int i = 0; i <= aiCount; i++)
            {
                if (i == 0)
                {
                    // spawn player at the determined index — MVC vehicle in hybrid mode, else kit car
                    var prefab = (useMvcPlayer && mvcPlayerPrefab != null) ? mvcPlayerPrefab : playerPrefab;
                    var player = Instantiate(prefab, spawnPoints[playerSpawnIndex].position, spawnPoints[playerSpawnIndex].rotation);
                    spawnedPlayers.Add((player, player.transform.position, player.transform.rotation));

                    // the kit player prefab nests a CheckpointTracker; the MVC prefab does not, so
                    // attach one from the tracker prefab. Either way scoring keys off race index 0.
                    var checkpointTracker = player.GetComponentInChildren<CheckpointTracker>();
                    if (checkpointTracker == null && checkpointTrackerPrefab != null)
                        checkpointTracker = Instantiate(checkpointTrackerPrefab, player.transform).GetComponent<CheckpointTracker>();
                    checkpointTracker.SetCarRacePositionIndex(0);
                    playersCheckpointTrackers.Add(checkpointTracker);

                    if (useMvcPlayer && mvcPlayerPrefab != null)
                        GMTK.Mvc.GmtkMvcBridge.SetPlayerVehicle(player);
                }
                else
                {
                    // spawn AI at their respective indexes
                    int aiSpawnIdx = (i <= playerSpawnIndex) ? i - 1 : i; // adjust AI index if it overlaps with player
                    aiSpawnIdx = Mathf.Clamp(aiSpawnIdx, 0, totalSpawns - 1);

                    if (useMvcAi && mvcAiPrefab != null)
                    {
                        // MVC AI car following the shared VehicleAIPath
                        var aiCar = Instantiate(mvcAiPrefab, spawnPoints[aiSpawnIdx].position, spawnPoints[aiSpawnIdx].rotation);
                        spawnedPlayers.Add((aiCar, aiCar.transform.position, aiCar.transform.rotation));

                        var mvcTracker = aiCar.GetComponentInChildren<CheckpointTracker>();
                        if (mvcTracker == null && checkpointTrackerPrefab != null)
                            mvcTracker = Instantiate(checkpointTrackerPrefab, aiCar.transform).GetComponent<CheckpointTracker>();
                        mvcTracker.SetCarRacePositionIndex(i);
                        playersCheckpointTrackers.Add(mvcTracker);

                        GMTK.Mvc.GmtkMvcBridge.SetupAiFollower(aiCar, aiPathObject);
                        continue;
                    }

                    var aiCar2 = Instantiate(aiCarPrefab, spawnPoints[aiSpawnIdx].position, spawnPoints[aiSpawnIdx].rotation);
                    spawnedPlayers.Add((aiCar2, aiCar2.transform.position, aiCar2.transform.rotation));

                    var aiTracker = Instantiate(aiWaypointTrackerPrefab).GetComponent<AIWaypointTracker>();
                    aiTracker.SetupAICarCollider(aiCar2.GetComponentInChildren<AICarWaypointTrackerColliderTrigger>().GetColliderTrigger());

                    aiCar2.GetComponent<CarAIControl>().SetTarget(aiTracker.transform); // replace with your car controller ai target to aim/follow

                    var checkpointTracker = aiCar2.GetComponentInChildren<CheckpointTracker>();
                    checkpointTracker.SetCarRacePositionIndex(i);
                    playersCheckpointTrackers.Add(checkpointTracker);
                }
            }

            // let the MVC manager register runtime-spawned MVC vehicles (player + AI)
            if (useMvcPlayer || useMvcAi)
                GMTK.Mvc.GmtkMvcBridge.RefreshVehicles();

            gameEvents.PlayersCheckpointTrackersAssignedEvent.Invoke(playersCheckpointTrackers);
        }

        private void OnRestartRace()
        {
            // reallocate players position and rotation to initial spawn points
            foreach (var player in spawnedPlayers)
            {
                player.go.transform.position = player.spawnPos;

                // check for main and child Rigidbodies
                Rigidbody[] rigidbodies = player.go.GetComponentsInChildren<Rigidbody>();
                foreach (var rb in rigidbodies)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.rotation = player.spawnRot;
                }

                if (rigidbodies.Length == 0)
                {
                    player.go.transform.rotation = player.spawnRot;
                }
            }
        }
    }
}