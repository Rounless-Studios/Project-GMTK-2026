using System;
using System.Collections.Generic;

namespace Gmtk2026.Quiz
{
    public sealed class QuizQuestionSelector
    {
        private const int GeneratedBatchSize = 24;
        private static readonly QuizKind[] SupportedKinds =
        {
            QuizKind.Text,
            QuizKind.MultipleChoice,
            QuizKind.NumberSequenceClick,
            QuizKind.RhythmTap,
            QuizKind.ButtonMash
        };

        private readonly Random random;
        private readonly IQuizQuestionSource generatedSource;
        private readonly List<QuizQuestion> authoredQuestions = new List<QuizQuestion>();
        private readonly List<QuizQuestion> generatedBag = new List<QuizQuestion>();
        private readonly List<QuizKind> kindBag = new List<QuizKind>();
        private string previousQuestionId;
        private QuizKind? previousKind;

        public QuizQuestionSelector(
            IEnumerable<QuizQuestion> authoredQuestions,
            IQuizQuestionSource generatedSource,
            int? seed = null)
        {
            this.generatedSource = generatedSource ?? throw new ArgumentNullException(nameof(generatedSource));
            random = seed.HasValue ? new Random(seed.Value) : new Random();

            if (authoredQuestions == null)
            {
                return;
            }

            foreach (QuizQuestion question in authoredQuestions)
            {
                if (question != null && question.IsValid(out _))
                {
                    this.authoredQuestions.Add(question);
                }
            }
        }

        public QuizQuestion Next()
        {
            QuizKind targetKind = NextKind();
            EnsureGeneratedKind(targetKind);

            bool useAuthored = HasKind(authoredQuestions, targetKind) && random.NextDouble() < 0.35;
            QuizQuestion question = useAuthored
                ? PickKind(authoredQuestions, targetKind, remove: false)
                : PickKind(generatedBag, targetKind, remove: true);

            previousQuestionId = question.Id;
            previousKind = question.Kind;
            return question;
        }

        private QuizKind NextKind()
        {
            if (kindBag.Count == 0)
            {
                kindBag.AddRange(SupportedKinds);
                Shuffle(kindBag);

                if (previousKind.HasValue &&
                    kindBag.Count > 1 &&
                    kindBag[0] == previousKind.Value)
                {
                    int swapIndex = random.Next(1, kindBag.Count);
                    (kindBag[0], kindBag[swapIndex]) = (kindBag[swapIndex], kindBag[0]);
                }
            }

            QuizKind result = kindBag[0];
            kindBag.RemoveAt(0);
            return result;
        }

        private void EnsureGeneratedKind(QuizKind kind)
        {
            int attempts = 0;
            while (!HasKind(generatedBag, kind) && attempts < 2)
            {
                foreach (QuizQuestion question in generatedSource.CreateQuestions(GeneratedBatchSize, random))
                {
                    if (question != null && question.IsValid(out _))
                    {
                        generatedBag.Add(question);
                    }
                }

                attempts++;
            }

            if (!HasKind(generatedBag, kind))
            {
                throw new InvalidOperationException(
                    $"The quiz source did not produce a valid question of kind {kind}.");
            }
        }

        private QuizQuestion PickKind(List<QuizQuestion> source, QuizKind kind, bool remove)
        {
            List<int> matchingIndices = new List<int>();
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i].Kind == kind)
                {
                    matchingIndices.Add(i);
                }
            }

            int index = matchingIndices[random.Next(matchingIndices.Count)];
            if (matchingIndices.Count > 1 && source[index].Id == previousQuestionId)
            {
                int currentMatch = matchingIndices.IndexOf(index);
                index = matchingIndices[(currentMatch + 1) % matchingIndices.Count];
            }

            QuizQuestion result = source[index];
            if (remove)
            {
                source.RemoveAt(index);
            }

            return result;
        }

        private static bool HasKind(List<QuizQuestion> source, QuizKind kind)
        {
            foreach (QuizQuestion question in source)
            {
                if (question.Kind == kind)
                {
                    return true;
                }
            }

            return false;
        }

        private void Shuffle<T>(IList<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                (items[i], items[swapIndex]) = (items[swapIndex], items[i]);
            }
        }
    }
}
