using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Gmtk2026.Quiz;
using GMTK.Kit;

namespace GMTK
{
    /// <summary>
    /// Playable bootstrap for the bora race: title screen, short prologue, five-second
    /// grid countdown, then the live race. It intentionally uses unscaled time before
    /// the start so the MVC vehicles remain parked on the grid.
    /// </summary>
    public sealed class RaceFlow : MonoBehaviour
    {
        public enum Phase { StartScreen, Prologue, Countdown, Racing, Finished }

        public static RaceFlow Instance { get; private set; }
        public Phase CurrentPhase { get; private set; } = Phase.StartScreen;
        public static event System.Action<Phase> PhaseChanged;
        public static bool OwnsGameEventsStart { get; private set; }

        [Header("Timing")]
        [SerializeField, Min(0.1f)] private float readyPromptSeconds = 1f;
        [SerializeField, Min(1f)] private float countdownSeconds = 3f;

        private Canvas canvas;
        private GameObject startPanel;
        private GameObject prologuePanel;
        private GameObject countdownPanel;
        private TMP_Text statusText;
        private TMP_Text countdownText;
        private Button startButton;
        private QuizSessionController quiz;
        private StartMenuCanvas sceneMenu;
        private GameEvents gameEvents;
        private bool started;
        private Coroutine gameEventsPreRaceRoutine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (sceneName != "bora" && sceneName != "GMTK_Race") return;
            if (FindFirstObjectByType<RaceFlow>(FindObjectsInactive.Include) != null) return;
            new GameObject("RaceFlow").AddComponent<RaceFlow>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            gameEvents = Race.Events;
            OwnsGameEventsStart = gameEvents != null && SceneManager.GetActiveScene().name == "GMTK_Race";
            sceneMenu = FindFirstObjectByType<StartMenuCanvas>(FindObjectsInactive.Include);
            if (sceneMenu != null)
            {
                sceneMenu.EnsureUi();
                startPanel = sceneMenu.StartPanel;
                prologuePanel = sceneMenu.ProloguePanel;
                countdownPanel = sceneMenu.CountdownPanel;
                startButton = sceneMenu.StartButton;
                statusText = sceneMenu.StatusText;
                countdownText = sceneMenu.CountdownText;
            }
            else if (!OwnsGameEventsStart)
                Debug.LogError("RaceFlow requires a scene-authored StartMenuCanvas with assigned UI references.", this);
        }

