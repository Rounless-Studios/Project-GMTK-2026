using NUnit.Framework;

namespace Gmtk2026.Quiz.Tests
{
    public sealed class ButtonMashProgressTests
    {
        [Test]
        public void Tap_FillsGaugeAndCompletesAtTarget()
        {
            ButtonMashProgress progress = new ButtonMashProgress();
            progress.Reset(3);

            Assert.That(progress.Tap(), Is.True);
            Assert.That(progress.Normalized, Is.EqualTo(1f / 3f).Within(0.0001f));
            Assert.That(progress.IsComplete, Is.False);

            progress.Tap();
            progress.Tap();

            Assert.That(progress.IsComplete, Is.True);
            Assert.That(progress.Normalized, Is.EqualTo(1f));
            Assert.That(progress.Tap(), Is.False);
            Assert.That(progress.CurrentTaps, Is.EqualTo(3));
        }

        [Test]
        public void Reset_ClampsTargetAndClearsProgress()
        {
            ButtonMashProgress progress = new ButtonMashProgress();
            progress.Reset(2);
            progress.Tap();
            progress.Reset(0);

            Assert.That(progress.TargetTaps, Is.EqualTo(1));
            Assert.That(progress.CurrentTaps, Is.Zero);
            Assert.That(progress.IsComplete, Is.False);
        }
    }
}
