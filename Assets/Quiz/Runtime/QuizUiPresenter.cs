using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gmtk2026.Quiz
{
    public sealed class QuizUiPresenter : MonoBehaviour
    {
        private const string CurseWarningTitle = "YOU'VE BEEN CURSED!";

        private static readonly Color PanelColor = new Color(0.035f, 0.055f, 0.10f, 0.96f);
        private static readonly Color NormalButtonColor = new Color(0.12f, 0.18f, 0.29f, 1f);
        private static readonly Color CorrectColor = new Color(0.16f, 0.65f, 0.35f, 1f);
        private static readonly Color WrongColor = new Color(0.85f, 0.25f, 0.25f, 1f);

        private readonly List<Button> answerButtons = new List<Button>();
        private QuizSessionController controller;
        private GameObject phoneRoot;
        private GameObject canvasRoot;
        private GameObject standardAnswersRoot;
        private TMP_Text kindText;
        private TMP_Text promptText;
        private TMP_Text timerText;
        private TMP_Text feedbackText;
        private Slider timerSlider;
        private TMP_FontAsset uiFont;
        private WorldSpaceQuizCanvasFollower canvasFollower;
        private NumberSequenceMiniGameView numberSequenceView;
        private RhythmMiniGameView rhythmView;
        private ButtonMashMiniGameView buttonMashView;

        public void Build(QuizSessionController sessionController)
        {
            if (controller != null || sessionController == null)
            {
                return;
            }

            controller = sessionController;
            uiFont = TMP_Settings.defaultFontAsset;
            BuildCanvas();
            Subscribe();
            canvasFollower.SetVisible(false);
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Update()
        {
            rhythmView?.Update();
            buttonMashView?.Update();
        }

        private void Subscribe()
        {
            controller.QuestionStarted += ShowQuestion;
            controller.TimerChanged += UpdateTimer;
            controller.AnswerEvaluated += ShowFeedback;
            controller.QuizClosed += HideQuiz;
        }

        private void Unsubscribe()
        {
            if (controller == null)
            {
                return;
            }

            controller.QuestionStarted -= ShowQuestion;
            controller.TimerChanged -= UpdateTimer;
            controller.AnswerEvaluated -= ShowFeedback;
            controller.QuizClosed -= HideQuiz;
        }

        private void ShowQuestion(QuizQuestion question)
        {
            canvasFollower.SetVisible(true);
            kindText.text = CurseWarningTitle;
            promptText.text = question.Prompt;
            feedbackText.text = string.Empty;
            numberSequenceView.Hide();
            rhythmView.Hide();
            buttonMashView.Hide();

            bool isStandardQuestion =
                question.Kind != QuizKind.NumberSequenceClick &&
                question.Kind != QuizKind.RhythmTap &&
                question.Kind != QuizKind.ButtonMash;
            standardAnswersRoot.SetActive(isStandardQuestion);

            if (question.Kind == QuizKind.NumberSequenceClick)
            {
                numberSequenceView.Show(result => controller.SubmitInteractiveResult(result));
                return;
            }

            if (question.Kind == QuizKind.RhythmTap)
            {
                rhythmView.Show(
                    question,
                    result => controller.SubmitInteractiveResult(result));
                return;
            }

            if (question.Kind == QuizKind.ButtonMash)
            {
                buttonMashView.Show(
                    question,
                    result => controller.SubmitInteractiveResult(result));
                return;
            }

            for (int i = 0; i < answerButtons.Count; i++)
            {
                bool visible = i < question.Choices.Count;
                Button button = answerButtons[i];
                button.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                button.transition = Selectable.Transition.ColorTint;
                button.interactable = true;
                button.image.color = NormalButtonColor;
                button.GetComponentInChildren<TMP_Text>().text = question.Choices[i];
            }
        }

        private void UpdateTimer(float remaining, float total)
        {
            timerText.text = $"{remaining:0.0}s";
            timerSlider.value = total > 0f ? remaining / total : 0f;
            timerSlider.fillRect.GetComponent<Image>().color =
                remaining <= 2.5f ? WrongColor : new Color(0.25f, 0.78f, 1f, 1f);
        }

        private void ShowFeedback(QuizAnswerResult result)
        {
            numberSequenceView.Hide();
            rhythmView.Hide();
            buttonMashView.Hide();
            canvasFollower.SetVisible(false);

            foreach (Button button in answerButtons)
            {
                button.interactable = false;
                button.transition = Selectable.Transition.None;
            }

            bool isStandardQuestion =
                result.Question.Kind != QuizKind.NumberSequenceClick &&
                result.Question.Kind != QuizKind.RhythmTap &&
                result.Question.Kind != QuizKind.ButtonMash;

            if (isStandardQuestion &&
                result.Question.CorrectChoiceIndex < answerButtons.Count)
            {
                answerButtons[result.Question.CorrectChoiceIndex].image.color = CorrectColor;
            }

            if (isStandardQuestion &&
                !result.IsCorrect &&
                !result.TimedOut &&
                result.SelectedChoiceIndex >= 0 &&
                result.SelectedChoiceIndex < answerButtons.Count)
            {
                answerButtons[result.SelectedChoiceIndex].image.color = WrongColor;
            }

            if (result.TimedOut)
            {
                feedbackText.text = $"TIME'S UP!\n{result.Question.Explanation}";
                feedbackText.color = WrongColor;
            }
            else if (result.IsCorrect)
            {
                feedbackText.text = $"CORRECT!\n{result.Question.Explanation}";
                feedbackText.color = CorrectColor;
            }
            else
            {
                feedbackText.text = $"WRONG!\n{result.Question.Explanation}";
                feedbackText.color = WrongColor;
            }
        }

        private void HideQuiz()
        {
            numberSequenceView.Hide();
            rhythmView.Hide();
            buttonMashView.Hide();
            canvasFollower.SetVisible(false);
        }

        private void BuildCanvas()
        {
            phoneRoot = new GameObject(
                "QuizPhone",
                typeof(WorldSpaceQuizCanvasFollower),
                typeof(QuizPhoneVisual));
            phoneRoot.transform.SetParent(transform, false);
            phoneRoot.GetComponent<QuizPhoneVisual>().Build();

            canvasRoot = new GameObject(
                "QuizPhoneScreen",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasRoot.transform.SetParent(phoneRoot.transform, false);

            Canvas canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            canvas.sortingOrder = 500;

            CanvasScaler scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.dynamicPixelsPerUnit = 4f;

            RectTransform canvasRect = canvasRoot.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(860f, 1480f);
            canvasRect.localScale = new Vector3(0.00115f, 0.00135f, 0.00135f);
            canvasRect.localPosition = new Vector3(0f, 0f, -0.102f);
            canvasFollower = phoneRoot.GetComponent<WorldSpaceQuizCanvasFollower>();
            canvasFollower.Configure(canvas);

            GameObject overlay = CreateImage("PhoneScreenBackground", canvasRoot.transform, new Color(0.018f, 0.025f, 0.045f, 1f));
            Stretch(overlay.GetComponent<RectTransform>());

            GameObject panel = CreateImage("QuizPanel", overlay.transform, PanelColor);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(780f, 1320f);

            kindText = CreateText("Kind", panel.transform, 42, TextAlignmentOptions.MidlineLeft);
            SetRect(kindText.rectTransform, new Vector2(38f, -55f), new Vector2(500f, 54f), new Vector2(0f, 1f));
            kindText.color = new Color(1f, 0.12f, 0.12f, 1f);

            timerText = CreateText("TimerText", panel.transform, 42, TextAlignmentOptions.MidlineRight);
            SetRect(timerText.rectTransform, new Vector2(-38f, -55f), new Vector2(180f, 54f), new Vector2(1f, 1f));

            timerSlider = CreateTimerSlider(panel.transform);
            SetRect(timerSlider.GetComponent<RectTransform>(), new Vector2(0f, -135f), new Vector2(700f, 48f), new Vector2(0.5f, 1f));

            promptText = CreateText("Prompt", panel.transform, 64, TextAlignmentOptions.Center);
            SetRect(promptText.rectTransform, new Vector2(0f, -220f), new Vector2(700f, 260f), new Vector2(0.5f, 1f));
            promptText.fontStyle = FontStyles.Bold;

            Vector2[] buttonPositions =
            {
                new Vector2(-185f, -585f),
                new Vector2(185f, -585f),
                new Vector2(-185f, -755f),
                new Vector2(185f, -755f)
            };

            standardAnswersRoot = new GameObject("StandardAnswers", typeof(RectTransform));
            standardAnswersRoot.transform.SetParent(panel.transform, false);
            Stretch(standardAnswersRoot.GetComponent<RectTransform>());

            for (int i = 0; i < buttonPositions.Length; i++)
            {
                int capturedIndex = i;
                Button button = CreateAnswerButton(standardAnswersRoot.transform, $"Answer {i + 1}");
                SetRect(
                    button.GetComponent<RectTransform>(),
                    buttonPositions[i],
                    new Vector2(335f, 132f),
                    new Vector2(0.5f, 1f));
                button.onClick.AddListener(() => controller.SubmitAnswer(capturedIndex));
                answerButtons.Add(button);
            }

            feedbackText = CreateText("Feedback", panel.transform, 42, TextAlignmentOptions.Center);
            SetRect(feedbackText.rectTransform, new Vector2(0f, 120f), new Vector2(700f, 190f), new Vector2(0.5f, 0f));

            numberSequenceView = new NumberSequenceMiniGameView(panel.transform, uiFont);
            rhythmView = new RhythmMiniGameView(panel.transform, uiFont);
            buttonMashView = new ButtonMashMiniGameView(panel.transform, uiFont);
        }

        private Slider CreateTimerSlider(Transform parent)
        {
            GameObject sliderObject = new GameObject("Timer", typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(parent, false);
            Slider slider = sliderObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;

            GameObject background = CreateImage("Background", sliderObject.transform, new Color(0.12f, 0.14f, 0.2f, 1f));
            Stretch(background.GetComponent<RectTransform>());

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderObject.transform, false);
            Stretch(fillArea.GetComponent<RectTransform>());

            GameObject fill = CreateImage("Fill", fillArea.transform, new Color(0.25f, 0.78f, 1f, 1f));
            Stretch(fill.GetComponent<RectTransform>());
            slider.fillRect = fill.GetComponent<RectTransform>();

            return slider;
        }

        private Button CreateAnswerButton(Transform parent, string label)
        {
            GameObject buttonObject = CreateImage("AnswerButton", parent, NormalButtonColor);
            Button button = buttonObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.2f, 0.36f, 0.55f, 1f);
            colors.pressedColor = new Color(0.1f, 0.5f, 0.75f, 1f);
            colors.disabledColor = Color.white;
            button.colors = colors;

            TMP_Text text = CreateText("Label", buttonObject.transform, 44, TextAlignmentOptions.Center);
            text.text = label;
            text.fontStyle = FontStyles.Bold;
            Stretch(text.rectTransform, new Vector2(14f, 8f));
            return button;
        }

        private TMP_Text CreateText(
            string name,
            Transform parent,
            int fontSize,
            TextAlignmentOptions alignment)
        {
            GameObject textObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            TMP_Text text = textObject.GetComponent<TMP_Text>();
            text.font = uiFont;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Truncate;
            text.enableAutoSizing = true;
            text.fontSizeMin = 28;
            text.fontSizeMax = fontSize;
            text.outlineColor = new Color32(0, 0, 0, 230);
            text.outlineWidth = 0.12f;
            return text;
        }

        private static GameObject CreateImage(string name, Transform parent, Color color)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            imageObject.GetComponent<Image>().color = color;
            return imageObject;
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 anchoredPosition,
            Vector2 size,
            Vector2 anchor)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, Vector2 inset = default)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = inset;
            rect.offsetMax = -inset;
        }

    }
}
