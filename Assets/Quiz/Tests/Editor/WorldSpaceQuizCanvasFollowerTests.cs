using NUnit.Framework;
using UnityEngine;

namespace Gmtk2026.Quiz.Tests
{
    public sealed class WorldSpaceQuizCanvasFollowerTests
    {
        [Test]
        public void GetTargetPosition_UsesCameraLocalForwardAndUp()
        {
            GameObject cameraObject = new GameObject("TestCamera");
            try
            {
                cameraObject.transform.position = new Vector3(4f, 3f, -2f);
                cameraObject.transform.rotation = Quaternion.Euler(10f, 35f, 0f);

                Vector3 position = WorldSpaceQuizCanvasFollower.GetTargetPosition(
                    cameraObject.transform,
                    6f,
                    0.55f,
                    0.35f);

                Vector3 expected = cameraObject.transform.position +
                                   cameraObject.transform.forward * 6f +
                                   cameraObject.transform.right * 0.55f +
                                   cameraObject.transform.up * 0.35f;
                Assert.That(Vector3.Distance(position, expected), Is.LessThan(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void AdvancePresentation_ReachesTargetsUsingConfiguredDurations()
        {
            float shownHalfway = WorldSpaceQuizCanvasFollower.AdvancePresentation(
                0f,
                true,
                0.14f,
                0.28f,
                0.20f);
            float shown = WorldSpaceQuizCanvasFollower.AdvancePresentation(
                shownHalfway,
                true,
                0.14f,
                0.28f,
                0.20f);
            float hidden = WorldSpaceQuizCanvasFollower.AdvancePresentation(
                shown,
                false,
                0.20f,
                0.28f,
                0.20f);

            Assert.That(shownHalfway, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(shown, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(hidden, Is.EqualTo(0f).Within(0.0001f));
        }
    }
}