        private void Start()
        {
            if (OwnsGameEventsStart)
            {
                SubscribeToGameEvents();
                HideSceneUi();
                HideLegacyCountdownUi();
                StartCoroutine(PrepareQuizBeforeStart());
                return;
            }

            Time.timeScale = 0f;
            SetPhase(Phase.StartScreen);
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(BeginPrologue);
                startButton.onClick.AddListener(BeginPrologue);
            }
            StartCoroutine(PrepareQuizBeforeStart());
        }

        private void SubscribeToGameEvents()
        {
            if (gameEvents == null) return;
            // ScriptableObject UnityEvents survive Play sessions when domain reload is disabled.
            // Keep this target idempotent and include the restart path owned by this flow.
            UnsubscribeFromGameEvents();
            gameEvents.OnClickPlayRaceEvent.AddListener(OnGameEventsPlayRace);
            gameEvents.RaceStartedEvent.AddListener(OnGameEventsRaceStarted);
            gameEvents.RestartRaceEvent.AddListener(OnGameEventsRestartRace);
        }

        private void UnsubscribeFromGameEvents()
        {
            if (gameEvents == null) return;
            gameEvents.OnClickPlayRaceEvent.RemoveListener(OnGameEventsPlayRace);
            gameEvents.RaceStartedEvent.RemoveListener(OnGameEventsRaceStarted);
            gameEvents.RestartRaceEvent.RemoveListener(OnGameEventsRestartRace);
        }

        private void OnGameEventsPlayRace()
        {
            if (started) return;
            started = true;
            gameEventsPreRaceRoutine = StartCoroutine(RunGameEventsPreRace(true));
        }

        private void OnGameEventsRaceStarted()
        {
            if (!OwnsGameEventsStart || CurrentPhase == Phase.Racing) return;
            SetPhase(Phase.Racing);
        }

        private void OnGameEventsRestartRace()
        {
            if (!OwnsGameEventsStart) return;

            if (gameEventsPreRaceRoutine != null)
                StopCoroutine(gameEventsPreRaceRoutine);

            started = true;
            if (quiz != null)
            {
                quiz.ResetForRace();
                quiz.enabled = false;
            }

            // RestartRaceEvent has already restored the grid and gameplay systems. Re-enter the
            // authored countdown directly; the prologue only belongs to the first race.
            gameEventsPreRaceRoutine = StartCoroutine(RunGameEventsPreRace(false));
        }

        private IEnumerator RunGameEventsPreRace(bool playPrologue)
        {
            if (playPrologue)
            {
                SetPhase(Phase.Prologue);
                yield return PrologueCutscenePlayer.Play();
            }

            SetPhase(Phase.Countdown);
            gameEvents.ToggleCarFreezeEvent.Invoke(true);
            if (countdownText != null) countdownText.text = "Are You Ready?";
            yield return new WaitForSecondsRealtime(readyPromptSeconds);

            int seconds = Mathf.Max(1, Mathf.RoundToInt(countdownSeconds));
            GameAudioManager.Instance?.PlayCountdownTick();
            for (int remaining = seconds; remaining > 0; remaining--)
            {
                if (countdownText != null) countdownText.text = remaining.ToString();
                yield return new WaitForSecondsRealtime(1f);
            }

            if (countdownText != null) countdownText.text = "GO!";
            GameAudioManager.Instance?.PlayRaceStart();
            gameEvents.ToggleCarFreezeEvent.Invoke(false);
            gameEvents.ChangeToRaceCamerasEvent.Invoke();
            try
            {
                // The kit initializes RaceManager before its HUD and pause controls
                // are allowed to observe the Racing phase.
                gameEvents.RaceStartedEvent.Invoke();
            }
            finally
            {
                // Keep the UI transition deterministic even if another race-start
                // listener fails before RaceFlow receives the event.
                if (CurrentPhase != Phase.Racing)
                    SetPhase(Phase.Racing);
            }
            gameEvents.PreRaceUpdateGuiEvent.Invoke();
            if (quiz != null)
            {
                quiz.enabled = true;
                quiz.ConfigureFeedbackDuration(1.8f);
            }
            yield return new WaitForSecondsRealtime(0.45f);
            if (countdownPanel != null) countdownPanel.SetActive(false);
            gameEventsPreRaceRoutine = null;
        }

        private void HideSceneUi()
        {
            if (startPanel != null) startPanel.SetActive(false);
            if (prologuePanel != null) prologuePanel.SetActive(false);
            if (countdownPanel != null) countdownPanel.SetActive(false);
        }

        private static void HideLegacyCountdownUi()
        {
            foreach (var legacyCountdown in FindObjectsByType<PreRaceCountdownGUI>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (legacyCountdown.countdownTMP != null)
                    legacyCountdown.countdownTMP.gameObject.SetActive(false);
                legacyCountdown.gameObject.SetActive(false);
            }
        }

        private IEnumerator PrepareQuizBeforeStart()
        {
            // QuizGameBootstrap is also scene-load initialized; wait briefly so its
            // controller is found regardless of RuntimeInitialize callback ordering.
            float deadline = Time.realtimeSinceStartup + 1f;
            while (quiz == null && Time.realtimeSinceStartup < deadline)
            {
                quiz = FindFirstObjectByType<QuizSessionController>(FindObjectsInactive.Include);
                if (quiz != null) break;
                yield return null;
            }

            if (quiz != null) quiz.enabled = false;
        }

        private void OnDestroy()
        {
            UnsubscribeFromGameEvents();
            if (Instance == this)
            {
                Time.timeScale = 1f;
                Instance = null;
                OwnsGameEventsStart = false;
            }
        }

        private void BuildUi()
        {
            EnsureEventSystem();

            var root = new GameObject("RaceFlowCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            root.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            root.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);

            startPanel = MakePanel("StartPanel", new Color(0.015f, 0.02f, 0.04f, 0.94f));
            AddText(startPanel.transform, "HELL COUNTDOWN\nRACING", 64, new Vector2(0f, 180f), new Vector2(1200f, 180f), TextAnchor.MiddleCenter);
            AddText(startPanel.transform, "RACE THROUGH HELL. NEVER FINISH LAST.", 28, new Vector2(0f, 55f), new Vector2(1200f, 70f), TextAnchor.MiddleCenter);
            startButton = MakeButton(startPanel.transform, "START RACE", new Vector2(0f, -100f));
            startButton.onClick.AddListener(BeginPrologue);

            prologuePanel = MakePanel("ProloguePanel", new Color(0.015f, 0.02f, 0.04f, 0.96f));
            statusText = AddText(prologuePanel.transform, "", 38, new Vector2(0f, 0f), new Vector2(1400f, 500f), TextAnchor.MiddleCenter);

            countdownPanel = MakePanel("CountdownPanel", new Color(0f, 0f, 0f, 0.38f));
            countdownText = AddText(countdownPanel.transform, "", 150, new Vector2(0f, 0f), new Vector2(800f, 300f), TextAnchor.MiddleCenter);

            startPanel.SetActive(false);
            prologuePanel.SetActive(false);
            countdownPanel.SetActive(false);
        }

        private GameObject MakePanel(string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(canvas.transform, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = color;
            return go;
        }

        private TMP_Text AddText(Transform parent, string value, int size, Vector2 position, Vector2 dimensions, TextAnchor alignment)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            var text = go.GetComponent<TMP_Text>();
            text.text = value;
            // Unity 6 removed Arial.ttf from the built-in runtime fonts.
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            return text;
        }

        private Button MakeButton(Transform parent, string label, Vector2 position)
        {
            var go = new GameObject("StartButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(360f, 90f);
            go.GetComponent<Image>().color = new Color(0.75f, 0.12f, 0.08f, 1f);
            AddText(go.transform, label, 32, Vector2.zero, new Vector2(340f, 80f), TextAnchor.MiddleCenter);
            return go.GetComponent<Button>();
        }

        private void BeginPrologue()
        {
            if (started) return;
            started = true;
            GameAudioManager.Instance?.PlayButtonClick();
            StartCoroutine(RunPreRace());
        }

        private IEnumerator RunPreRace()
        {
            SetPhase(Phase.Prologue);
            yield return PrologueCutscenePlayer.Play();

            SetPhase(Phase.Countdown);
            countdownPanel.SetActive(true);
            countdownText.text = "Are You Ready?";
            yield return new WaitForSecondsRealtime(readyPromptSeconds);

            float end = Time.unscaledTime + countdownSeconds;
            GameAudioManager.Instance?.PlayCountdownTick();
            while (Time.unscaledTime < end)
            {
                float remaining = Mathf.Ceil(end - Time.unscaledTime);
                countdownText.text = remaining > 0f ? remaining.ToString("0") : "GO!";
                yield return null;
            }

            countdownText.text = "GO!";
            GameAudioManager.Instance?.PlayRaceStart();
            yield return new WaitForSecondsRealtime(0.45f);
            countdownPanel.SetActive(false);

            // The Racing Starter Kit spawner can be created after Map.OnEnable.
            // Re-apply the editor-authored points immediately before the race starts
            // so runtime-instantiated cars use the same locations.
            FindFirstObjectByType<TrackLayout>(FindObjectsInactive.Include)?.ApplyLayout();

            Time.timeScale = 1f;
            SetPhase(Phase.Racing);

            if (quiz != null)
            {
                quiz.enabled = true;
                quiz.ConfigureFeedbackDuration(1.8f);
            }
        }

        private void SetPhase(Phase phase)
        {
            CurrentPhase = phase;
            if (sceneMenu != null)
            {
                sceneMenu.SetVisible(phase.ToString());
            }
            else if (startPanel != null && prologuePanel != null && countdownPanel != null)
            {
                startPanel.SetActive(phase == Phase.StartScreen);
                prologuePanel.SetActive(false);
                countdownPanel.SetActive(phase == Phase.Countdown);
            }
            PhaseChanged?.Invoke(phase);
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) != null) return;
            new GameObject("RaceEventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        }
    }
}
