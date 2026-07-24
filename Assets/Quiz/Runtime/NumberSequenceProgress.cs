namespace Gmtk2026.Quiz
{
    public sealed class NumberSequenceProgress
    {
        public NumberSequenceProgress(int finalNumber = 5)
        {
            FinalNumber = finalNumber < 1 ? 1 : finalNumber;
            Reset();
        }

        public int FinalNumber { get; }
        public int ExpectedNumber { get; private set; }
        public bool IsComplete => ExpectedNumber > FinalNumber;

        public bool TrySelect(int number)
        {
            if (IsComplete || number != ExpectedNumber)
            {
                return false;
            }

            ExpectedNumber++;
            return true;
        }

        public void Reset()
        {
            ExpectedNumber = 1;
        }
    }
}
