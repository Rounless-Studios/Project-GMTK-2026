using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gmtk2026.Quiz
{
    public enum QuizKind
    {
        Text = 0,
        MultipleChoice = 1,
        NumberSequenceClick = 4,
        RhythmTap = 5,
        ButtonMash = 6
    }

    [Serializable]
    public sealed class QuizQuestion
    {
        [SerializeField] private string id;
        [SerializeField] private QuizKind kind;
        [SerializeField, TextArea(2, 5)] private string prompt;
        [SerializeField] private List<string> choices = new List<string>();
        [SerializeField] private int correctChoiceIndex;
        [SerializeField, TextArea(1, 3)] private string explanation;
        [SerializeField, Min(1f)] private float timeLimitSeconds = 8f;

        public string Id => id;
        public QuizKind Kind => kind;
        public string Prompt => prompt;
        public IReadOnlyList<string> Choices => choices;
        public int CorrectChoiceIndex => correctChoiceIndex;
        public string Explanation => explanation;
        public float TimeLimitSeconds => Mathf.Max(1f, timeLimitSeconds);

        public QuizQuestion(
            string id,
            QuizKind kind,
            string prompt,
            IList<string> choices,
            int correctChoiceIndex,
            string explanation,
            float timeLimitSeconds)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                throw new ArgumentException("Quiz prompt cannot be empty.", nameof(prompt));
            }

            if (choices == null || choices.Count < 2)
            {
                throw new ArgumentException("A quiz requires at least two choices.", nameof(choices));
            }

            if (correctChoiceIndex < 0 || correctChoiceIndex >= choices.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(correctChoiceIndex));
            }

            for (int i = 0; i < choices.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(choices[i]))
                {
                    throw new ArgumentException("Quiz choices cannot be empty.", nameof(choices));
                }
            }

            this.id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
            this.kind = kind;
            this.prompt = prompt.Trim();
            this.choices = new List<string>(choices);
            this.correctChoiceIndex = correctChoiceIndex;
            this.explanation = explanation ?? string.Empty;
            this.timeLimitSeconds = Mathf.Max(1f, timeLimitSeconds);
        }

        public bool IsCorrect(int selectedChoiceIndex)
        {
            return selectedChoiceIndex == correctChoiceIndex;
        }

        public bool IsValid(out string reason)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                reason = "Prompt is empty.";
                return false;
            }

            if (choices == null || choices.Count < 2)
            {
                reason = "At least two choices are required.";
                return false;
            }

            if (correctChoiceIndex < 0 || correctChoiceIndex >= choices.Count)
            {
                reason = "Correct choice index is out of range.";
                return false;
            }

            foreach (string choice in choices)
            {
                if (string.IsNullOrWhiteSpace(choice))
                {
                    reason = "Choices cannot be empty.";
                    return false;
                }
            }

            reason = string.Empty;
            return true;
        }
    }
}
