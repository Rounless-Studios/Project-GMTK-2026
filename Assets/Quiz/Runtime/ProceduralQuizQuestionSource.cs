using System;
using System.Collections.Generic;

namespace Gmtk2026.Quiz
{
    public sealed class ProceduralQuizQuestionSource : IQuizQuestionSource
    {
        private static readonly CapitalFact[] CapitalFacts =
        {
            new CapitalFact("United States", "Washington, D.C.", new[] { "New York", "Los Angeles", "Chicago" }),
            new CapitalFact("South Korea", "Seoul", new[] { "Busan", "Incheon", "Daejeon" }),
            new CapitalFact("Japan", "Tokyo", new[] { "Osaka", "Kyoto", "Sapporo" }),
            new CapitalFact("France", "Paris", new[] { "Lyon", "Marseille", "Nice" }),
            new CapitalFact("Canada", "Ottawa", new[] { "Toronto", "Vancouver", "Montreal" }),
            new CapitalFact("Australia", "Canberra", new[] { "Sydney", "Melbourne", "Perth" })
        };

        public IEnumerable<QuizQuestion> CreateQuestions(int count, Random random)
        {
            if (count <= 0)
            {
                yield break;
            }

            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            int kindOffset = random.Next(5);
            for (int i = 0; i < count; i++)
            {
                switch ((i + kindOffset) % 5)
                {
                    case 0:
                        yield return CreateArithmetic(random);
                        break;
                    case 1:
                        yield return CreateCapital(random);
                        break;
                    case 2:
                        yield return CreateNumberSequenceClick();
                        break;
                    case 3:
                        yield return CreateRhythmTap(random);
                        break;
                    default:
                        yield return CreateButtonMash(random);
                        break;
                }
            }
        }

        private static QuizQuestion CreateButtonMash(Random random)
        {
            int targetTaps = random.Next(18, 25);
            return new QuizQuestion(
                $"button-mash-{Guid.NewGuid():N}",
                QuizKind.ButtonMash,
                "Mash the button to fill the gauge!",
                new[] { targetTaps.ToString(), "Mash" },
                0,
                $"You filled the gauge with {targetTaps} taps.",
                4f);
        }

        private static QuizQuestion CreateNumberSequenceClick()
        {
            return new QuizQuestion(
                $"number-click-{Guid.NewGuid():N}",
                QuizKind.NumberSequenceClick,
                "Tap the numbers from 1 to 5 in order!",
                new[] { "1", "2", "3", "4", "5" },
                0,
                "You tapped every number from 1 to 5 in order.",
                4f);
        }

        private static QuizQuestion CreateRhythmTap(Random random)
        {
            string[] lanes = new string[6];
            for (int i = 0; i < lanes.Length; i++)
            {
                lanes[i] = random.Next(1, 4).ToString();
            }

            return new QuizQuestion(
                $"rhythm-{Guid.NewGuid():N}",
                QuizKind.RhythmTap,
                "Tap each lane when the note reaches the hit line!",
                lanes,
                0,
                "You played the rhythm successfully.",
                5f);
        }

        private static QuizQuestion CreateArithmetic(Random random)
        {
            int left = random.Next(2, 13);
            int right = random.Next(2, 13);
            bool multiply = random.NextDouble() < 0.55;
            int answer = multiply ? left * right : left + right;
            string op = multiply ? "×" : "+";
            List<string> choices = BuildNumberChoices(answer, random);
            int correctIndex = choices.IndexOf(answer.ToString());

            return new QuizQuestion(
                $"math-{Guid.NewGuid():N}",
                QuizKind.Text,
                $"What is {left} {op} {right}?",
                choices,
                correctIndex,
                $"{left} {op} {right} = {answer}",
                3f);
        }

        private static QuizQuestion CreateCapital(Random random)
        {
            CapitalFact fact = CapitalFacts[random.Next(CapitalFacts.Length)];
            List<string> choices = new List<string> { fact.Capital };
            choices.AddRange(fact.Distractors);
            Shuffle(choices, random);

            return new QuizQuestion(
                $"capital-{Guid.NewGuid():N}",
                QuizKind.MultipleChoice,
                $"What is the capital of {fact.Country}?",
                choices,
                choices.IndexOf(fact.Capital),
                $"The capital of {fact.Country} is {fact.Capital}.",
                4f);
        }

        private static List<string> BuildNumberChoices(int answer, Random random)
        {
            HashSet<int> values = new HashSet<int> { answer };
            int spread = Math.Max(3, Math.Abs(answer) / 4);

            while (values.Count < 4)
            {
                int offset = random.Next(1, spread + 1);
                int candidate = random.NextDouble() < 0.5 ? answer - offset : answer + offset;
                if (candidate >= 0)
                {
                    values.Add(candidate);
                }
            }

            List<string> choices = new List<string>(4);
            foreach (int value in values)
            {
                choices.Add(value.ToString());
            }

            Shuffle(choices, random);
            return choices;
        }

        private static void Shuffle<T>(IList<T> items, Random random)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                (items[i], items[swapIndex]) = (items[swapIndex], items[i]);
            }
        }

        private readonly struct CapitalFact
        {
            public CapitalFact(string country, string capital, string[] distractors)
            {
                Country = country;
                Capital = capital;
                Distractors = distractors;
            }

            public string Country { get; }
            public string Capital { get; }
            public string[] Distractors { get; }
        }
    }
}
