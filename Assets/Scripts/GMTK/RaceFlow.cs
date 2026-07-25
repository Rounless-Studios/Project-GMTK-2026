using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Gmtk2026.Quiz;

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

        [Header("Timing")]
        [SerializeField, Min(0.5f)] private float prologueSeconds = 4f;
        [SerializeField, Min(1f)] private float countdownSeconds = 5f;

        private Canvas canvas;
        private GameObject startPanel;
        private GameObject prologuePanel;
        private GameObject countdownPanel;
        private TMP_Text statusText;
        private TMP_Text countdownText;
        private Button startButton;
        private QuizSessionController quiz;
        private StartMenuCanvas sceneMenu;
        private bool started;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            if (SceneManager.GetActiveScene().name != "bora") return;
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
            else Debug.LogError("RaceFlow requires a scene-authored StartMenuCanvas with assigned UI references.", this);
        }

        private void Start()
        {
            Time.timeScale = 0f;
            SetPhase(Phase.StartScreen);
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(BeginPrologue);
                startButton.onClick.AddListener(BeginPrologue);
            }
            StartCoroutine(PrepareQuizBeforeStart());
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
            if (Instance == this)
            {
                Time.timeScale = 1f;
                Instance = null;
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
            StartCoroutine(RunPreRace());
        }

        private IEnumerator RunPreRace()
        {
            SetPhase(Phase.Prologue);
            statusText.text = "THE GATES OF HELL ARE OPEN...\n\nTHE LAST CAR IS EXECUTED EVERY 30 SECONDS.\nSOLVE QUIZZES WHILE DRIVING AND CAST CURSES.";
            yield return new WaitForSecondsRealtime(prologueSeconds);

            SetPhase(Phase.Countdown);
            countdownPanel.SetActive(true);
            float end = Time.unscaledTime + countdownSeconds;
            while (Time.unscaledTime < end)
            {
                float remaining = Mathf.Ceil(end - Time.unscaledTime);
                countdownText.text = remaining > 0f ? remaining.ToString("0") : "GO!";
                yield return null;
            }

            countdownText.text = "GO!";
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
                quiz.ConfigureSchedule(12f, 12f, 20f, 1.8f);
            }
        }

        private void SetPhase(Phase phase)
        {
            CurrentPhase = phase;
            if (sceneMenu != null)
            {
                sceneMenu.SetVisible(phase.ToString());
            }
            else
            {
                startPanel.SetActive(phase == Phase.StartScreen);
                prologuePanel.SetActive(phase == Phase.Prologue);
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
