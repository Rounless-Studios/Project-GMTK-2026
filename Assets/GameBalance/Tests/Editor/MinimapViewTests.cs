using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Gmtk2026.GameBalance.Tests
{
    public class MinimapViewTests
    {
        /// <summary>100 m square loop on the XZ plane, one point every 10 m: 40 points, 400 m long.</summary>
        private static List<Vector3> DenseSquare()
        {
            var points = new List<Vector3>();
            for (int i = 0; i < 10; i++) points.Add(new Vector3(i * 10f, 0f, 0f));
            for (int i = 0; i < 10; i++) points.Add(new Vector3(100f, 0f, i * 10f));
            for (int i = 0; i < 10; i++) points.Add(new Vector3(100f - i * 10f, 0f, 100f));
            for (int i = 0; i < 10; i++) points.Add(new Vector3(0f, 0f, 100f - i * 10f));
            return points;
        }

        [Test]
        public void ThinningKeepsOnePointPerSpacing()
        {
            var thinned = new List<Vector3>();

            MinimapView.Thin(DenseSquare(), 20f, thinned);

            Assert.AreEqual(20, thinned.Count);
            Assert.AreEqual(new Vector3(0f, 0f, 0f), thinned[0], "the first point anchors the outline");
            Assert.AreEqual(new Vector3(20f, 0f, 0f), thinned[1]);
        }

        [Test]
        public void ThinningIgnoresHeightSoCrestsDoNotAddPoints()
        {
            var source = new List<Vector3>
            {
                new(0f, 0f, 0f),
                new(2f, 30f, 0f),      // 30 m up but 2 m along: a hill, not a corner
                new(20f, 0f, 0f)
            };
            var thinned = new List<Vector3>();

            MinimapView.Thin(source, 10f, thinned);

            Assert.AreEqual(2, thinned.Count);
        }

        [Test]
        public void ThinningDropsALastPointCrowdingTheFirst()
        {
            var source = new List<Vector3>
            {
                new(0f, 0f, 0f),
                new(50f, 0f, 0f),
                new(50f, 0f, 50f),
                new(1f, 0f, 0f)        // back at the start: the closing segment already covers it
            };
            var thinned = new List<Vector3>();

            MinimapView.Thin(source, 10f, thinned);

            Assert.AreEqual(3, thinned.Count);
            Assert.AreEqual(new Vector3(50f, 0f, 50f), thinned[thinned.Count - 1]);
        }

        [Test]
        public void ThinningAnEmptyPathClearsTheResult()
        {
            var thinned = new List<Vector3> { Vector3.one };

            MinimapView.Thin(new List<Vector3>(), 10f, thinned);

            Assert.AreEqual(0, thinned.Count);
        }

        [Test]
        public void FlatLengthClosesTheLoop()
        {
            Assert.AreEqual(400f, MinimapView.FlatLength(DenseSquare()), 0.001f);
            Assert.AreEqual(0f, MinimapView.FlatLength(new List<Vector3> { Vector3.zero }));
        }

        [Test]
        public void BoundsCoverThePathOnTheXzPlane()
        {
            Assert.IsTrue(MinimapView.Bounds(DenseSquare(), out Vector2 centre, out Vector2 size));

            Assert.AreEqual(new Vector2(50f, 50f), centre);
            Assert.AreEqual(new Vector2(100f, 100f), size);
            Assert.IsFalse(MinimapView.Bounds(new List<Vector3>(), out _, out _));
        }

        [Test]
        public void FitScaleUsesTheTighterAxisAndRespectsPadding()
        {
            // 200 x 100 m into a 240 x 240 px panel with 20 px padding: 200 px usable either way
            float scale = MinimapView.FitPixelsPerMetre(new Vector2(200f, 100f), new Vector2(240f, 240f), 20f);

            Assert.AreEqual(1f, scale, 0.001f, "the long axis decides, so nothing spills out of the panel");
            Assert.AreEqual(1f, MinimapView.FitPixelsPerMetre(Vector2.zero, new Vector2(240f, 240f), 20f),
                "a path with no extent must not divide by zero");
        }

        [Test]
        public void NorthUpProjectionKeepsWorldAxes()
        {
            Vector2 offset = MinimapView.ToPanel(new Vector3(30f, 5f, 10f), new Vector2(20f, 20f), 2f, 0f);

            Assert.AreEqual(20f, offset.x, 0.001f, "+X is right and height is ignored");
            Assert.AreEqual(-20f, offset.y, 0.001f, "+Z is up");
        }

        [Test]
        public void RotatedProjectionPutsTheCarsHeadingUp()
        {
            // the car sits at the origin facing +X (heading 90)
            var centre = new Vector2(0f, 0f);

            Vector2 ahead = MinimapView.ToPanel(new Vector3(10f, 0f, 0f), centre, 1f, 90f);
            Vector2 right = MinimapView.ToPanel(new Vector3(0f, 0f, -10f), centre, 1f, 90f);

            Assert.AreEqual(0f, ahead.x, 0.001f);
            Assert.AreEqual(10f, ahead.y, 0.001f, "what is in front of the car must be above it");
            Assert.AreEqual(10f, right.x, 0.001f, "and what is to its right must be to the right");
            Assert.AreEqual(0f, right.y, 0.001f);
        }

        [Test]
        public void HeadingMatchesAYRotation()
        {
            Assert.AreEqual(0f, MinimapView.Heading(Vector3.forward), 0.001f);
            Assert.AreEqual(90f, MinimapView.Heading(Vector3.right), 0.001f);
            Assert.AreEqual(180f, Mathf.Abs(MinimapView.Heading(Vector3.back)), 0.001f);
            Assert.AreEqual(0f, MinimapView.Heading(Vector3.up), "a vertical vector has no heading");
        }

        [Test]
        public void WindowCutsTheStretchAroundTheCar()
        {
            List<Vector3> square = DenseSquare();
            var window = new List<Vector3>();

            int centre = MinimapView.Window(square, 20, 25f, window);

            Assert.AreEqual(square[20], window[centre]);
            Assert.AreEqual(3, centre, "25 m of 10 m steps reaches three points back");
            Assert.AreEqual(7, window.Count, "and three forwards");
            for (int i = 0; i < window.Count; i++)
                Assert.AreEqual(square[17 + i], window[i], "the stretch stays in driving order");
        }

        [Test]
        public void WindowWrapsOverTheStartOfTheLap()
        {
            List<Vector3> square = DenseSquare();
            var window = new List<Vector3>();

            int centre = MinimapView.Window(square, 1, 15f, window);

            Assert.AreEqual(square[1], window[centre]);
            Assert.AreEqual(square[39], window[0], "the point before the start line comes first");
            Assert.AreEqual(square[0], window[1]);
        }

        [Test]
        public void WindowLongerThanTheTrackStopsAtOneLap()
        {
            List<Vector3> square = DenseSquare();
            var window = new List<Vector3>();

            MinimapView.Window(square, 5, 100000f, window);

            Assert.AreEqual(square.Count, window.Count, "a huge radius must not repeat the loop");
        }

        [Test]
        public void WindowOnAnEmptyPathReportsNoCentre()
        {
            var window = new List<Vector3>();

            Assert.AreEqual(-1, MinimapView.Window(new List<Vector3>(), 0, 50f, window));
            Assert.AreEqual(0, window.Count);
        }
    }
}
