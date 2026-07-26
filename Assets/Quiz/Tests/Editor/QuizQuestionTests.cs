using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Gmtk2026.Quiz.Tests
{
    public sealed class QuizQuestionTests
    {
        [Test]
        public void Constructor_RejectsInvalidCorrectIndex()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new QuizQuestion(
                    "invalid",
                    QuizKind.Text,
                    "Question?",
                    new[] { "A", "B" },
                    2,
                    string.Empty,
                    5f));
        }

        [Test]
        public void IsCorrect_OnlyAcceptsCorrectChoice()
        {
            QuizQuestion question = new QuizQuestion(
                "answer",
                QuizKind.Text,
                "What is 3 × 3?",
                new[] { "6", "9", "12" },
                1,
                "3 × 3 = 9",
                5f);

            Assert.That(question.IsCorrect(1), Is.True);
            Assert.That(question.IsCorrect(0), Is.False);
        }

        [Test]
        public void ProceduralSource_CreatesValidVariedQuestions()
        {
            ProceduralQuizQuestionSource source = new ProceduralQuizQuestionSource();
            List<QuizQuestion> questions =
                new List<QuizQuestion>(source.CreateQuestions(100, new Random(2026)));
            HashSet<QuizKind> kinds = new HashSet<QuizKind>();

            Assert.That(questions, Has.Count.EqualTo(100));
            foreach (QuizQuestion question in questions)
            {
                Assert.That(question.IsValid(out string reason), Is.True, reason);
                Assert.That(question.Choices.Count, Is.InRange(2, 6));
                Assert.That(question.TimeLimitSeconds, Is.GreaterThan(0f));
                kinds.Add(question.Kind);
            }

            Assert.That(kinds, Has.Count.EqualTo(5));
            Assert.That(kinds.Contains((QuizKind)2), Is.False);
            Assert.That(kinds.Contains((QuizKind)3), Is.False);
        }

        [Test]
        public void ProceduralSource_UsesConfiguredTimeAndAnswerCount()
        {
            var source = new ProceduralQuizQuestionSource(
                timeLimitSeconds: 2.5f,
                minimumAnswers: 2,
                maximumAnswers: 3);
            var questions = new List<QuizQuestion>(
                source.CreateQuestions(50, new Random(2026)));

            foreach (QuizQuestion question in questions)
            {
                Assert.That(question.TimeLimitSeconds, Is.EqualTo(2.5f));
                if (question.Kind == QuizKind.Text ||
                    question.Kind == QuizKind.MultipleChoice)
                    Assert.That(question.Choices.Count, Is.InRange(2, 3));
            }
        }

        [Test]
        public void Selector_DoesNotRepeatGeneratedQuestionIdImmediately()
        {
            QuizQuestionSelector selector = new QuizQuestionSelector(
                Array.Empty<QuizQuestion>(),
                new ProceduralQuizQuestionSource(),
                seed: 77);
            string previousId = null;

            for (int i = 0; i < 80; i++)
            {
                QuizQuestion question = selector.Next();
                Assert.That(question.Id, Is.Not.EqualTo(previousId));
                previousId = question.Id;
            }
        }

        [Test]
        public void Selector_UsesEveryKindOncePerCycleWithoutAdjacentRepeats()
        {
            QuizQuestionSelector selector = new QuizQuestionSelector(
                Array.Empty<QuizQuestion>(),
                new ProceduralQuizQuestionSource(),
                seed: 2026);
            QuizKind? previousKind = null;

            for (int cycle = 0; cycle < 20; cycle++)
            {
                HashSet<QuizKind> cycleKinds = new HashSet<QuizKind>();
                for (int i = 0; i < 5; i++)
                {
                    QuizKind kind = selector.Next().Kind;
                    Assert.That(kind, Is.Not.EqualTo(previousKind));
                    Assert.That(cycleKinds.Add(kind), Is.True);
                    previousKind = kind;
                }

                Assert.That(cycleKinds, Has.Count.EqualTo(5));
            }
        }
    }
}
