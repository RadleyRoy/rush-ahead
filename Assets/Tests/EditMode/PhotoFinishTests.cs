using NUnit.Framework;
using RushAhead.Render;
using RushAhead.UI;
using UnityEngine;

namespace RushAhead.Tests
{
    /// <summary>When a finish is close enough for the slow-motion photo finish, and the recording it plays back.</summary>
    public class PhotoFinishTests
    {
        [Test]
        public void ATeamRace_IsAPhotoFinish_WhenTheSecondRaftIsWithinSixTenths()
        {
            Assert.That(PhotoFinishRule.Margin(6000, 6005, 60), Is.EqualTo(5f / 60f).Within(1e-5f));
            Assert.That(PhotoFinishRule.Margin(6000, 6036, 60), Is.EqualTo(0.6f).Within(1e-5f), "six tenths is still close");
            Assert.That(PhotoFinishRule.Margin(6000, 6037, 60), Is.Null, "any more isn't");
            Assert.That(PhotoFinishRule.Margin(6000, 0, 60), Is.Null, "the other raft never finished");
        }

        [Test]
        public void AnEliminationFinal_IsAPhotoFinish_WhenTheRunnerUpWasThatClose()
        {
            Assert.That(PhotoFinishRule.Margin(1.2f, 12f), Is.EqualTo(0.1f).Within(1e-5f), "1.2 m short at 12 m/s");
            Assert.That(PhotoFinishRule.Margin(9f, 12f), Is.Null, "three quarters of a second back");
            Assert.That(PhotoFinishRule.Margin(1f, 0.2f), Is.Null, "a raft that's stopped isn't racing for the line");
        }

        sealed class Still : RushAhead.Sim.IBoatState, RushAhead.Sim.ICrewState
        {
            public Vector2 At;
            public int Index => 0;
            public Vector2 RenderPosition => At;
            public float RenderAngleRad => 0f;
            public Vector2 Velocity => Vector2.right;
            public Vector2 Tilt => Vector2.zero;
            public bool Capsized => false;
            public int CapsizeCount => 0;
            public int TeleportCount => 0;
            public int GatesPassed => 0;
            public int FinishTick => 0;
            public bool Fallen => false;
            public int Lives => 3;
            public bool Out => false;
            public float TiltDanger => 0f;
            public RushAhead.Sim.PowerUp Item => RushAhead.Sim.PowerUp.None;
            public bool Turbo => false;
            public bool Shielded => false;
            public int ShieldPops => 0;
            public int HornCount => 0;
            public int Pickups => 0;
            public int CrewCount => 1;
            public RushAhead.Sim.ICrewState CrewAt(int seat) => this;
            public int Seat => 0;
            public RushAhead.Content.BlowerDef Blower => null;
            public float AimAngle => 0f;
            public float Spool => 0.5f;
            public bool IsBlowing => true;
            public float AirFraction => 1f;
            public bool Braced => false;
            public int BurstCount => 0;
        }

        [Test]
        public void TheRecording_KeepsTheLastFourSeconds_AndPlaysThemBack()
        {
            var raft = new Still();
            var recording = new RaftRecording();
            for (var frame = 0; frame <= 600; frame++)
            {
                raft.At = new Vector2(frame / 60f * 10f, 0f); // 10 m/s for ten seconds, drawn at 60 frames a second
                recording.Record(raft, frame / 60.0);
            }

            Assert.That(recording.Count, Is.LessThanOrEqualTo(Mathf.CeilToInt(RaftRecording.Rate * RaftRecording.Seconds) + 2), "thirty a second, for four seconds");
            Assert.That(recording[0].Clock, Is.GreaterThan(5.8), "the oldest is about four seconds old");
            Assert.That(recording.PositionAt(8.25).x, Is.EqualTo(82.5f).Within(0.05f), "in between two poses");

            var replay = new ReplayBoat(raft, recording);
            replay.Show(7.0);
            Assert.That(replay.RenderPosition.x, Is.EqualTo(70f).Within(0.05f));
            Assert.That(replay.TeleportCount, Is.Not.EqualTo(raft.TeleportCount), "the view cuts to the replay rather than seeing a bump");

            recording.Record(raft, 1.0);
            Assert.That(recording.Count, Is.EqualTo(1), "a clock that went back is a new race");
        }
    }
}
