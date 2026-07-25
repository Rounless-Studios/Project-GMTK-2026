using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Gmtk2026.Quiz.Tests
{
    public sealed class QuizSessionControllerTests
    {
        private GameObject gameObject;
        private QuizSessionController controller;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("QuizSessionControllerTests");
            controller = gameObject.AddComponent<QuizSessionController>();
            controller.Initialize(randomSeed: 42);
            controller.ConfigureFeedbackDuration(0.1f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void TriggerNow_StartsQuestion()
        {
            controller.TriggerNow();

            Assert.That(controller.State, Is.EqualTo(QuizSessionState.Question));
            Assert.That(controller.CurrentQuestion, Is.Not.Null);
            Assert.That(controller.RemainingSeconds, Is.GreaterThan(0f));
            Assert.That(controller.TotalQuestionCount, Is.EqualTo(1));
        }

        [Test]
        public void Tick_DoesNotStartPeriodicQuestion()
        {
            controller.Tick(1000f, float.MaxValue);

            Assert.That(controller.State, Is.EqualTo(QuizSessionState.Waiting));
            Assert.That(controller.CurrentQuestion, Is.Null);
            Assert.That(controller.TotalQuestionCount, Is.Zero);
        }

        [Test]
        public void SubmitCorrectAnswer_ProducesCorrectFeedback()
        {
            QuizAnswerResult? observed = null;
            controller.AnswerEvaluated += result => observed = result;
            controller.TriggerNow();

            bool accepted = controller.SubmitAnswer(controller.CurrentQuestion.CorrectChoiceIndex);

            Assert.That(accepted, Is.True);
            Assert.That(observed.HasValue, Is.True);
            Assert.That(observed.Value.IsCorrect, Is.True);
            Assert.That(observed.Value.TimedOut, Is.False);
            Assert.That(controller.CorrectAnswerCount, Is.EqualTo(1));
            Assert.That(controller.State, Is.EqualTo(QuizSessionState.Feedback));
        }

        [Test]
        public void Tick_WhenTimerExpires_ProducesTimeoutFeedback()
        {
            QuizAnswerResult? observed = null;
            controller.AnswerEvaluated += result => observed = result;
            controller.TriggerNow();

            controller.Tick(controller.CurrentQuestion.TimeLimitSeconds + 0.1f, Time.unscaledTime);

            Assert.That(observed.HasValue, Is.True);
            Assert.That(observed.Value.IsCorrect, Is.False);
            Assert.That(observed.Value.TimedOut, Is.True);
            Assert.That(observed.Value.SelectedChoiceIndex, Is.EqualTo(-1));
            Assert.That(controller.State, Is.EqualTo(QuizSessionState.Feedback));
        }

        [Test]
        public void SubmitAnswer_RejectsSecondSubmission()
        {
            controller.TriggerNow();
            int correctIndex = controller.CurrentQuestion.CorrectChoiceIndex;

            Assert.That(controller.SubmitAnswer(correctIndex), Is.True);
            Assert.That(controller.SubmitAnswer(correctIndex), Is.False);
        }

        [Test]
        public void SubmitInteractiveResult_CompletesInteractiveQuestion()
        {
            QuizQuestion question = new QuizQuestion(
                "interactive",
                QuizKind.NumberSequenceClick,
                "Press in order",
                new[] { "1", "2", "3", "4", "5" },
                0,
                "Done",
                7f);
            QuizAnswerResult? observed = null;
            controller.AnswerEvaluated += result => observed = result;
            controller.BeginQuestionForTests(question);

            bool accepted = controller.SubmitInteractiveResult(true);

            Assert.That(accepted, Is.True);
            Assert.That(observed.HasValue, Is.True);
            Assert.That(observed.Value.IsCorrect, Is.True);
            Assert.That(controller.State, Is.EqualTo(QuizSessionState.Feedback));
        }

        [Test]
        public void SubmitInteractiveResult_AcceptsButtonMashQuestion()
        {
            GameObject gameObject = new GameObject("ButtonMashSessionTest");
            try
            {
                QuizSessionController controller = gameObject.AddComponent<QuizSessionController>();
                QuizQuestion question = new QuizQuestion(
                    "button-mash-test",
                    QuizKind.ButtonMash,
                    "Mash!",
                    new[] { "20", "Mash" },
                    0,
                    string.Empty,
                    6f);
                controller.BeginQuestionForTests(question);

                Assert.That(controller.SubmitInteractiveResult(true), Is.True);
                Assert.That(controller.State, Is.EqualTo(QuizSessionState.Feedback));
                Assert.That(controller.CorrectAnswerCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void RuntimeQuestions_UseEveryKindBeforeRepeating()
        {
            controller.Initialize(randomSeed: 2026);
            HashSet<QuizKind> cycleKinds = new HashSet<QuizKind>();
            QuizKind? previousKind = null;

            for (int i = 0; i < 10; i++)
            {
                controller.TriggerNow();
                QuizKind kind = controller.CurrentQuestion.Kind;

                Assert.That(kind, Is.Not.EqualTo(previousKind));
                Assert.That(cycleKinds.Add(kind), Is.True);
                previousKind = kind;

                if (kind == QuizKind.NumberSequenceClick ||
                    kind == QuizKind.RhythmTap ||
                    kind == QuizKind.ButtonMash)
                {
                    controller.SubmitInteractiveResult(true);
                }
                else
                {
                    controller.SubmitAnswer(controller.CurrentQuestion.CorrectChoiceIndex);
                }

                controller.Tick(0f, float.MaxValue);
                if (cycleKinds.Count == 5)
                {
                    cycleKinds.Clear();
                }
            }
        }
    }
}
