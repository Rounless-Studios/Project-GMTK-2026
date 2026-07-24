using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gmtk2026.Quiz
{
    public sealed class ButtonMashMiniGameView
    {
        private const float FullGaugeDisplaySeconds = 0.08f;

        private readonly GameObject root;
        private readonly Image gaugeFill;
        private readonly Button mashButton;
        private readonly ButtonMashProgress progress = new ButtonMashProgress();
        private Action<bool> completed;
        private bool resultSubmitted;
        private bool successPending;
        private float successSubmitAt;

        public ButtonMashMiniGameView(Transform parent, TMP_FontAsset font)
        {
            root = new GameObject("ButtonMashMiniGame", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = rootRect.anchorMax = rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.anchoredPosition = new Vector2(0f, -430f);
            rootRect.sizeDelta = new Vector2(700f, 650f);

            GameObject gaugeBackground = CreateImage(
                "GaugeBackground",
                root.transform,
                new Color(0.08f, 0.11f, 0.18f, 1f));
            SetRect(
                gaugeBackground.GetComponent<RectTransform>(),
                new Vector2(0f, -100f),
                new Vector2(600f, 110f));

            GameObject fillArea = new GameObject("GaugeFillArea", typeof(RectTransform));
            fillArea.transform.SetParent(gaugeBackground.transform, false);
            Stretch(fillArea.GetComponent<RectTransform>(), new Vector2(10f, 10f));

            GameObject fillObject = CreateImage(
                "GaugeFill",
                fillArea.transform,
                new Color(0.2f, 0.85f, 0.45f, 1f));
            gaugeFill = fillObject.GetComponent<Image>();
            gaugeFill.rectTransform.anchorMin = Vector2.zero;
            gaugeFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            gaugeFill.rectTransform.offsetMin = Vector2.zero;
            gaugeFill.rectTransform.offsetMax = Vector2.zero;

            GameObject buttonObject = CreateImage(
                "MashButton",
                root.transform,
                new Color(0.95f, 0.32f, 0.20f, 1f));
            SetRect(
                buttonObject.GetComponent<RectTransform>(),
                new Vector2(0f, -390f),
                new Vector2(520f, 230f));
            mashButton = buttonObject.AddComponent<Button>();
            ColorBlock colors = mashButton.colors;
            colors.highlightedColor = new Color(1f, 0.48f, 0.28f, 1f);
            colors.pressedColor = new Color(0.72f, 0.16f, 0.10f, 1f);
            mashButton.colors = colors;
            mashButton.onClick.AddListener(Tap);

            TMP_Text buttonText = CreateText("Label", buttonObject.transform, font, 64);
            buttonText.text = "MASH!";
            buttonText.fontStyle = FontStyles.Bold;
            Stretch(buttonText.rectTransform);

            root.SetActive(false);
        }

        public void Show(QuizQuestion question, Action<bool> onCompleted)
        {
            int targetTaps =
                question.Choices.Count > 0 &&
                int.TryParse(question.Choices[0], out int parsed)
                    ? parsed
                    : 20;
            progress.Reset(targetTaps);
            completed = onCompleted;
            resultSubmitted = false;
            successPending = false;
            mashButton.interactable = true;
            root.SetActive(true);
            Refresh();
        }

        public void Update()
        {
            if (!root.activeSelf || resultSubmitted)
            {
                return;
            }

            if (successPending)
            {
                if (Time.unscaledTime >= successSubmitAt)
                {
                    resultSubmitted = true;
                    successPending = false;
                    completed?.Invoke(true);
                }

                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                Tap();
            }
        }

        public void Hide()
        {
            root.SetActive(false);
            completed = null;
            successPending = false;
        }

        private void Tap()
        {
            if (resultSubmitted || !progress.Tap())
            {
                return;
            }

            Refresh();
            if (!progress.IsComplete)
            {
                return;
            }

            successPending = true;
            successSubmitAt = Time.unscaledTime + FullGaugeDisplaySeconds;
            mashButton.interactable = false;
        }

        private void Refresh()
        {
            gaugeFill.rectTransform.anchorMax = new Vector2(progress.Normalized, 1f);
            gaugeFill.rectTransform.offsetMax = Vector2.zero;
        }

        private static GameObject CreateImage(string name, Transform parent, Color color)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            imageObject.GetComponent<Image>().color = color;
            return imageObject;
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            TMP_FontAsset font,
            int size)
        {
            GameObject textObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            TMP_Text text = textObject.GetComponent<TMP_Text>();
            text.font = font;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.outlineColor = new Color32(0, 0, 0, 230);
            text.outlineWidth = 0.12f;
            return text;
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
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
