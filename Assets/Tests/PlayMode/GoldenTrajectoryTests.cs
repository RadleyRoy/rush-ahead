using System.Globalization;
using NUnit.Framework;
using RushAhead.Content;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.Tests
{
    /// <summary>
    /// Freezes the tuned feel. A fixed input script must put the raft at the same poses as when the tuning was
    /// signed off. If this fails after a deliberate tuning change, re-record the expected values from the failure
    /// message; if it fails after anything else, something changed how the raft handles.
    /// </summary>
    public class GoldenTrajectoryTests
    {
        const float Dt = 1f / 60f;
        const float Astern = Mathf.PI;
        const float TowardStarboard = -Mathf.PI / 2f;

        // Recorded from the default SimConfig and leaf blower, 22 Oct 2026 (tuning signed off after phone play).
        static readonly (int tick, float x, float y, float headingDeg, float lean)[] Expected =
        {
            (60, 0.4680f, 0.0000f, 0.000f, 0.0722f),
            (180, 4.7028f, -0.0030f, -6.216f, 0.0249f),
            (300, 9.5858f, -0.3056f, -56.062f, 0.1599f),
            (360, 10.7689f, 0.2610f, -83.148f, 0.0955f),
        };

        sealed class Script : ICrewInputSource
        {
            public CrewInput Input;
            public CrewInput ReadInput() => Input;
        }

        [Test]
        public void TheTunedRaft_FollowsItsRecordedPath()
        {
            var previousMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            var config = ScriptableObject.CreateInstance<SimConfig>();
            var blower = ScriptableObject.CreateInstance<BlowerDef>();
            var world = new SimWorld(config);

            try
            {
                var boat = world.SpawnBoat(Vector2.zero, 0f, new[] { blower, blower, blower, blower });
                var seats = new Script[4];
                for (var i = 0; i < seats.Length; i++) boat.Crew[i].Input = seats[i] = new Script();

                var actual = new (int tick, float x, float y, float headingDeg, float lean)[Expected.Length];
                var next = 0;
                for (var tick = 1; tick <= 360; tick++)
                {
                    for (var i = 0; i < seats.Length; i++) seats[i].Input = CrewInput.Idle;
                    if (tick <= 120)
                    {
                        Drive(seats[2], Astern); // both rear seats: straight ahead
                        Drive(seats[3], Astern);
                    }
                    else if (tick <= 240)
                    {
                        Drive(seats[2], Astern); // rear-left alone: a turn
                    }
                    else
                    {
                        for (var i = 0; i < seats.Length; i++) Drive(seats[i], TowardStarboard); // everyone shoves to port
                    }

                    world.Advance(Dt);
                    if (next < Expected.Length && tick == Expected[next].tick)
                        actual[next++] = (tick, boat.Body.Position.x, boat.Body.Position.y, boat.Body.AngleRad * Mathf.Rad2Deg, boat.Tilt.Tilt.magnitude);
                }

                var recording = string.Join("\n", System.Array.ConvertAll(actual, a => string.Format(CultureInfo.InvariantCulture,
                    "            ({0}, {1:0.0000}f, {2:0.0000}f, {3:0.000}f, {4:0.0000}f),", a.tick, a.x, a.y, a.headingDeg, a.lean)));

                for (var i = 0; i < Expected.Length; i++)
                {
                    var (tick, x, y, heading, lean) = Expected[i];
                    var got = actual[i];
                    var close = Mathf.Abs(got.x - x) < 0.01f && Mathf.Abs(got.y - y) < 0.01f &&
                                Mathf.Abs(Mathf.DeltaAngle(got.headingDeg, heading)) < 0.2f && Mathf.Abs(got.lean - lean) < 0.002f;
                    Assert.That(close, Is.True, $"Pose at tick {tick} moved. If the tuning change was deliberate, re-record:\n{recording}");
                }
            }
            finally
            {
                world.DestroyAll();
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(blower);
                Physics2D.simulationMode = previousMode;
            }
        }

        static void Drive(Script seat, float airAngle) =>
            seat.Input = new CrewInput { AimAngle = airAngle, HasAim = true, Throttle = 1f };
    }
}
