using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gmtk2026.Quiz
{
    public sealed class NumberSequenceMiniGameView
    {
        private static readonly Color NormalColor = new Color(0.12f, 0.30f, 0.50f, 1f);
        private static readonly Color CorrectColor = new Color(0.16f, 0.65f, 0.35f, 1f);
        private static readonly Color WrongColor = new Color(0.85f, 0.25f, 0.25f, 1f);

        private readonly GameObject root;
        private readonly List<Button> buttons = new List<Button>();
        private readonly NumberSequenceProgress progress = new NumberSequenceProgress();
        private Action<bool> completed;
        private bool resultSubmitted;

        public NumberSequenceMiniGameView(Transform parent, TMP_FontAsset font)
        {
            root = new GameObject("NumberSequenceMiniGame", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.anchoredPosition = new Vector2(0f, -410f);
            rootRect.sizeDelta = new Vector2(700f, 720f);

            for (int i = 1; i <= 5; i++)
            {
                int value = i;
                GameObject buttonObject = new GameObject(
                    $"Number {value}",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(Button));
                buttonObject.transform.SetParent(root.transform, false);
                Button button = buttonObject.GetComponent<Button>();
                button.image.color = NormalColor;
                button.onClick.AddListener(() => SelectNumber(value));

                GameObject labelObject = new GameObject(
                    "Label",
                    typeof(RectTransform),
                    typeof(TextMeshProUGUI));
                labelObject.transform.SetParent(buttonObject.transform, false);
                TMP_Text label = labelObject.GetComponent<TMP_Text>();
                label.font = font;
                label.fontSize = 64;
                label.fontStyle = FontStyles.Bold;
                label.alignment = TextAlignmentOptions.Center;
                label.color = Color.white;
                label.text = value.ToString();
                label.outlineColor = new Color32(0, 0, 0, 230);
                label.outlineWidth = 0.12f;
                Stretch(label.rectTransform);
                buttons.Add(button);
            }

            root.SetActive(false);
        }

        public void Show(Action<bool> onCompleted)
        {
            completed = onCompleted;
            resultSubmitted = false;
            progress.Reset();
            root.SetActive(true);

            Vector2[] positions =
            {
                new Vector2(-240f, 240f),
                new Vector2(0f, 250f),
                new Vector2(240f, 220f),
                new Vector2(-220f, 0f),
                new Vector2(20f, 20f),
                new Vector2(245f, -10f),
                new Vector2(-235f, -240f),
                new Vector2(0f, -235f),
                new Vector2(235f, -225f)
            };

            for (int i = positions.Length - 1; i > 0; i--)
            {
                int swapIndex = UnityEngine.Random.Range(0, i + 1);
                (positions[i], positions[swapIndex]) = (positions[swapIndex], positions[i]);
            }

            for (int i = 0; i < buttons.Count; i++)
            {
                Button button = buttons[i];
                button.interactable = true;
                button.image.color = NormalColor;
                RectTransform rect = button.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = positions[i];
                rect.sizeDelta = new Vector2(132f, 132f);
            }
        }

        public void Hide()
        {
            root.SetActive(false);
            completed = null;
        }

        private void SelectNumber(int number)
        {
            if (resultSubmitted)
            {
                return;
            }

            Button button = buttons[number - 1];
            if (!progress.TrySelect(number))
            {
                button.image.color = WrongColor;
                SubmitResult(false);
                return;
            }

            button.image.color = CorrectColor;
            button.interactable = false;
            if (progress.IsComplete)
            {
                SubmitResult(true);
            }
        }

        private void SubmitResult(bool isCorrect)
        {
            resultSubmitted = true;
            foreach (Button button in buttons)
            {
                button.interactable = false;
            }

            completed?.Invoke(isCorrect);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
