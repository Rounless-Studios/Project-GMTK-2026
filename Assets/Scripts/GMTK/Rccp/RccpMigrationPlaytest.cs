using System.Collections;
using System.Text;
using UnityEngine;
using GMTK.Kit;

namespace GMTK.Rccp
{
    /// <summary>Runtime smoke test for the RCCP vehicle migration.</summary>
    public sealed class RccpMigrationPlaytest : MonoBehaviour
    {
        public const string Tag = "GMTK-RCCP:";
        public static string LastResult = "(not run yet)";

        [SerializeField] private int aiCount = 3;
        [SerializeField] private float timeout = 15f;

        private void Start()
        {
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            StringBuilder result = new();
            bool passed = true;

            void Check(string name, bool condition, string detail = "")
            {
                passed &= condition;
                result.Append(condition ? "[OK] " : "[FAIL] ").Append(name);

                if (!string.IsNullOrEmpty(detail))
                    result.Append(" (").Append(detail).Append(')');

                result.Append("; ");
            }

            GameEvents events = Race.Events;

            if (events == null)
            {
                LastResult = "FAIL | no GameEvents";
                Debug.LogError(Tag + " " + LastResult);
                yield break;
            }

            RaceData.AiBotsSelected = aiCount;
            RaceData.LapsSelected = 20;
            events.OnClickPlayRaceEvent.Invoke();

            float startedAt = Time.time;

            while (!Race.IsRaceInProgress && Time.time - startedAt < timeout)
                yield return null;

            Check("race started", Race.IsRaceInProgress);
            Check("car count", Race.CarCount == aiCount + 1, $"cars={Race.CarCount}");

            GameObject player = Race.CarByIndex(0);
            Check("player resolved", player != null);
            Check(
                "player uses RCCP",
                player != null && player.GetComponent<RCCP_CarController>() != null);
            Check(
                "player adapter registered",
                player != null &&
                player.TryGetComponent(out GmtkRccpVehicle playerAdapter) &&
                playerAdapter.IsPlayer);

            int rccpAiCount = 0;

            for (int i = 1; i < Race.CarCount; i++)
            {
                GameObject ai = Race.CarByIndex(i);

                if (ai != null &&
                    ai.GetComponent<RCCP_CarController>() != null &&
                    ai.GetComponent<GmtkRccpWaypointDriver>() != null)
                {
                    rccpAiCount++;
                }
            }

            Check("AI uses RCCP waypoint driver", rccpAiCount == aiCount, $"ai={rccpAiCount}");
            Check(
                "checkpoint trackers preserved",
                Race.Positions != null &&
                Race.Positions.CarCheckpointTrackers.Count == aiCount + 1);

            LastResult = (passed ? "PASS | " : "FAIL | ") + result;
            Debug.Log(Tag + " " + LastResult);
        }
    }
}
