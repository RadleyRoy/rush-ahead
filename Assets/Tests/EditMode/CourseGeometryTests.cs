using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RushAhead.Content;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.Tests
{
    /// <summary>River geometry, gates and currents, plus layout checks that every shipped course must pass.</summary>
    public class CourseGeometryTests
    {
        static readonly RiverPoint[] Straight = { new(0f, 0f, 10f), new(50f, 0f, 10f), new(100f, 0f, 10f) };

        [Test]
        public void AStraightRiver_IsWhereItShouldBe()
        {
            var river = new River(Straight);
            Assert.That(river.Length, Is.EqualTo(100f).Within(0.01f));

            var at = river.At(30f);
            Assert.That(Vector2.Distance(at.Centre, new Vector2(30f, 0f)), Is.LessThan(0.01f));
            Assert.That(Vector2.Distance(at.Tangent, Vector2.right), Is.LessThan(1e-3f));
            Assert.That(at.HalfWidth, Is.EqualTo(5f).Within(1e-3f));
            Assert.That(Vector2.Distance(at.LeftBank, new Vector2(30f, 5f)), Is.LessThan(0.01f), "left looking downstream is +y");

            var p = river.Project(new Vector2(42f, -3f));
            Assert.That(p.Distance, Is.EqualTo(42f).Within(0.01f));
            Assert.That(p.Lateral, Is.EqualTo(-3f).Within(0.01f), "right of the centreline is negative");
        }

        [Test]
        public void ABendingRiver_PassesThroughItsPoints_AndItsBanksMakeOneLoopRoundTheWater()
        {
            var points = new[] { new RiverPoint(0f, 0f, 12f), new RiverPoint(20f, 2f, 10f), new RiverPoint(35f, 15f, 14f), new RiverPoint(40f, 35f, 12f) };
            var river = new River(points);
            foreach (var point in points)
                Assert.That(river.Samples.Min(s => Vector2.Distance(s.Centre, point.position)), Is.LessThan(1e-3f), "the curve goes through every control point");

            var outline = river.Outline();
            var twiceArea = 0f;
            for (var i = 0; i < outline.Length; i++) twiceArea += Cross(outline[i], outline[(i + 1) % outline.Length]);
            Assert.That(twiceArea, Is.GreaterThan(0f), "counter-clockwise, so the cliffs build outward, away from the water");
            Assert.That(river.TightestBend, Is.LessThan(0.9f));
        }

        [Test]
        public void AGate_CountsOnlyACrossingDownstreamBetweenItsPosts()
        {
            var gate = new Gate(new River(Straight).At(40f));
            Assert.That(gate.CrossedBy(new Vector2(39f, 1f), new Vector2(41f, 1f)), Is.True);
            Assert.That(gate.CrossedBy(new Vector2(41f, 1f), new Vector2(39f, 1f)), Is.False, "going back upstream");
            Assert.That(gate.CrossedBy(new Vector2(39f, 9f), new Vector2(41f, 9f)), Is.False, "outside the posts");
            Assert.That(gate.CrossedBy(new Vector2(35f, 0f), new Vector2(38f, 0f)), Is.False, "not there yet");
        }

        [Test]
        public void ACurrent_FlowsDownTheRiver_StrongestMidStream()
        {
            var def = ScriptableObject.CreateInstance<CourseDef>();
            try
            {
                def.river = Straight;
                def.flow = 0f; // the currents alone
                def.currents = new[] { new CurrentZone(20f, 60f, 1f), new CurrentZone(70f, 90f, -0.5f) };
                var course = new Course(def);

                Assert.That(Vector2.Distance(course.WaterVelocityAt(new Vector2(40f, 0f)), Vector2.right), Is.LessThan(1e-3f));
                Assert.That(course.WaterVelocityAt(new Vector2(40f, 4f)).x, Is.InRange(0.2f, 0.5f), "slower near the bank");
                Assert.That(course.WaterVelocityAt(new Vector2(22f, 0f)).x, Is.InRange(0.2f, 0.5f), "eases in at the start of the zone");
                Assert.That(course.WaterVelocityAt(new Vector2(10f, 0f)), Is.EqualTo(Vector2.zero), "still water outside it");
                Assert.That(course.WaterVelocityAt(new Vector2(80f, 0f)).x, Is.EqualTo(-0.5f).Within(1e-3f), "a negative zone pushes upstream");
            }
            finally
            {
                Object.DestroyImmediate(def);
            }
        }

        [Test]
        public void TheRiver_RunsDownstreamEverywhereInTheChannel_AndNotPastItsEdges()
        {
            var def = ScriptableObject.CreateInstance<CourseDef>();
            try
            {
                def.river = Straight;
                def.flow = 1.5f;
                var course = new Course(def);

                Assert.That(course.WaterVelocityAt(new Vector2(10f, 0f)).x, Is.EqualTo(1.5f).Within(1e-3f), "full speed mid-stream");
                Assert.That(course.WaterVelocityAt(new Vector2(10f, 4.5f)).x, Is.InRange(0.75f, 1.5f), "slower toward the edge, still downstream");
                Assert.That(course.WaterVelocityAt(new Vector2(10f, 9f)), Is.EqualTo(Vector2.zero), "nothing past the edge");
                Assert.That(course.IsOffCourse(new Vector2(10f, 4f)), Is.False);
                Assert.That(course.IsOffCourse(new Vector2(10f, 5f + Course.EdgeMargin + 0.1f)), Is.True, "over the edge");
                Assert.That(course.IsOffCourse(new Vector2(-1f, 0f)), Is.False, "the ends are walled, not drops");
            }
            finally
            {
                Object.DestroyImmediate(def);
            }
        }

        static IEnumerable<TestCaseData> ShippedCourses() => CourseAssets.All().Select(c => new TestCaseData(c).SetName($"{c.name} is laid out sensibly"));

        [TestCaseSource(nameof(ShippedCourses))]
        public void EveryCourse_IsLaidOutSensibly(CourseDef def)
        {
            var course = new Course(def);
            var river = course.River;
            const float raftRadius = 1.6f;

            Assert.That(river.TightestBend, Is.LessThan(0.9f), "a bend so tight its inside bank folds back on itself");
            Assert.That(def.gates.Length, Is.GreaterThanOrEqualTo(2), "at least one checkpoint and a finish");
            Assert.That(def.startDistance, Is.LessThan(def.gates[0] - 5f), "the start is upstream of the first gate");
            for (var i = 1; i < def.gates.Length; i++) Assert.That(def.gates[i], Is.GreaterThan(def.gates[i - 1] + 10f), "gates in order, well apart");
            Assert.That(def.FinishDistance, Is.LessThan(river.Length - 5f), "room past the finish line");
            Assert.That(def.threeStarSeconds, Is.LessThan(def.twoStarSeconds).And.LessThan(def.parSeconds));

            foreach (var rock in def.rocks)
            {
                var at = river.Project(rock.position);
                Assert.That(Mathf.Abs(at.Lateral), Is.LessThan(at.HalfWidth), $"rock at {rock.position} is in the water");
                var line = river.At(at.Distance).RacingPoint;
                Assert.That(Vector2.Distance(line, rock.position), Is.GreaterThan(rock.radius + raftRadius + 0.3f),
                    $"rock at {rock.position} sits on the racing line; move it or steer the line round it");
            }

            foreach (var barrel in def.barrels)
            {
                var at = river.Project(barrel);
                Assert.That(Mathf.Abs(at.Lateral), Is.LessThan(at.HalfWidth - 0.5f), $"barrel at {barrel} is in the water");
            }

            foreach (var gate in course.Gates)
                foreach (var rock in def.rocks)
                    Assert.That(Vector2.Distance(gate.Centre, rock.position), Is.GreaterThan(rock.radius + raftRadius + 3f),
                        "a rock right where a capsized raft respawns");

            foreach (var log in def.logs)
            {
                var at = river.Project(log.position);
                Assert.That(Mathf.Abs(at.Lateral) + log.length * 0.5f, Is.LessThan(at.HalfWidth + 1f), $"log at {log.position} is in the water");
            }

            foreach (var pool in def.whirlpools)
            {
                var at = river.Project(pool.position);
                Assert.That(Mathf.Abs(at.Lateral), Is.LessThan(at.HalfWidth - 1f), $"whirlpool at {pool.position} is in the water");
                var line = river.At(at.Distance).RacingPoint;
                Assert.That(Vector2.Distance(line, pool.position), Is.GreaterThan(pool.radius + raftRadius),
                    $"whirlpool at {pool.position} sits on the racing line; steer the line round it");
            }

            Assert.That(def.whirlpools.Length + def.logs.Length + def.barrels.Length, Is.LessThanOrEqualTo(64), "snapshots carry at most 64 loose things");

            // Obstacles: in the water, leaving a way past, and clear of every place a raft starts or comes back.
            var respawns = new List<Vector2>();
            for (var gate = 0; gate < def.gates.Length; gate++) respawns.Add(course.RespawnPose(gate).position);
            void ClearOfRespawns(Vector2 at, float reach, string what)
            {
                foreach (var spot in respawns)
                    Assert.That(Vector2.Distance(spot, at), Is.GreaterThan(reach + raftRadius + 4f), $"{what} right where a raft starts or comes back");
            }

            foreach (var wheel in def.wheels)
            {
                var at = river.Project(wheel.position);
                Assert.That(Mathf.Abs(at.Lateral) + wheel.radius, Is.LessThan(at.HalfWidth + 0.5f), $"wheel at {wheel.position} is in the water");
                Assert.That(at.HalfWidth - Mathf.Abs(at.Lateral) + wheel.radius, Is.LessThan(2f * at.HalfWidth - 2f * raftRadius - 1f),
                    $"wheel at {wheel.position} leaves no way past");
                ClearOfRespawns(wheel.position, wheel.radius, $"wheel at {wheel.position}");
            }

            foreach (var geyser in def.geysers)
            {
                var at = river.Project(geyser.position);
                Assert.That(Mathf.Abs(at.Lateral) + geyser.radius, Is.LessThan(at.HalfWidth), $"geyser at {geyser.position} is in the water");
                ClearOfRespawns(geyser.position, geyser.radius, $"geyser at {geyser.position}");
            }

            foreach (var croc in def.crocodiles)
            {
                Assert.That(croc.reach, Is.InRange(0.1f, 0.5f), "a crocodile lunges part way across, never right over");
                Assert.That(Mathf.Abs(croc.side), Is.EqualTo(1f), "a crocodile waits on one side or the other");
                ClearOfRespawns(river.At(croc.distance).Centre, river.At(croc.distance).HalfWidth, $"crocodile at {croc.distance:0} m");
            }

            foreach (var swing in def.swingers)
            {
                Assert.That(swing.distance, Is.InRange(def.startDistance + 10f, def.FinishDistance - 5f), "a swinging log on the course");
                ClearOfRespawns(river.At(swing.distance).Centre, 2f, $"swinging log at {swing.distance:0} m");
                foreach (var gate in course.Gates)
                    Assert.That(Mathf.Abs(gate.Distance - swing.distance), Is.GreaterThan(6f), "a bridge right on a checkpoint");
            }

            // The river never comes back near itself, or one stretch's banks would stand in another's water.
            var samples = river.Samples;
            for (var i = 0; i < samples.Count; i += 4)
            for (var j = i + 4; j < samples.Count; j += 4)
            {
                if (samples[j].Distance - samples[i].Distance < 60f) continue;
                var gap = Vector2.Distance(samples[i].Centre, samples[j].Centre) - samples[i].HalfWidth - samples[j].HalfWidth;
                Assert.That(gap, Is.GreaterThan(12f), $"the river passes within {gap:0} m of itself at {samples[i].Distance:0} m and {samples[j].Distance:0} m");
            }
        }

        [Test]
        public void AWhirlpool_CarriesWaterRoundAndIn_AndIsStillAtItsMiddleAndRim()
        {
            var def = ScriptableObject.CreateInstance<CourseDef>();
            try
            {
                def.river = new[] { new RiverPoint(0f, 0f, 30f), new RiverPoint(60f, 0f, 30f) };
                def.flow = 0f; // the whirlpool alone
                def.gates = new[] { 30f, 50f };
                def.whirlpools = new[] { new WhirlpoolSpot(new Vector2(30f, 0f), 6f, 2f, 1f) };
                var course = new Course(def);

                var east = course.WaterVelocityAt(new Vector2(33f, 0f)); // halfway out, east of the middle
                Assert.That(east.y, Is.EqualTo(2f).Within(0.01f), "anticlockwise: north on the east side");
                Assert.That(east.x, Is.EqualTo(-1f).Within(0.01f), "and drawn in toward the middle");
                Assert.That(course.WaterVelocityAt(new Vector2(30f, 0f)).magnitude, Is.LessThan(0.01f), "still at the middle");
                Assert.That(course.WaterVelocityAt(new Vector2(36.5f, 0f)).magnitude, Is.LessThan(0.01f), "and beyond the rim");
            }
            finally
            {
                Object.DestroyImmediate(def);
            }
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }

    static class CourseAssets
    {
        /// <summary>Every course in the project, found through the asset database (tests only ever run in the editor).</summary>
        public static IEnumerable<CourseDef> All()
        {
#if UNITY_EDITOR
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:CourseDef", new[] { "Assets/_Project/Content" }))
                yield return UnityEditor.AssetDatabase.LoadAssetAtPath<CourseDef>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
#else
            yield break;
#endif
        }
    }
}
