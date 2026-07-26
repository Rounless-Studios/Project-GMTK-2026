using System;
using UnityEngine;
using UnityEngine.UI;
using Gmtk2026.GameBalance;
using SpinMotion;

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
        [SerializeField] private Text objectiveText;
        [SerializeField] private Text resultText;
        private EliminationManager elimination;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            SuppressLegacyDuplicateHud();
            if (FindFirstObjectByType<RaceHud>(FindObjectsInactive.Include) != null) return;

            var root = new GameObject(
                "GMTK_RaceHud",
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(RaceHud));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            float hudScale = AccessibilityPreferences.HudScale;
            scaler.referenceResolution = new Vector2(1920f, 1080f) / hudScale;
            scaler.matchWidthOrHeight = 0.5f;

            RaceHud hud = root.GetComponent<RaceHud>();
            hud.hudCanvas = root;
            hud.BuildUi();
        }

        private static void SuppressLegacyDuplicateHud()
        {
            // RaceUI.prefab is the authored HUD. These starter-kit widgets display the same
            // position/lap/timer data in the same corners and were the large overlapping labels
            // visible behind it.
            foreach (LapsGUI gui in
                     FindObjectsByType<LapsGUI>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                gui.gameObject.SetActive(false);
            foreach (RaceTimerGUI gui in
                     FindObjectsByType<RaceTimerGUI>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                gui.gameObject.SetActive(false);
        }

        private void Reset()
        {
            BuildUi();
        }

        private void Awake()
        {
            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler != null)
                scaler.referenceResolution =
                    new Vector2(1920f, 1080f) / AccessibilityPreferences.HudScale;
            if (positionText == null) BuildUi();
            if (positionText != null) positionText.gameObject.SetActive(false);
            if (durabilityPanel != null && hudCanvas != null)
                durabilityPanel.SetParent(hudCanvas.transform, false);
            elimination = FindFirstObjectByType<EliminationManager>(FindObjectsInactive.Include);
        }

        [ContextMenu("Build HUD UI")]
        public void BuildUi()
        {
            if (positionText != null) return;
            if (hudCanvas == null)
            {
                Canvas canvas = GetComponent<Canvas>();
                if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                hudCanvas = gameObject;
            }
            positionText = CreateLabel("Position", 0);
            executionText = CreateLabel("ExecutionCountdown", 1);
            lastPlaceText = CreateLabel("LastPlaceWarning", 2);
            boostText = CreateLabel("Boost", 3);
            curseText = CreateLabel("Curse", 4);
            overtakeText = CreateLabel("OvertakeChallenge", 5);
            finalGateText = CreateLabel("FinalGate", 6);
            objectiveText = CreateLabel("Objective", 7);
            resultText = CreateCenteredLabel("Result");

            // The shipped UI already owns position, boost, curse and durability. RaceHud is only
            // the missing alert layer; duplicating those values caused the top-left overlap.
            // Keep the starter kit's larger ordinal display ("1st Place"). It is clearer than
            // this alert layer's compact fraction and matches the original HUD composition.
            positionText.gameObject.SetActive(false);
            lastPlaceText.gameObject.SetActive(false);
            boostText.gameObject.SetActive(false);
            curseText.gameObject.SetActive(false);
            finalGateText.gameObject.SetActive(false);
            ConfigureAlertLabel(objectiveText, -112f, 26, FontStyle.Bold);
            ConfigureAlertLabel(executionText, -148f, 24, FontStyle.Bold);
            ConfigureAlertLabel(overtakeText, -182f, 20, FontStyle.Normal);
            RectTransform positionRect = positionText.rectTransform;
            positionRect.anchorMin = positionRect.anchorMax = new Vector2(0f, 1f);
            positionRect.pivot = new Vector2(0f, 1f);
            positionRect.anchoredPosition = new Vector2(185f, -23f);
            positionRect.sizeDelta = new Vector2(140f, 40f);
            positionText.fontSize = 24;
            positionText.fontStyle = FontStyle.Bold;
        }

        private static void ConfigureAlertLabel(
            Text label,
            float y,
            int fontSize,
            FontStyle style)
        {
            RectTransform rect = label.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(920f, 34f);
            label.alignment = TextAnchor.MiddleCenter;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.raycastTarget = false;
        }

        private Text CreateCenteredLabel(string labelName)
        {
            var root = new GameObject(labelName, typeof(RectTransform), typeof(Text));
            root.transform.SetParent(hudCanvas.transform, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.2f, 0.4f);
            rect.anchorMax = new Vector2(0.8f, 0.7f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var label = root.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 64;
            label.fontStyle = FontStyle.Bold;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
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
            RacePhase phase = GMTKRaceState.Instance != null
                ? GMTKRaceState.Instance.CurrentPhase
                : (Race.IsRaceInProgress ? RacePhase.Racing : RacePhase.Boot);
            bool raceStarted = phase == RacePhase.Racing || phase == RacePhase.FinalDuel;
            Canvas canvas = hudCanvas != null ? hudCanvas.GetComponent<Canvas>() : null;
            if (canvas != null) canvas.enabled = raceStarted || phase == RacePhase.Finished;
            if (resultText != null)
            {
                resultText.gameObject.SetActive(phase == RacePhase.Finished);
                if (phase == RacePhase.Finished)
                {
                    bool won = GMTKRaceState.Instance != null &&
                               GMTKRaceState.Instance.LastResult ==
                               SpinMotion.RaceFinishType.Win;
                    string stats = RaceSessionStats.Instance != null
                        ? "\n\n" + RaceSessionStats.Instance.BuildResultSummary()
                        : string.Empty;
                    resultText.text = (won
                        ? "ESCAPED HELL"
                        : "CONDEMNED") + stats + "\n\nPRESS RESTART TO RACE AGAIN";
                }
            }
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
            positionText.text = $"{rank}/{Mathf.Max(0, total)}";
            bool warningActive = elimination != null &&
                                 elimination.Level != EliminationWarningLevel.None;
            executionText.gameObject.SetActive(warningActive);
            executionText.text = warningActive
                ? $"PURGE IN {elimination.SecondsToElimination:0.0}s — " +
                  (elimination.CurrentLastPlaceIndex == 0
                      ? "YOU ARE MARKED"
                      : $"{RaceSessionStats.NameOf(elimination.CurrentLastPlaceIndex)} IS MARKED")
                : string.Empty;
            lastPlaceText.text = $"{last}  {(elimination != null ? elimination.Level.ToString() : "-")}";
            bool playerLast = elimination != null && elimination.CurrentLastPlaceIndex == 0;
            lastPlaceText.color = playerLast ? new Color(1f, 0.1f, 0.05f) : new Color(1f, 0.75f, 0.15f);
            executionText.color = elimination != null && elimination.Level >= EliminationWarningLevel.Intense
                ? new Color(1f, 0.15f, 0.05f)
                : Color.white;
            boostText.text = $"BOOST  {(boost != null ? $"{boost.Charges}/{GameBalance.Current.boost.maximumCharges}" : "-")}";
            curseText.text = curseLine;
            bool finalDuel = phase == RacePhase.FinalDuel;
            bool activeChallenge = overtake != null && overtake.Challenge != null &&
                                   overtake.Challenge.Status == OvertakeStatus.Active;
            overtakeText.gameObject.SetActive(activeChallenge || finalDuel);
            overtakeText.text = finalDuel ? finalGate : challenge;
            finalGateText.text = finalGate;
            objectiveText.text = phase == RacePhase.FinalDuel
                ? "FINAL DUEL — REACH THE GATE FIRST"
                : playerLast
                    ? "ESCAPE LAST PLACE BEFORE THE TIMER HITS ZERO"
                    : "STAY AHEAD — THE LAST RACER DIES EVERY 30 SECONDS";
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
