using NUnit.Framework;

namespace Gmtk2026.Quiz.Tests
{
    public sealed class NumberSequenceProgressTests
    {
        [Test]
        public void TrySelect_CompletesOnlyInAscendingOrder()
        {
            NumberSequenceProgress progress = new NumberSequenceProgress(5);

            Assert.That(progress.TrySelect(1), Is.True);
            Assert.That(progress.TrySelect(3), Is.False);
            Assert.That(progress.TrySelect(2), Is.True);
            Assert.That(progress.TrySelect(3), Is.True);
            Assert.That(progress.TrySelect(4), Is.True);
            Assert.That(progress.TrySelect(5), Is.True);
            Assert.That(progress.IsComplete, Is.True);
        }

        [Test]
        public void Reset_RestartsAtOne()
        {
            NumberSequenceProgress progress = new NumberSequenceProgress(2);
            progress.TrySelect(1);
            progress.Reset();

            Assert.That(progress.ExpectedNumber, Is.EqualTo(1));
            Assert.That(progress.IsComplete, Is.False);
        }
    }
}
