namespace Gmtk2026.Quiz
{
    public readonly struct QuizAnswerResult
    {
        public QuizAnswerResult(
            QuizQuestion question,
            int selectedChoiceIndex,
            bool isCorrect,
            bool timedOut)
        {
            Question = question;
            SelectedChoiceIndex = selectedChoiceIndex;
            IsCorrect = isCorrect;
            TimedOut = timedOut;
        }

        public QuizQuestion Question { get; }
        public int SelectedChoiceIndex { get; }
        public bool IsCorrect { get; }
        public bool TimedOut { get; }
    }
}
