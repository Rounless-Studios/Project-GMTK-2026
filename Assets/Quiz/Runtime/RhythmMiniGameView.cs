using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gmtk2026.Quiz
{
    public sealed class RhythmMiniGameView
    {
        private const float HitWindow = 0.24f;
        private const float FirstBeatTime = 1.55f;
        private const float BeatInterval = 0.55f;
        private const float JudgmentLineY = -170f;
        private const float NoteSpeed = 320f;

        private readonly GameObject root;
        private readonly TMP_Text statusText;
        private readonly List<RhythmNote> notes = new List<RhythmNote>();
        private Action<bool> completed;
        private float startedAt;
        private int hits;
        private int misses;
        private bool resultSubmitted;

        public RhythmMiniGameView(Transform parent, TMP_FontAsset font)
        {
            root = new GameObject(
                "RhythmMiniGame",
                typeof(RectTransform),
                typeof(RectMask2D));
            root.transform.SetParent(parent, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.anchoredPosition = new Vector2(0f, -410f);
            rootRect.sizeDelta = new Vector2(700f, 720f);

            for (int lane = 0; lane < 3; lane++)
            {
                GameObject laneBackground = CreateImage(
                    $"Lane {lane + 1}",
                    root.transform,
                    new Color(0.08f, 0.11f, 0.18f, 0.9f));
                RectTransform laneRect = laneBackground.GetComponent<RectTransform>();
                laneRect.anchorMin = laneRect.anchorMax = laneRect.pivot = new Vector2(0.5f, 0.5f);
                laneRect.anchoredPosition = new Vector2((lane - 1) * 205f, 35f);
                laneRect.sizeDelta = new Vector2(175f, 560f);

                int capturedLane = lane;
                GameObject laneButtonObject = CreateImage(
                    $"Tap {lane + 1}",
                    root.transform,
                    new Color(0.12f, 0.30f, 0.50f, 1f));
                RectTransform buttonRect = laneButtonObject.GetComponent<RectTransform>();
                buttonRect.anchorMin = buttonRect.anchorMax = buttonRect.pivot = new Vector2(0.5f, 0.5f);
                buttonRect.anchoredPosition = new Vector2((lane - 1) * 205f, -300f);
                buttonRect.sizeDelta = new Vector2(175f, 100f);
                Button button = laneButtonObject.AddComponent<Button>();
                button.onClick.AddListener(() => TapLane(capturedLane));

                TMP_Text label = CreateText("Label", laneButtonObject.transform, font, 44);
                label.text = (lane + 1).ToString();
                Stretch(label.rectTransform);
            }

            GameObject judgmentLine = CreateImage(
                "JudgmentLine",
                root.transform,
                new Color(0.25f, 0.85f, 1f, 1f));
            RectTransform lineRect = judgmentLine.GetComponent<RectTransform>();
            lineRect.anchorMin = lineRect.anchorMax = lineRect.pivot = new Vector2(0.5f, 0.5f);
            lineRect.anchoredPosition = new Vector2(0f, JudgmentLineY);
            lineRect.sizeDelta = new Vector2(590f, 12f);

            statusText = CreateText("Status", root.transform, font, 26);
            statusText.alignment = TextAlignmentOptions.Center;
            RectTransform statusRect = statusText.rectTransform;
            statusRect.anchorMin = statusRect.anchorMax = statusRect.pivot = new Vector2(0.5f, 1f);
            statusRect.anchoredPosition = new Vector2(0f, -5f);
            statusRect.sizeDelta = new Vector2(600f, 55f);
            root.SetActive(false);
        }

        public void Show(QuizQuestion question, Action<bool> onCompleted)
        {
            ClearNotes();
            completed = onCompleted;
            resultSubmitted = false;
            hits = 0;
            misses = 0;
            startedAt = Time.unscaledTime;
            root.SetActive(true);
            statusText.text = "Use the 1 · 2 · 3 keys or buttons";

            for (int i = 0; i < question.Choices.Count; i++)
            {
                int lane = int.TryParse(question.Choices[i], out int parsed)
                    ? Mathf.Clamp(parsed - 1, 0, 2)
                    : 0;
                GameObject noteObject = CreateImage(
                    $"Note {i + 1}",
                    root.transform,
                    LaneColor(lane));
                RectTransform rect = noteObject.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(130f, 58f);
                notes.Add(new RhythmNote(
                    lane,
                    FirstBeatTime + i * BeatInterval,
                    noteObject));
            }
        }

        public void Update()
        {
            if (!root.activeSelf || resultSubmitted)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) TapLane(0);
                if (keyboard.digit2Key.wasPressedThisFrame) TapLane(1);
                if (keyboard.digit3Key.wasPressedThisFrame) TapLane(2);
            }

            float elapsed = Time.unscaledTime - startedAt;
            foreach (RhythmNote note in notes)
            {
                if (note.Resolved)
                {
                    continue;
                }

                RectTransform rect = note.Object.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(
                    (note.Lane - 1) * 205f,
                    JudgmentLineY + (note.BeatTime - elapsed) * NoteSpeed);

                if (elapsed > note.BeatTime + HitWindow)
                {
                    note.Resolved = true;
                    note.Object.SetActive(false);
                    misses++;
                    statusText.text = "MISS";
                }
            }

            FinishIfComplete();
        }

        public void Hide()
        {
            root.SetActive(false);
            completed = null;
            ClearNotes();
        }

        private void TapLane(int lane)
        {
            if (resultSubmitted)
            {
                return;
            }

            float elapsed = Time.unscaledTime - startedAt;
            RhythmNote candidate = null;
            float bestDifference = float.MaxValue;
            foreach (RhythmNote note in notes)
            {
                if (note.Resolved || note.Lane != lane)
                {
                    continue;
                }

                float difference = Mathf.Abs(elapsed - note.BeatTime);
                if (difference < bestDifference)
                {
                    candidate = note;
                    bestDifference = difference;
                }
            }

            if (candidate != null && bestDifference <= HitWindow)
            {
                candidate.Resolved = true;
                candidate.Object.SetActive(false);
                hits++;
                statusText.text = bestDifference <= 0.1f ? "PERFECT" : "GOOD";
            }
            else
            {
                misses++;
                statusText.text = "MISS";
            }

            FinishIfComplete();
        }

        private void FinishIfComplete()
        {
            int resolvedNotes = 0;
            foreach (RhythmNote note in notes)
            {
                if (note.Resolved)
                {
                    resolvedNotes++;
                }
            }

            if (resolvedNotes < notes.Count)
            {
                return;
            }

            resultSubmitted = true;
            completed?.Invoke(hits >= Mathf.CeilToInt(notes.Count * 0.7f));
        }

        private void ClearNotes()
        {
            foreach (RhythmNote note in notes)
            {
                if (note.Object != null)
                {
                    UnityEngine.Object.Destroy(note.Object);
                }
            }

            notes.Clear();
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

        private static Color LaneColor(int lane)
        {
            switch (lane)
            {
                case 0: return new Color(0.25f, 0.75f, 1f, 1f);
                case 1: return new Color(1f, 0.42f, 0.65f, 1f);
                default: return new Color(0.65f, 0.9f, 0.3f, 1f);
            }
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private sealed class RhythmNote
        {
            public RhythmNote(int lane, float beatTime, GameObject noteObject)
            {
                Lane = lane;
                BeatTime = beatTime;
                Object = noteObject;
            }

            public int Lane { get; }
            public float BeatTime { get; }
            public GameObject Object { get; }
            public bool Resolved { get; set; }
        }
    }
}
