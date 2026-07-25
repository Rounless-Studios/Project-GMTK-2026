using System;
using UnityEngine;
using UnityEngine.UI;
using Gmtk2026.GameBalance;

namespace GMTK
{
    /// <summary>Runtime race HUD for the core GMTK rules. Builds a small overlay automatically.</summary>
    [ExecuteAlways]
    public sealed class RaceHud : MonoBehaviour
    {
        [SerializeField] private GameObject hudCanvas;
        [Tooltip("Drag the existing DurabilityHud panel here to keep its original slider design inside RaceHud.")]
        [SerializeField] private RectTransform durabilityPanel;
        [SerializeField] private Text positionText;
        [SerializeField] private Text executionText;
        [SerializeField] private Text lastPlaceText;
        [SerializeField] private Text boostText;
        [SerializeField] private Text curseText;
        [SerializeField] private Text overtakeText;
        [SerializeField] private Text finalGateText;
        private EliminationManager elimination;

        private void Reset()
        {
            BuildUi();
        }

        private void Awake()
        {
            if (positionText == null) BuildUi();
            if (durabilityPanel != null && hudCanvas != null)
                durabilityPanel.SetParent(hudCanvas.transform, false);
            elimination = FindFirstObjectByType<EliminationManager>(FindObjectsInactive.Include);
        }

        [ContextMenu("Build HUD UI")]
        public void BuildUi()
        {
            if (positionText != null) return;
            positionText = CreateLabel("Position", 0);
            executionText = CreateLabel("ExecutionCountdown", 1);
            lastPlaceText = CreateLabel("LastPlaceWarning", 2);
            boostText = CreateLabel("Boost", 3);
            curseText = CreateLabel("Curse", 4);
            overtakeText = CreateLabel("OvertakeChallenge", 5);
            finalGateText = CreateLabel("FinalGate", 6);
        }

        private Text CreateLabel(string labelName, int row)
        {
            var root = new GameObject(labelName, typeof(RectTransform), typeof(Text));
            root.transform.SetParent(hudCanvas.transform, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(28, -28 - row * 30); rect.sizeDelta = new Vector2(760, 28);
            var label = root.GetComponent<Text>(); label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 22; label.color = Color.white; label.alignment = TextAnchor.UpperLeft;
            label.horizontalOverflow = HorizontalWrapMode.Overflow; label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        private void Update()
        {
            if (positionText == null) return;
            if (!Application.isPlaying) return;
            bool raceStarted = RaceFlow.Instance == null
                ? Race.IsRaceInProgress
                : RaceFlow.Instance.CurrentPhase == RaceFlow.Phase.Racing;
            // RaceHud lives on the canvas root, so disabling the GameObject would also
            // disable this Update loop permanently. Toggle the Canvas component instead.
            if (hudCanvas != null && hudCanvas.activeSelf != raceStarted)
                hudCanvas.SetActive(raceStarted);
            if (!raceStarted) return;

            var player = Race.CarByIndex(0);
            var boost = player != null ? player.GetComponent<BoostController>() : null;
            var curse = player != null ? player.GetComponent<CurseController>() : null;
            var overtake = OvertakeManager.Instance;
            int rank = GetRank();
            int total = Race.CarCount;
            string last = elimination != null && elimination.CurrentLastPlaceIndex >= 0
                ? $"LAST PLACE: CAR {elimination.CurrentLastPlaceIndex + 1}" : "LAST PLACE: -";
            string curseLine = curse == null ? "CURSE: -" : $"CURSE: {(curse.State != null ? curse.State.NextOrdered().ToString() : "-")}  {(curse.CanCast ? "READY" : $"{curse.CooldownRemaining:0.0}s")}";
            string challenge = overtake != null && overtake.Challenge != null && overtake.Challenge.Status == OvertakeStatus.Active
                ? $"OVERTAKE: CAR {overtake.RivalIndex + 1} / {overtake.Challenge.TimeRemaining:0.0}s" : "OVERTAKE: STANDBY";
            string finalGate = FinalGate.Instance != null && FinalGate.Instance.IsOpen
                ? $"FINAL GATE: {FinalGate.Instance.TimeRemaining:0.0}s" : "FINAL GATE: -";
            positionText.text = $"POSITION  {rank}/{Mathf.Max(0, total)}";
            executionText.text = $"EXECUTION IN  {(elimination != null ? elimination.SecondsToElimination.ToString("0.0") : "-")}s";
            lastPlaceText.text = $"{last}  {(elimination != null ? elimination.Level.ToString() : "-")}";
            boostText.text = $"BOOST  {(boost != null ? $"{boost.Charges}/{GameBalance.Current.boost.maximumCharges}" : "-")}";
            curseText.text = curseLine;
            overtakeText.text = challenge;
            finalGateText.text = finalGate;
        }

        private int GetRank()
        {
            if (Race.CarCount <= 0) return 0;
            double score = Race.ScoreOf(0); int rank = 1;
            var elim = elimination;
            for (int i = 0; i < Race.CarCount; i++)
                if (i != 0 && (elim == null || !elim.IsEliminated(i)) && Race.ScoreOf(i) > score) rank++;
            return rank;
        }
    }
}
