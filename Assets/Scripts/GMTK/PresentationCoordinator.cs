using Gmtk2026.GameBalance;
using Gmtk2026.Quiz;
using UnityEngine;

namespace GMTK
{
    public enum PresentationPriority
    {
        NormalHud,
        Overtake,
        ExecutionCctv,
        PlayerExecution,
        Quiz,
        Result
    }

    /// <summary>
    /// Single source of truth for mutually exclusive screen presentations. Systems may keep their
    /// gameplay state running while yielding their visuals to a higher-priority presentation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PresentationCoordinator : MonoBehaviour
    {
        public static PresentationCoordinator Instance { get; private set; }
        public PresentationPriority Current { get; private set; } =
            PresentationPriority.NormalHud;
        public static event System.Action<PresentationPriority> Changed;

        private QuizSessionController quiz;
        private bool playerExecution;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            GameObject host = GMTKGameMode.GetOrCreate();
            if (host.GetComponent<PresentationCoordinator>() == null)
                host.AddComponent<PresentationCoordinator>();
        }

        private void Awake() => Instance = this;

        private void Start()
        {
            quiz = FindFirstObjectByType<QuizSessionController>(FindObjectsInactive.Include);
            if (quiz != null)
            {
                quiz.QuestionStarted += OnQuizStarted;
                quiz.QuizClosed += Reevaluate;
            }

            GMTKRaceState.PhaseChanged += OnPhaseChanged;
            EliminationManager.EliminationTargetLocked += OnExecutionLocked;
            EliminationManager.CarEliminated += OnCarEliminated;
            Reevaluate();
        }

        private void OnDestroy()
        {
            if (quiz != null)
            {
                quiz.QuestionStarted -= OnQuizStarted;
                quiz.QuizClosed -= Reevaluate;
            }
            GMTKRaceState.PhaseChanged -= OnPhaseChanged;
            EliminationManager.EliminationTargetLocked -= OnExecutionLocked;
            EliminationManager.CarEliminated -= OnCarEliminated;
            if (Instance == this) Instance = null;
        }

        public bool Allows(PresentationPriority layer) => layer >= Current;

        private void OnQuizStarted(QuizQuestion _) => Reevaluate();

        private void OnPhaseChanged(RacePhase _) => Reevaluate();

        private void OnExecutionLocked(int raceIndex)
        {
            playerExecution = raceIndex == 0;
            Reevaluate();
        }

        private void OnCarEliminated(int _)
        {
            playerExecution = false;
            Reevaluate();
        }

        private void Reevaluate()
        {
            PresentationPriority next;
            if (GMTKRaceState.Instance != null &&
                GMTKRaceState.Instance.CurrentPhase == RacePhase.Finished)
                next = PresentationPriority.Result;
            else if (quiz != null && quiz.State != QuizSessionState.Waiting)
                next = PresentationPriority.Quiz;
            else if (playerExecution)
                next = PresentationPriority.PlayerExecution;
            else if (FindFirstObjectByType<ExecutionCctvDirector>() != null &&
                     FindFirstObjectByType<EliminationManager>()?.Level !=
                     EliminationWarningLevel.None)
                next = PresentationPriority.ExecutionCctv;
            else if (OvertakeManager.Instance?.Challenge?.Status == OvertakeStatus.Active)
                next = PresentationPriority.Overtake;
            else
                next = PresentationPriority.NormalHud;

            if (Current == next) return;
            Current = next;
            Changed?.Invoke(next);
        }
    }

    /// <summary>Persistent, device-local readability and comfort controls.</summary>
    public static class AccessibilityPreferences
    {
        public const string HudScaleKey = "GMTK.Accessibility.HudScale";
        public const string QuizScaleKey = "GMTK.Accessibility.QuizScale";
        public const string ShakeScaleKey = "GMTK.Accessibility.ShakeScale";
        public const string ReducedMotionKey = "GMTK.Accessibility.ReducedMotion";
        public const string HighContrastKey = "GMTK.Accessibility.HighContrast";

        public static float HudScale =>
            Mathf.Clamp(PlayerPrefs.GetFloat(HudScaleKey, 1f), 0.75f, 1.35f);
        public static float QuizScale =>
            Mathf.Clamp(PlayerPrefs.GetFloat(QuizScaleKey, 1f), 0.75f, 1.2f);
        public static float ShakeScale =>
            Mathf.Clamp01(PlayerPrefs.GetFloat(ShakeScaleKey, 1f));
        public static bool ReducedMotion => PlayerPrefs.GetInt(ReducedMotionKey, 0) != 0;
        public static bool HighContrast => PlayerPrefs.GetInt(HighContrastKey, 0) != 0;

        public static void SetFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
        }

        public static void SetToggle(string key, bool value)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
