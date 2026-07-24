using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gmtk2026.Quiz
{
    public enum QuizSessionState
    {
        Waiting,
        Question,
        Feedback
    }

    public sealed class QuizSessionController : MonoBehaviour
    {
        private const int QuizKindCount = 5;

        [Header("Question Sources")]
        [SerializeField] private QuizPool authoredPool;

        [Header("Schedule")]
        [SerializeField] private bool autoStart = true;
        [SerializeField, Min(0f)] private float initialDelaySeconds = 2f;
        [SerializeField, Min(1f)] private float minimumIntervalSeconds = 10f;
        [SerializeField, Min(1f)] private float maximumIntervalSeconds = 15f;
        [SerializeField, Min(0.1f)] private float feedbackDurationSeconds = 1.8f;

        [Header("Gameplay")]
        [SerializeField] private bool pauseGameplayDuringQuiz;

        private QuizQuestionSelector selector;
        private float nextQuizAt;
        private float feedbackEndsAt;
        private float previousTimeScale = 1f;
        private bool initialized;
        private readonly HashSet<QuizKind> presentedKinds = new HashSet<QuizKind>();
        private QuizKind? previousPresentedKind;

        public event Action<QuizQuestion> QuestionStarted;
        public event Action<float, float> TimerChanged;
        public event Action<QuizAnswerResult> AnswerEvaluated;
        public event Action QuizClosed;

        public QuizSessionState State { get; private set; } = QuizSessionState.Waiting;
        public QuizQuestion CurrentQuestion { get; private set; }
        public float RemainingSeconds { get; private set; }
        public int CorrectAnswerCount { get; private set; }
        public int TotalQuestionCount { get; private set; }

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            if (initialized)
            {
                ScheduleNext(initialDelaySeconds);
            }
        }

        private void OnDisable()
        {
            RestoreGameplayTime();
        }

        private void Update()
        {
            Tick(Time.unscaledDeltaTime, Time.unscaledTime);
        }

        public void Initialize(int? randomSeed = null)
        {
            IEnumerable<QuizQuestion> authored = authoredPool != null
                ? authoredPool.GetValidQuestions()
                : Array.Empty<QuizQuestion>();

            selector = new QuizQuestionSelector(
                authored,
                new ProceduralQuizQuestionSource(),
                randomSeed);

            presentedKinds.Clear();
            previousPresentedKind = null;
            initialized = true;
            State = QuizSessionState.Waiting;
            ScheduleNext(initialDelaySeconds);
        }

        public void Tick(float unscaledDeltaTime, float unscaledTime)
        {
            if (!initialized)
            {
                return;
            }

            switch (State)
            {
                case QuizSessionState.Waiting:
                    if (autoStart && unscaledTime >= nextQuizAt)
                    {
                        BeginNextQuestion();
                    }
                    break;

                case QuizSessionState.Question:
                    RemainingSeconds = Mathf.Max(0f, RemainingSeconds - Mathf.Max(0f, unscaledDeltaTime));
                    TimerChanged?.Invoke(RemainingSeconds, CurrentQuestion.TimeLimitSeconds);

                    if (RemainingSeconds <= 0f)
                    {
                        CompleteQuestion(-1, timedOut: true);
                    }
                    break;

                case QuizSessionState.Feedback:
                    if (unscaledTime >= feedbackEndsAt)
                    {
                        CloseQuestion();
                    }
                    break;
            }
        }

        public void TriggerNow()
        {
            if (!initialized)
            {
                Initialize();
            }

            if (State == QuizSessionState.Waiting)
            {
                BeginNextQuestion();
            }
        }

        public bool SubmitAnswer(int selectedChoiceIndex)
        {
            if (State != QuizSessionState.Question ||
                selectedChoiceIndex < 0 ||
                selectedChoiceIndex >= CurrentQuestion.Choices.Count)
            {
                return false;
            }

            CompleteQuestion(selectedChoiceIndex, timedOut: false);
            return true;
        }

        public bool SubmitInteractiveResult(bool isCorrect)
        {
            if (State != QuizSessionState.Question ||
                CurrentQuestion == null ||
                (CurrentQuestion.Kind != QuizKind.NumberSequenceClick &&
                 CurrentQuestion.Kind != QuizKind.RhythmTap &&
                 CurrentQuestion.Kind != QuizKind.ButtonMash))
            {
                return false;
            }

            CompleteQuestion(
                isCorrect ? CurrentQuestion.CorrectChoiceIndex : -1,
                timedOut: false,
                correctnessOverride: isCorrect);
            return true;
        }

        public void ConfigureSchedule(
            float initialDelay,
            float minimumInterval,
            float maximumInterval,
            float feedbackDuration)
        {
            initialDelaySeconds = Mathf.Max(0f, initialDelay);
            minimumIntervalSeconds = Mathf.Max(1f, minimumInterval);
            maximumIntervalSeconds = Mathf.Max(minimumIntervalSeconds, maximumInterval);
            feedbackDurationSeconds = Mathf.Max(0.1f, feedbackDuration);

            if (State == QuizSessionState.Waiting)
            {
                ScheduleNext(initialDelaySeconds);
            }
        }

        private void BeginNextQuestion()
        {
            CurrentQuestion = SelectNextQuestionForCycle();
            RemainingSeconds = CurrentQuestion.TimeLimitSeconds;
            State = QuizSessionState.Question;
            TotalQuestionCount++;

            if (pauseGameplayDuringQuiz)
            {
                previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }

            QuestionStarted?.Invoke(CurrentQuestion);
            TimerChanged?.Invoke(RemainingSeconds, CurrentQuestion.TimeLimitSeconds);
        }

        private QuizQuestion SelectNextQuestionForCycle()
        {
            if (presentedKinds.Count >= QuizKindCount)
            {
                presentedKinds.Clear();
            }

            QuizQuestion fallback = null;
            for (int attempt = 0; attempt < QuizKindCount * 2; attempt++)
            {
                QuizQuestion candidate = selector.Next();
                fallback = candidate;
                if (presentedKinds.Contains(candidate.Kind) ||
                    (previousPresentedKind.HasValue &&
                     previousPresentedKind.Value == candidate.Kind))
                {
                    continue;
                }

                presentedKinds.Add(candidate.Kind);
                previousPresentedKind = candidate.Kind;
                return candidate;
            }

            // A malformed custom source must not stall the game indefinitely.
            presentedKinds.Clear();
            presentedKinds.Add(fallback.Kind);
            previousPresentedKind = fallback.Kind;
            return fallback;
        }

        private void CompleteQuestion(
            int selectedChoiceIndex,
            bool timedOut,
            bool? correctnessOverride = null)
        {
            bool isCorrect = !timedOut &&
                             (correctnessOverride ?? CurrentQuestion.IsCorrect(selectedChoiceIndex));
            if (isCorrect)
            {
                CorrectAnswerCount++;
            }

            State = QuizSessionState.Feedback;
            feedbackEndsAt = Time.unscaledTime + feedbackDurationSeconds;
            AnswerEvaluated?.Invoke(
                new QuizAnswerResult(CurrentQuestion, selectedChoiceIndex, isCorrect, timedOut));
            RestoreGameplayTime();
        }

        private void CloseQuestion()
        {
            RestoreGameplayTime();
            State = QuizSessionState.Waiting;
            CurrentQuestion = null;
            QuizClosed?.Invoke();
            ScheduleNext(UnityEngine.Random.Range(minimumIntervalSeconds, maximumIntervalSeconds));
        }

        private void ScheduleNext(float delay)
        {
            nextQuizAt = Time.unscaledTime + Mathf.Max(0f, delay);
        }

        private void RestoreGameplayTime()
        {
            if (pauseGameplayDuringQuiz && Time.timeScale == 0f)
            {
                Time.timeScale = previousTimeScale;
            }
        }

#if UNITY_EDITOR
        public void SetAuthoredPoolForTests(QuizPool pool)
        {
            authoredPool = pool;
        }

        public void BeginQuestionForTests(QuizQuestion question)
        {
            CurrentQuestion = question;
            RemainingSeconds = question.TimeLimitSeconds;
            State = QuizSessionState.Question;
            QuestionStarted?.Invoke(CurrentQuestion);
            TimerChanged?.Invoke(RemainingSeconds, CurrentQuestion.TimeLimitSeconds);
        }
#endif
    }
}
