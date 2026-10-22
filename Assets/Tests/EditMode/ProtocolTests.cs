using System;
using System.Linq;
using NUnit.Framework;
using RushAhead.Content;
using RushAhead.Net;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.Tests
{
    public class ProtocolTests
    {
        static Snapshot SampleSnapshot()
        {
            var s = new Snapshot { Tick = 1234 };
            var boat = new BoatSnapshot
            {
                Position = new Vector2(-22.5f, 3.25f),
                AngleRad = 1.25f,
                Velocity = new Vector2(2.5f, -0.75f),
                AngularVelocity = -0.5f,
                Tilt = new Vector2(0.12f, -0.03f),
                Capsized = false,
                CapsizeCount = 2,
                GatesPassed = 3,
                FinishTick = 0x1234,
                TeleportCount = 5,
            };
            for (var i = 0; i < 4; i++)
                boat.Crew.Add(new CrewSnapshot { LocalAimDeg = 45f + 90f * i - 180f, Spool = 0.25f * i, Air = 1f - 0.2f * i, Braced = i == 1, BurstCount = (byte)(i * 3), BlowerIndex = (byte)i });
            s.Boats.Add(boat);
            s.Props.Add(new PropSnapshot { Position = new Vector2(-15f, -2.7f), AngleRad = 0.5f });
            s.Props.Add(new PropSnapshot { Position = new Vector2(-13.2f, -1.8f), AngleRad = 5f });
            return s;
        }

        static readonly InputMessage SampleInput = new()
        {
            Seat = 2, Sequence = 65534, AimAngle = Mathf.PI, HasAim = true, Throttle = 0.75f, Brace = false, BurstCount = 7,
        };

        static byte[] Encode(Action<ByteWriter> write)
        {
            var writer = new ByteWriter();
            write(writer);
            return writer.ToArray();
        }

        static string Hex(byte[] bytes) => string.Concat(bytes.Select(b => b.ToString("x2")));

        [Test]
        public void Input_RoundTrips()
        {
            var bytes = Encode(w => Protocol.Write(w, SampleInput));
            var read = Protocol.ReadInput(new ByteReader(new ArraySegment<byte>(bytes)));

            Assert.That(read.Seat, Is.EqualTo(2));
            Assert.That(read.Sequence, Is.EqualTo(65534));
            Assert.That(read.AimAngle, Is.EqualTo(Mathf.PI).Within(1e-3f));
            Assert.That(read.HasAim, Is.True);
            Assert.That(read.Brace, Is.False);
            Assert.That(read.Throttle, Is.EqualTo(0.75f).Within(1f / 255f));
            Assert.That(read.BurstCount, Is.EqualTo(7));
        }

        [Test]
        public void Snapshot_RoundTripsWithinQuantisation()
        {
            var original = SampleSnapshot();
            var bytes = Encode(w => Protocol.Write(w, original));
            var read = new Snapshot();
            Protocol.ReadSnapshot(new ByteReader(new ArraySegment<byte>(bytes)), read);

            Assert.That(read.Tick, Is.EqualTo(1234u));
            var a = original.Boats[0];
            var b = read.Boats[0];
            Assert.That(Vector2.Distance(a.Position, b.Position), Is.LessThan(0.005f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(a.AngleRad * Mathf.Rad2Deg, b.AngleRad * Mathf.Rad2Deg)), Is.LessThan(0.01f));
            Assert.That(Vector2.Distance(a.Velocity, b.Velocity), Is.LessThan(0.005f));
            Assert.That(b.AngularVelocity, Is.EqualTo(a.AngularVelocity).Within(0.002f));
            Assert.That(Vector2.Distance(a.Tilt, b.Tilt), Is.LessThan(0.0005f));
            Assert.That(b.CapsizeCount, Is.EqualTo(2));
            Assert.That(b.GatesPassed, Is.EqualTo(3));
            Assert.That(b.FinishTick, Is.EqualTo(0x1234u));
            Assert.That(b.TeleportCount, Is.EqualTo(5));
            Assert.That(b.Crew.Count, Is.EqualTo(4));
            for (var i = 0; i < 4; i++)
            {
                Assert.That(Mathf.DeltaAngle(a.Crew[i].LocalAimDeg, b.Crew[i].LocalAimDeg), Is.EqualTo(0f).Within(0.01f));
                Assert.That(b.Crew[i].Spool, Is.EqualTo(a.Crew[i].Spool).Within(1f / 255f));
                Assert.That(b.Crew[i].Braced, Is.EqualTo(a.Crew[i].Braced));
                Assert.That(b.Crew[i].BurstCount, Is.EqualTo(a.Crew[i].BurstCount));
                Assert.That(b.Crew[i].BlowerIndex, Is.EqualTo(a.Crew[i].BlowerIndex));
            }

            Assert.That(read.Props.Count, Is.EqualTo(2));
            Assert.That(Vector2.Distance(read.Props[1].Position, original.Props[1].Position), Is.LessThan(0.005f));
            Assert.That(bytes.Length, Is.LessThan(100), "a 4-crew snapshot with two props should stay well under 100 bytes");
        }

        static RosterSeat[] SampleRoster() => new[]
        {
            new RosterSeat { Human = true, Host = true, Loadout = new Loadout { Species = 0, Fur = 0, Blower = 0, Name = "Skipper" } },
            new RosterSeat { Human = true, Yours = true, Loadout = new Loadout { Species = 4, Fur = 9, Blower = 1, Name = "Ötter" } },
            new RosterSeat { Ai = true, Loadout = new Loadout { Species = 2, Fur = 2, Blower = 3, Name = "" } },
        };

        static readonly Loadout SampleProfile = new() { Species = 5, Fur = 7, Blower = 4, Name = "Radley" };
        const ulong SampleDevice = 0x0123456789ABCDEF;

        static readonly RoomAdvert SampleAdvert = new()
        {
            RoomId = 0xDEADBEEF, Version = Protocol.Version, GamePort = 7777, Players = 2, Seats = 4, HostName = "Skipper",
        };

        [Test]
        public void Roster_RoundTrips_AndRejectsAnotherProtocolVersion()
        {
            var bytes = Encode(w => Protocol.Write(w, SampleRoster()));
            var read = Protocol.ReadRoster(new ByteReader(new ArraySegment<byte>(bytes)));
            Assert.That(read, Is.EqualTo(SampleRoster()));

            bytes[1] = Protocol.Version + 1;
            var e = Assert.Throws<VersionMismatchException>(() => Protocol.ReadRoster(new ByteReader(new ArraySegment<byte>(bytes))));
            Assert.That(e.Message, Does.Contain("same version"), "the player is told what to do about it");
        }

        [Test]
        public void Race_RoundTrips()
        {
            var bytes = Encode(w => Protocol.Write(w, new RaceMessage { Course = 1, StartTick = 987654 }));
            var read = Protocol.ReadRace(new ByteReader(new ArraySegment<byte>(bytes)));
            Assert.That(read.Course, Is.EqualTo(1));
            Assert.That(read.StartTick, Is.EqualTo(987654u));
        }

        [Test]
        public void ATeamRaceRoster_CarriesBothRafts_AndOrdersSayWhichRaft()
        {
            var seats = SampleRoster().Concat(SampleRoster()).ToArray(); // two rafts of three
            var bytes = Encode(w => Protocol.Write(w, seats, rafts: 2));
            var read = Protocol.ReadRoster(new ByteReader(new ArraySegment<byte>(bytes)), out var rafts);
            Assert.That(rafts, Is.EqualTo(2));
            Assert.That(read, Is.EqualTo(seats));

            var order = Encode(w => Protocol.Write(w, new OrderMessage { Order = CrewOrder.Hold, Raft = 1 }));
            Assert.That(Protocol.ReadOrder(new ByteReader(new ArraySegment<byte>(order))).Raft, Is.EqualTo(1));
            order[2] = 5;
            Assert.Throws<ProtocolException>(() => Protocol.ReadOrder(new ByteReader(new ArraySegment<byte>(order))), "there's no sixth raft");
        }

        [Test]
        public void ProfileAndSeatClaim_RoundTrip()
        {
            var profile = Encode(w => Protocol.WriteProfile(w, SampleProfile, SampleDevice));
            Assert.That(Protocol.ReadProfile(new ByteReader(new ArraySegment<byte>(profile)), out var device), Is.EqualTo(SampleProfile));
            Assert.That(device, Is.EqualTo(SampleDevice), "the id a host recognises a returning player by");

            var order = Encode(w => Protocol.Write(w, new OrderMessage { Order = CrewOrder.PushHere, Point = new Vector2(10.5f, -2.25f) }));
            var readOrder = Protocol.ReadOrder(new ByteReader(new ArraySegment<byte>(order)));
            Assert.That(readOrder.Order, Is.EqualTo(CrewOrder.PushHere));
            Assert.That(readOrder.Point, Is.EqualTo(new Vector2(10.5f, -2.25f)));
            order[1] = 9;
            Assert.Throws<ProtocolException>(() => Protocol.ReadOrder(new ByteReader(new ArraySegment<byte>(order))), "no such order");

            var claim = Encode(w => Protocol.WriteClaimSeat(w, 3));
            Assert.That(Protocol.ReadClaimSeat(new ByteReader(new ArraySegment<byte>(claim))), Is.EqualTo(3));
        }

        [Test]
        public void Names_AreCleanedOnTheWay_AndAnOverlongOneIsRefused()
        {
            var messy = new Loadout { Name = "  Bob\n\tthe\u0007Great Skipper of the Lagoon  " };
            var bytes = Encode(w => Protocol.WriteProfile(w, messy, 1));
            var read = Protocol.ReadProfile(new ByteReader(new ArraySegment<byte>(bytes)), out _);
            Assert.That(read.Name, Is.EqualTo("BobtheGreat"), "control characters dropped, trimmed, at most 12 characters");

            // A hand-made profile claiming a 200-byte name must not be believed.
            var hostile = new byte[] { (byte)MessageType.Profile, Protocol.Version, 0, 0, 0, 200 }.Concat(new byte[200]).ToArray();
            Assert.Throws<ProtocolException>(() => Protocol.ReadProfile(new ByteReader(new ArraySegment<byte>(hostile)), out _));
        }

        [Test]
        public void Advert_RoundTrips_AndIgnoresStrangers()
        {
            var bytes = Encode(w => Protocol.Write(w, SampleAdvert));
            Assert.That(Protocol.TryReadAdvert(new ArraySegment<byte>(bytes), out var read), Is.True);
            Assert.That(read.RoomId, Is.EqualTo(0xDEADBEEF));
            Assert.That(read.GamePort, Is.EqualTo(7777));
            Assert.That(read.Players, Is.EqualTo(2));
            Assert.That(read.Seats, Is.EqualTo(4));
            Assert.That(read.HostName, Is.EqualTo("Skipper"));
            Assert.That(read.Compatible, Is.True);

            // Other apps' traffic on the same port, truncations and trailing junk are all just "not an advert".
            Assert.That(Protocol.TryReadAdvert(new ArraySegment<byte>(System.Text.Encoding.ASCII.GetBytes("M-SEARCH * HTTP/1.1")), out _), Is.False);
            for (var length = 0; length < bytes.Length; length++)
                Assert.That(Protocol.TryReadAdvert(new ArraySegment<byte>(bytes, 0, length), out _), Is.False, $"truncated to {length}");
            Assert.That(Protocol.TryReadAdvert(new ArraySegment<byte>(bytes.Concat(new byte[] { 1 }).ToArray()), out _), Is.False);

            // A room from another version is still recognised, so it can be listed as needing an update.
            var newer = (byte[])bytes.Clone();
            newer[4] = Protocol.Version + 1;
            Assert.That(Protocol.TryReadAdvert(new ArraySegment<byte>(newer), out var other), Is.True);
            Assert.That(other.Compatible, Is.False);
        }

        // Committed wire bytes: if these change, the protocol changed. Bump Protocol.Version and re-record.
        const string InputGolden = "2002feff0080bf0107";
        const string SnapshotGolden = "10d20400000180e9ffff40030000ee32800240ff00ffec0185ff00020334120000050400a000ff00000000e040cc010301002080990006020060bf660009030200f1ffff4dfdffff5f14cdf2ffff33feffffb8cb";
        const string RosterGolden = "020501030300000007536b69707065720504090106c396747465720802020300";
        const string ProfileGolden = "2105050704065261646c6579efcdab8967452301";
        const string AdvertGolden = "5255534805efbeadde611e020407536b6970706572";
        const string OrderGolden = "040300800a0000c0fdffff";
        const string RaceGolden = "0302d2040000";

        [Test]
        public void WireFormat_MatchesTheCommittedBytes()
        {
            var input = Hex(Encode(w => Protocol.Write(w, SampleInput)));
            var snapshot = Hex(Encode(w => Protocol.Write(w, SampleSnapshot())));
            var roster = Hex(Encode(w => Protocol.Write(w, SampleRoster())));
            var profile = Hex(Encode(w => Protocol.WriteProfile(w, SampleProfile, SampleDevice)));
            var order = Hex(Encode(w => Protocol.Write(w, new OrderMessage { Order = CrewOrder.PushHere, Point = new Vector2(10.5f, -2.25f) })));
            var advert = Hex(Encode(w => Protocol.Write(w, SampleAdvert)));
            var race = Hex(Encode(w => Protocol.Write(w, new RaceMessage { Course = 2, StartTick = 1234 })));
            var recording = $"InputGolden = \"{input}\";\nSnapshotGolden = \"{snapshot}\";\nRosterGolden = \"{roster}\";\n" +
                            $"ProfileGolden = \"{profile}\";\nAdvertGolden = \"{advert}\";\nRaceGolden = \"{race}\";\nOrderGolden = \"{order}\";";
            Assert.That(input == InputGolden && snapshot == SnapshotGolden && roster == RosterGolden && profile == ProfileGolden && advert == AdvertGolden && race == RaceGolden && order == OrderGolden,
                Is.True, $"Wire bytes changed. If deliberate, bump Protocol.Version and re-record:\n{recording}");
        }

        [Test]
        public void EveryTruncatedSnapshot_FailsCleanly()
        {
            var bytes = Encode(w => Protocol.Write(w, SampleSnapshot()));
            for (var length = 0; length < bytes.Length; length++)
            {
                var cut = new ArraySegment<byte>(bytes, 0, length);
                Assert.Throws<ProtocolException>(() => Protocol.ReadSnapshot(new ByteReader(cut), new Snapshot()), $"truncated to {length} bytes");
            }
        }

        [Test]
        public void RandomGarbage_NeverEscapesAsAnythingButProtocolException()
        {
            var random = new System.Random(99);
            for (var i = 0; i < 2000; i++)
            {
                var bytes = new byte[random.Next(0, 120)];
                random.NextBytes(bytes);
                if (bytes.Length > 0) bytes[0] = (byte)(random.Next(7) switch { 0 => 0x02, 1 => 0x10, 2 => 0x20, 3 => 0x21, 4 => 0x03, 5 => 0x04, _ => 0x22 }); // aim at real decoders
                if (bytes.Length > 1 && random.Next(2) == 0) bytes[1] = Protocol.Version; // and get past the version check half the time
                var message = new ArraySegment<byte>(bytes);
                Protocol.TryReadAdvert(message, out _); // must never throw at all
                try
                {
                    switch (bytes.Length == 0 ? 0 : bytes[0])
                    {
                        case 0x02: Protocol.ReadRoster(new ByteReader(message)); break;
                        case 0x03: Protocol.ReadRace(new ByteReader(message)); break;
                        case 0x04: Protocol.ReadOrder(new ByteReader(message)); break;
                        case 0x10: Protocol.ReadSnapshot(new ByteReader(message), new Snapshot()); break;
                        case 0x20: Protocol.ReadInput(new ByteReader(message)); break;
                        case 0x21: Protocol.ReadProfile(new ByteReader(message), out _); break;
                        case 0x22: Protocol.ReadClaimSeat(new ByteReader(message)); break;
                        default: Protocol.PeekType(message); break;
                    }
                }
                catch (ProtocolException)
                {
                    // expected for most garbage
                }
            }
        }

        [Test]
        public void Quantise_WrapsAnglesAndClampsRanges()
        {
            Assert.That(Quantize.Angle(Quantize.Angle(-0.5f)), Is.EqualTo(2f * Mathf.PI - 0.5f).Within(1e-3f));
            Assert.That(Quantize.Angle(Quantize.Angle(2f * Mathf.PI)), Is.EqualTo(0f).Within(1e-3f));
            Assert.That(Quantize.Unit(Quantize.Unit(2f)), Is.EqualTo(1f));
            Assert.That(Quantize.Velocity(Quantize.Velocity(1000f)), Is.EqualTo(short.MaxValue / 256f).Within(1e-3f), "clamped, not wrapped");
        }
    }

    public class NetSessionUnitTests
    {
        [Test]
        public void Timeline_SettlesTheConfiguredDelayBehindTheNewestSnapshot()
        {
            var timeline = new SnapshotTimeline(tickRate: 60f, snapshotEveryTicks: 2);
            for (uint tick = 0; tick <= 600; tick += 2)
            {
                timeline.Advance(2f / 60f);
                var s = timeline.Rent();
                s.Tick = tick;
                timeline.Add(s);
            }

            // Measured just as a snapshot lands: playback then sits the full delay behind it, and drifts closer until the next arrives.
            Assert.That(timeline.NewestTick - timeline.PlaybackTick, Is.EqualTo(timeline.DelayTicks).Within(0.5));
        }

        [Test]
        public void Timeline_IsNotReady_UntilPlaybackReachesTheFirstSnapshot()
        {
            var timeline = new SnapshotTimeline(60f, 2);
            Assert.That(timeline.Ready, Is.False, "nothing received yet");

            var s = timeline.Rent();
            s.Tick = 100;
            timeline.Add(s);
            timeline.Advance(1f / 60f); // playback starts the delay behind the first snapshot
            Assert.That(timeline.Ready, Is.False);

            for (var frame = 0; frame < 10; frame++) timeline.Advance(1f / 60f);
            Assert.That(timeline.Ready, Is.True);
        }

        [Test]
        public void Timeline_KeepsSnapshotsInTickOrder_AndDropsDuplicates()
        {
            var timeline = new SnapshotTimeline(60f, 2);
            foreach (var tick in new uint[] { 10, 14, 12, 14, 8 })
            {
                var s = timeline.Rent();
                s.Tick = tick;
                timeline.Add(s);
            }

            Assert.That(timeline.Count, Is.EqualTo(4));
            Assert.That(timeline.NewestTick, Is.EqualTo(14u));
        }

        [Test]
        public void RemoteSeat_IgnoresStalePackets_AndTurnsBurstCountsIntoSingleBursts()
        {
            var seat = new RemoteSeatInput();
            seat.Receive(new InputMessage { Sequence = 10, Throttle = 1f, HasAim = true, BurstCount = 4 });
            seat.Receive(new InputMessage { Sequence = 9, Throttle = 0f, BurstCount = 3 }); // late, reordered

            var first = seat.ReadInput();
            Assert.That(first.Throttle, Is.EqualTo(1f), "the older packet must not overwrite the newer one");
            Assert.That(first.Burst, Is.False, "the first count heard is the baseline, not a press");

            seat.Receive(new InputMessage { Sequence = 11, Throttle = 1f, BurstCount = 5 });
            Assert.That(seat.ReadInput().Burst, Is.True);
            Assert.That(seat.ReadInput().Burst, Is.False, "one burst per count change, however often it's read");
        }

        [Test]
        public void ANewlyDealtSeat_DoesNotFireBurstsFromBeforeTheReDeal()
        {
            // The packet in flight when seats were re-dealt carries the player's old running count.
            var seat = new RemoteSeatInput();
            seat.Receive(new InputMessage { Sequence = 500, BurstCount = 7 });
            Assert.That(seat.ReadInput().Burst, Is.False);
        }

        [Test]
        public void RemoteSeat_SurvivesTheSequenceCounterWrapping()
        {
            var seat = new RemoteSeatInput();
            seat.Receive(new InputMessage { Sequence = 65535, Throttle = 0.2f });
            seat.Receive(new InputMessage { Sequence = 1, Throttle = 0.9f }); // newer, after wrapping

            Assert.That(seat.ReadInput().Throttle, Is.EqualTo(0.9f).Within(1e-4f));
        }
    }
}
