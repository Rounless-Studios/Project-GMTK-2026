using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GMTK
{
    /// <summary>Editor-visible start/prologue/countdown UI hosted by the scene Canvas.</summary>
    [ExecuteAlways]
    public sealed class StartMenuCanvas : MonoBehaviour
    {
        public GameObject StartPanel { get; private set; }
        public GameObject ProloguePanel { get; private set; }
        public GameObject CountdownPanel { get; private set; }
        public GameObject MenuPanel { get; private set; }
        public GameObject RacePanel { get; private set; }
        public Button StartButton { get; private set; }
        public TMP_Text StatusText { get; private set; }
        public TMP_Text CountdownText { get; private set; }

        [SerializeField] private GameObject startPanel;
        [SerializeField] private GameObject prologuePanel;
        [SerializeField] private GameObject countdownPanel;
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private GameObject racePanel;
        [SerializeField] private Button startButton;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text countdownText;

        private const float GaugeMaximumSpeedKph = 300f;
        private const float GaugeMinimumNeedleAngle = 150f;
        private const float GaugeMaximumNeedleAngle = -150f;
        private const float GaugeNeedleSmoothTime = 0.08f;

        private TMP_Text speedText;
        private RectTransform rpmNeedle;
        private Rigidbody playerRigidbody;
        private float displayedNeedleAngle = GaugeMinimumNeedleAngle;
        private float needleAngularVelocity;

        private void OnEnable() => Cache();

        private void Update()
        {
            if (!Application.isPlaying)
                return;

            UpdateGauge();
        }

        public void EnsureUi()
        {
            Cache();
        }

        public void SetVisible(string phase)
        {
            EnsureUi();
            SetActive(StartPanel, phase == "StartScreen");
            SetActive(ProloguePanel, phase == "Prologue");
            SetActive(CountdownPanel, phase == "Countdown");
            SetActive(MenuPanel, phase == "StartScreen");
            SetActive(RacePanel, phase == "Racing" || phase == "Finished");
        }

        private void Cache()
        {
            StartPanel = startPanel != null ? startPanel : transform.Find("StartScreen")?.gameObject;
            ProloguePanel = prologuePanel != null ? prologuePanel : transform.Find("Prologue")?.gameObject;
            CountdownPanel = countdownPanel != null ? countdownPanel : transform.Find("Countdown")?.gameObject;
            MenuPanel = menuPanel != null ? menuPanel : transform.Find("MenuUI")?.gameObject;
            RacePanel = racePanel != null ? racePanel : transform.Find("RaceUI")?.gameObject;
            StartButton = startButton != null ? startButton : StartPanel?.GetComponentInChildren<Button>(true);
            StatusText = statusText != null ? statusText : ProloguePanel?.GetComponentInChildren<TMP_Text>(true);
            CountdownText = countdownText != null ? countdownText : CountdownPanel?.GetComponentInChildren<TMP_Text>(true);
            CacheGauge();
        }

        private void CacheGauge()
        {
            if (RacePanel == null)
                return;

            Transform gauge = RacePanel.transform.Find("Gauge");

            if (gauge == null)
                return;

            if (speedText == null)
                speedText = gauge.Find("Speed")?.GetComponent<TMP_Text>();

            if (rpmNeedle != null)
                return;

            foreach (Transform child in gauge)
            {
                // Gauge contains two children named RPM. The TMP child is the numeric
                // RPM label; the Image-only child is the rotating needle.
                if (child.name == "RPM" && child.GetComponent<TMP_Text>() == null)
                {
                    rpmNeedle = child as RectTransform;
                    break;
                }
            }
        }

        private void UpdateGauge()
        {
            if (speedText == null || rpmNeedle == null)
                CacheGauge();

            if (playerRigidbody == null)
            {
                GameObject player = Race.CarByIndex(0);

                if (player != null)
                    playerRigidbody = player.GetComponent<Rigidbody>();
            }

            float speedKph = playerRigidbody != null
                ? playerRigidbody.linearVelocity.magnitude * 3.6f
                : 0f;

            if (speedText != null)
                speedText.SetText("{0:0}", speedKph);

            if (rpmNeedle == null)
                return;

            float speedRatio = Mathf.Clamp01(speedKph / GaugeMaximumSpeedKph);
            float targetAngle = Mathf.Lerp(
                GaugeMinimumNeedleAngle,
                GaugeMaximumNeedleAngle,
                speedRatio);
            displayedNeedleAngle = Mathf.SmoothDamp(
                displayedNeedleAngle,
                targetAngle,
                ref needleAngularVelocity,
                GaugeNeedleSmoothTime);
            rpmNeedle.localRotation = Quaternion.Euler(0f, 0f, displayedNeedleAngle);
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
                target.SetActive(active);
        }
    }
}
