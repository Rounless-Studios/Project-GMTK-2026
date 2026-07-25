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

        private void OnEnable() => Cache();

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
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
                target.SetActive(active);
        }
    }
}
