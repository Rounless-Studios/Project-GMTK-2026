using TMPro;
using UnityEngine;
using GMTK;
/// <summary>
/// display player current position (calculated by RealTimeRacePositions script)
/// </summary>
namespace SpinMotion
{
    public class RacePositionGUI : MonoBehaviour
    {
        public RaceManagerItem raceManager;
        public RealTimeRacePositionsItem realTimeRacePositions;
        public TMP_Text racePositionTMP;
        public string racePositionNotAvailableText = "--";

        private TMP_Text racePositionSuffixTMP;

        private void Awake()
        {
            ResolvePositionSuffixText();
        }

        private void Update()
        {
            ResolvePositionSuffixText();

            if (raceManager.Item.IsRaceInProgress())
            {
                EliminationManager elimination = EliminationManager.Instance;
                int playerRacePosition = GetActivePlayerRacePosition(elimination);
                int activeRacerCount = elimination != null
                    ? elimination.ActiveCarCount
                    : realTimeRacePositions.Item.CarCheckpointTrackers.Count;
                racePositionTMP.text = playerRacePosition.ToString();

                if (racePositionSuffixTMP != null)
                    racePositionSuffixTMP.text = $"/{activeRacerCount}";
            }
            else
            {
                racePositionTMP.text = racePositionNotAvailableText;

                if (racePositionSuffixTMP != null)
                    racePositionSuffixTMP.text = string.Empty;
            }
        }

        private void ResolvePositionSuffixText()
        {
            if (racePositionSuffixTMP != null || racePositionTMP == null)
                return;

            Transform positionPanel = racePositionTMP.transform.parent;
            if (positionPanel != null)
                racePositionSuffixTMP = positionPanel.Find("nd TMP")?.GetComponent<TMP_Text>();
        }

        private int GetActivePlayerRacePosition(EliminationManager elimination)
        {
            RealTimeRacePositions positions = realTimeRacePositions.Item;
            if (positions == null || positions.RacePositionTotalScores.Count == 0)
                return 0;

            double playerScore = positions.RacePositionTotalScores[0];
            int playerPosition = 1;

            for (int i = 1; i < positions.RacePositionTotalScores.Count; i++)
            {
                if (elimination != null && elimination.IsEliminated(i))
                    continue;

                if (positions.RacePositionTotalScores[i] > playerScore)
                    playerPosition++;
            }

            return playerPosition;
        }

        public static string CardinalPos(int i)
        {
            if (i % 100 >= 11 && i % 100 <= 13)
            {
                return "th";
            }

            switch (i % 10)
            {
                case 1: return "st"; // 1st, 21st, 31st...
                case 2: return "nd"; // 2nd, 22nd, 32nd...
                case 3: return "rd"; // 3rd, 23rd, 33rd...
                default: return "th"; // 4th, 5th, ..., 11th, 12th...
            }
        }
    }
}
