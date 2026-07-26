using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Gmtk2026.GameBalance.Tests
{
    public class TrackProgressTests
    {
        /// <summary>100 m square loop on the XZ plane: total length 400 m, start at the origin.</summary>
        private static TrackProgress Square(int searchWindowSegments = 12,
            float relocateDistanceMetres = 30f)
        {
            var points = new List<Vector3>
            {
                new(0f, 0f, 0f),
                new(100f, 0f, 0f),
                new(100f, 0f, 100f),
                new(0f, 0f, 100f)
            };

            return new TrackProgress(points, searchWindowSegments, relocateDistanceMetres);
        }

        [Test]
        public void ClosingSegmentIsPartOfTheLength()
        {
            Assert.AreEqual(400f, Square().Length, 0.001f);
            Assert.AreEqual(4, Square().SegmentCount);
        }

        [Test]
        public void ProgressIsDistanceAlongThePath()
        {
            TrackProgress track = Square();
            var cursor = new TrackProgress.CarCursor();

            track.Advance(ref cursor, new Vector3(50f, 0f, 0f));
            Assert.AreEqual(50f, cursor.progressMetres, 0.001f);

            track.Advance(ref cursor, new Vector3(100f, 0f, 50f));
            Assert.AreEqual(150f, cursor.progressMetres, 0.001f);

            track.Advance(ref cursor, new Vector3(50f, 0f, 100f));
            Assert.AreEqual(250f, cursor.progressMetres, 0.001f);
            Assert.AreEqual(0, cursor.lap);
        }

        [Test]
        public void CarsOffTheCentreLineStillProjectOntoIt()
        {
            TrackProgress track = Square();
            var cursor = new TrackProgress.CarCursor();

            // 4 m to the side and 2 m up: a car in the outer lane on a crest
            track.Advance(ref cursor, new Vector3(50f, 2f, 4f));

            Assert.AreEqual(50f, cursor.progressMetres, 0.001f,
                "sideways and vertical offset must not change how far along the car is");
        }

        [Test]
        public void PassingTheStartLineForwardCountsALap()
        {
            TrackProgress track = Square();
            var cursor = new TrackProgress.CarCursor();

            track.Advance(ref cursor, new Vector3(0f, 0f, 50f));   // 350 m, last segment
            Assert.AreEqual(0, cursor.lap);

            track.Advance(ref cursor, new Vector3(10f, 0f, 0f));   // wrapped past the start
            Assert.AreEqual(1, cursor.lap);
            Assert.AreEqual(10f, cursor.progressMetres, 0.001f);
            Assert.AreEqual(410d, track.TotalDistance(cursor), 0.001d);
        }

        [Test]
        public void ReversingOverTheStartLineDoesNotAddALap()
        {
            TrackProgress track = Square();
            var cursor = new TrackProgress.CarCursor();

            track.Advance(ref cursor, new Vector3(0f, 0f, 50f));
            track.Advance(ref cursor, new Vector3(10f, 0f, 0f));   // lap 1
            track.Advance(ref cursor, new Vector3(0f, 0f, 50f));   // spun round, back over the line
            Assert.AreEqual(0, cursor.lap, "driving backwards over the line must undo the lap");

            track.Advance(ref cursor, new Vector3(10f, 0f, 0f));   // forwards again
            Assert.AreEqual(1, cursor.lap, "and the same lap must not be counted twice");
        }

        [Test]
        public void LapNeverGoesNegative()
        {
            TrackProgress track = Square();
            var cursor = new TrackProgress.CarCursor();

            track.Advance(ref cursor, new Vector3(10f, 0f, 0f));
            track.Advance(ref cursor, new Vector3(0f, 0f, 50f));   // reversed off the grid

            Assert.AreEqual(0, cursor.lap);
            Assert.GreaterOrEqual(track.TotalDistance(cursor), 0d);
        }

        [Test]
        public void TotalDistanceOrdersCarsAcrossLaps()
        {
            TrackProgress track = Square();
            var behind = new TrackProgress.CarCursor { placed = true, lap = 0, progressMetres = 399f };
            var ahead = new TrackProgress.CarCursor { placed = true, lap = 1, progressMetres = 0f };

            Assert.Less(track.TotalDistance(behind), track.TotalDistance(ahead),
                "a car that has just started its second lap leads one about to finish its first");
        }

        [Test]
        public void LocalSearchKeepsACarOnItsOwnSideOfAHairpin()
        {
            // out along z at x=0, back along z at x=6: the two straights are 6 m apart but 200 m
            // apart along the path, which is what a global nearest-point search gets wrong.
            var points = new List<Vector3>
            {
                new(0f, 0f, 0f),
                new(0f, 0f, 100f),
                new(6f, 0f, 100f),
                new(6f, 0f, 0f)
            };
            var track = new TrackProgress(points, 1);
            var cursor = new TrackProgress.CarCursor();

            track.Advance(ref cursor, new Vector3(0f, 0f, 40f));   // outbound straight
            Assert.AreEqual(40f, cursor.progressMetres, 0.001f);

            // drifted to the middle, marginally closer to the return straight
            track.Advance(ref cursor, new Vector3(3.2f, 0f, 50f));

            Assert.AreEqual(0, cursor.segment, "the car must stay on the straight it was driving");
            Assert.AreEqual(50f, cursor.progressMetres, 0.001f);
        }

        [Test]
        public void ATeleportedCarIsFoundAgainWithoutLosingItsLap()
        {
            TrackProgress track = Square(searchWindowSegments: 1, relocateDistanceMetres: 20f);
            var cursor = new TrackProgress.CarCursor();

            track.Advance(ref cursor, new Vector3(50f, 0f, 0f));
            track.Advance(ref cursor, new Vector3(0f, 0f, 50f));   // wrapped backwards, lap stays 0
            track.Advance(ref cursor, new Vector3(10f, 0f, 0f));   // lap 1
            Assert.AreEqual(1, cursor.lap);

            // respawned onto the opposite side of the loop: with a one-segment window that point is
            // 50 m from anything the window can see, and a half-lap jump would look like a wrap
            track.Advance(ref cursor, new Vector3(50f, 0f, 100f));

            Assert.AreEqual(250f, cursor.progressMetres, 0.001f, "the car must be relocated, not clamped");
            Assert.AreEqual(1, cursor.lap, "a respawn must neither award nor steal a lap");
        }

        [Test]
        public void AGridBehindTheStartLineDoesNotEarnAPhantomLap()
        {
            TrackProgress track = Square();
            var cursor = new TrackProgress.CarCursor();

            // the starting grid sits behind the line, so the first crossing is not a completed lap
            track.Advance(ref cursor, new Vector3(0f, 0f, 10f));   // 390 m: last segment, 10 m to go
            TrackProgress.CarCursor start = cursor;

            track.Advance(ref cursor, new Vector3(10f, 0f, 0f));   // 10 m past the line

            Assert.AreEqual(1, cursor.lap, "the wrap itself is still a lap of the path");
            Assert.AreEqual(20d, track.DistanceDriven(start, cursor), 0.001d,
                "but the car has only driven the 20 m between the two points");
        }

        [Test]
        public void DistanceDrivenRanksCarsThatStartedOnDifferentGridSlots()
        {
            TrackProgress track = Square();
            var pole = new TrackProgress.CarCursor();
            var back = new TrackProgress.CarCursor();

            track.Advance(ref pole, new Vector3(0f, 0f, 5f));      // 395 m: front row
            TrackProgress.CarCursor poleStart = pole;
            track.Advance(ref back, new Vector3(0f, 0f, 15f));     // 385 m: back row
            TrackProgress.CarCursor backStart = back;

            track.Advance(ref pole, new Vector3(10f, 0f, 0f));     // drove 15 m
            track.Advance(ref back, new Vector3(30f, 0f, 0f));     // drove 45 m

            Assert.Greater(track.DistanceDriven(backStart, back), track.DistanceDriven(poleStart, pole),
                "the car that covered more ground leads, whatever slot it started from");
            Assert.AreEqual(15d, track.DistanceDriven(poleStart, pole), 0.001d);
            Assert.AreEqual(45d, track.DistanceDriven(backStart, back), 0.001d);
        }

        [Test]
        public void ReversingOffTheGridDrivesANegativeDistance()
        {
            TrackProgress track = Square();
            var cursor = new TrackProgress.CarCursor();

            track.Advance(ref cursor, new Vector3(50f, 0f, 0f));
            TrackProgress.CarCursor start = cursor;
            track.Advance(ref cursor, new Vector3(30f, 0f, 0f));

            Assert.AreEqual(-20d, track.DistanceDriven(start, cursor), 0.001d,
                "a car pointing the wrong way must rank behind one that has not moved");
        }

        [Test]
        public void AnUnplacedCursorHasDrivenNothing()
        {
            TrackProgress track = Square();
            var unplaced = new TrackProgress.CarCursor();
            var placed = new TrackProgress.CarCursor();
            track.Advance(ref placed, new Vector3(50f, 0f, 0f));

            Assert.AreEqual(0d, track.DistanceDriven(unplaced, placed));
            Assert.AreEqual(0d, track.DistanceDriven(placed, unplaced));
        }

        [Test]
        public void AnEmptyPathIsUnusableAndInert()
        {
            var empty = new TrackProgress(new List<Vector3>());
            var single = new TrackProgress(new List<Vector3> { Vector3.zero });
            var cursor = new TrackProgress.CarCursor();

            Assert.IsFalse(empty.IsUsable);
            Assert.IsFalse(single.IsUsable);

            single.Advance(ref cursor, new Vector3(5f, 0f, 5f));

            Assert.IsFalse(cursor.placed, "an unusable path must not pretend to place cars");
            Assert.AreEqual(0d, single.TotalDistance(cursor));
        }
    }
}
