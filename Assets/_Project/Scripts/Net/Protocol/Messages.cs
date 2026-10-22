using System;
using System.Collections.Generic;
using System.Text;
using RushAhead.Content;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.Net
{
    public enum MessageType : byte
    {
        /// <summary>Host to one client, reliable, on every change: each seat's animal and which seat is theirs.</summary>
        Roster = 0x02,

        /// <summary>Host to every client, reliable, on every change: which course is loaded and when its race starts.</summary>
        Race = 0x03,

        /// <summary>Either way, reliable: a client asks the AI crew to do something; the host tells everyone what it's doing.</summary>
        Order = 0x04,

        /// <summary>Host to every client, unreliable, 30 Hz: the whole visible world.</summary>
        Snapshot = 0x10,

        /// <summary>Client to host, unreliable, 30 Hz: one seat's controls.</summary>
        Input = 0x20,

        /// <summary>Client to host, reliable, on joining and on every change: the animal they want to be.</summary>
        Profile = 0x21,

        /// <summary>Client to host, reliable: move me to this free seat.</summary>
        ClaimSeat = 0x22,
    }

    /// <summary>
    /// One seat's controls. Input is state, not events: every message carries the current stick, and bursts are a running
    /// count, so a lost message is simply superseded by the next one and no burst is ever lost or doubled.
    /// </summary>
    public struct InputMessage
    {
        public byte Seat;
        public ushort Sequence;
        public float AimAngle;
        public bool HasAim;
        public float Throttle;
        public bool Brace;
        public byte BurstCount;
    }

    /// <summary>One seat as a particular player sees it.</summary>
    public struct RosterSeat
    {
        public Loadout Loadout;

        /// <summary>A person sits here; otherwise it's a spare, driven by the host device's other controls.</summary>
        public bool Human;

        /// <summary>The host's own player.</summary>
        public bool Host;

        /// <summary>The player this roster was sent to.</summary>
        public bool Yours;

        /// <summary>An AI crewmate sits here.</summary>
        public bool Ai;

        /// <summary>Kept for a player who dropped out; the look is theirs, and they get the seat back if they return soon.</summary>
        public bool Kept;
    }

    public struct OrderMessage
    {
        public CrewOrder Order;

        /// <summary>Where to push, for <see cref="CrewOrder.PushHere"/>.</summary>
        public Vector2 Point;
    }

    /// <summary>What a host broadcasts on the LAN about once a second, so joiners can find it without typing an address.</summary>
    public struct RoomAdvert
    {
        /// <summary>Random per hosting session: tells two rooms apart, and makes one room heard two ways show once.</summary>
        public uint RoomId;
        public byte Version;
        public ushort GamePort;
        public byte Players;
        public byte Seats;
        public string HostName;

        public bool Compatible => Version == Protocol.Version;
        public bool Full => Players >= Seats;
    }

    public struct CrewSnapshot
    {
        public float LocalAimDeg;
        public float Spool;
        public float Air;
        public bool Braced;
        public byte BurstCount;
        public byte BlowerIndex;
    }

    public sealed class BoatSnapshot
    {
        public Vector2 Position;
        public float AngleRad;
        public Vector2 Velocity;
        public float AngularVelocity;
        public Vector2 Tilt;
        public bool Capsized;
        public byte CapsizeCount;
        public byte GatesPassed;
        public uint FinishTick;

        /// <summary>Wraps at 256; only ever compared for change.</summary>
        public byte TeleportCount;

        public readonly List<CrewSnapshot> Crew = new();
    }

    /// <summary>The venue and race everyone is in. Course 0 is the practice lagoon (no race); course n is the host's n-th course.</summary>
    public struct RaceMessage
    {
        public byte Course;

        /// <summary>The host tick the countdown ends and the clock starts.</summary>
        public uint StartTick;
    }

    public struct PropSnapshot
    {
        public Vector2 Position;
        public float AngleRad;
    }

    /// <summary>Everything a client draws, as of one host tick. A list of boats, so Team Race needs no protocol change.</summary>
    public sealed class Snapshot
    {
        public uint Tick;
        public readonly List<BoatSnapshot> Boats = new();
        public readonly List<PropSnapshot> Props = new();
    }

    public static class Protocol
    {
        /// <summary>Bump on any wire-format change. Mismatched peers refuse each other.</summary>
        public const byte Version = 4;

        const byte FlagHasAim = 1, FlagBrace = 2;
        const byte BoatCapsized = 1;
        const byte CrewBraced = 1;
        const byte SeatHuman = 1, SeatHost = 2, SeatYours = 4, SeatAi = 8, SeatKept = 16;
        const int MaxBoats = 8, MaxCrew = 4, MaxProps = 64;
        static readonly byte[] AdvertMagic = { (byte)'R', (byte)'U', (byte)'S', (byte)'H' };

        public static MessageType PeekType(ArraySegment<byte> message)
        {
            if (message.Array == null || message.Count == 0) throw new ProtocolException("Empty message.");
            return (MessageType)message.Array[message.Offset];
        }

        public static void Write(ByteWriter w, in InputMessage m)
        {
            w.U8((byte)MessageType.Input);
            w.U8(m.Seat);
            w.U16(m.Sequence);
            w.U16(Quantize.Angle(m.AimAngle));
            w.U8(Quantize.Unit(m.Throttle));
            w.U8((byte)((m.HasAim ? FlagHasAim : 0) | (m.Brace ? FlagBrace : 0)));
            w.U8(m.BurstCount);
        }

        public static InputMessage ReadInput(ByteReader r)
        {
            Expect(r, MessageType.Input);
            var m = new InputMessage
            {
                Seat = r.U8(),
                Sequence = r.U16(),
                AimAngle = Quantize.Angle(r.U16()),
                Throttle = Quantize.Unit(r.U8()),
            };
            var flags = r.U8();
            m.HasAim = (flags & FlagHasAim) != 0;
            m.Brace = (flags & FlagBrace) != 0;
            m.BurstCount = r.U8();
            return m;
        }

        /// <summary>A roster for one player: every seat in seat order, so the crew size is its length.</summary>
        public static void Write(ByteWriter w, IReadOnlyList<RosterSeat> seats)
        {
            w.U8((byte)MessageType.Roster);
            w.U8(Version);
            w.U8((byte)seats.Count);
            foreach (var seat in seats)
            {
                w.U8((byte)((seat.Human ? SeatHuman : 0) | (seat.Host ? SeatHost : 0) | (seat.Yours ? SeatYours : 0) |
                            (seat.Ai ? SeatAi : 0) | (seat.Kept ? SeatKept : 0)));
                Write(w, seat.Loadout);
            }
        }

        public static RosterSeat[] ReadRoster(ByteReader r)
        {
            Expect(r, MessageType.Roster);
            var version = r.U8();
            if (version != Version) throw new VersionMismatchException(version);
            var count = r.U8();
            if (count is < 1 or > MaxCrew) throw new ProtocolException($"Bad crew size {count}.");

            var seats = new RosterSeat[count];
            for (var i = 0; i < count; i++)
            {
                var flags = r.U8();
                seats[i] = new RosterSeat
                {
                    Human = (flags & SeatHuman) != 0,
                    Host = (flags & SeatHost) != 0,
                    Yours = (flags & SeatYours) != 0,
                    Ai = (flags & SeatAi) != 0,
                    Kept = (flags & SeatKept) != 0,
                    Loadout = ReadLoadout(r),
                };
            }

            ExpectEnd(r, "roster");
            return seats;
        }

        public static void Write(ByteWriter w, in RaceMessage m)
        {
            w.U8((byte)MessageType.Race);
            w.U8(m.Course);
            w.U32(m.StartTick);
        }

        public static RaceMessage ReadRace(ByteReader r)
        {
            Expect(r, MessageType.Race);
            var m = new RaceMessage { Course = r.U8(), StartTick = r.U32() };
            ExpectEnd(r, "race");
            return m;
        }

        /// <param name="device">A random id this device keeps, so the host recognises a player who drops out and comes back.</param>
        public static void WriteProfile(ByteWriter w, in Loadout loadout, ulong device)
        {
            w.U8((byte)MessageType.Profile);
            w.U8(Version);
            Write(w, loadout);
            w.U32((uint)device);
            w.U32((uint)(device >> 32));
        }

        public static Loadout ReadProfile(ByteReader r, out ulong device)
        {
            Expect(r, MessageType.Profile);
            var version = r.U8();
            if (version != Version) throw new VersionMismatchException(version);
            var loadout = ReadLoadout(r);
            device = r.U32() | (ulong)r.U32() << 32;
            ExpectEnd(r, "profile");
            return loadout;
        }

        public static void Write(ByteWriter w, in OrderMessage m)
        {
            w.U8((byte)MessageType.Order);
            w.U8((byte)m.Order);
            w.I32(Quantize.Position(m.Point.x));
            w.I32(Quantize.Position(m.Point.y));
        }

        public static OrderMessage ReadOrder(ByteReader r)
        {
            Expect(r, MessageType.Order);
            var order = r.U8();
            if (order > (byte)CrewOrder.PushHere) throw new ProtocolException($"Unknown crew order {order}.");
            var m = new OrderMessage { Order = (CrewOrder)order, Point = new Vector2(Quantize.Position(r.I32()), Quantize.Position(r.I32())) };
            ExpectEnd(r, "order");
            return m;
        }

        public static void WriteClaimSeat(ByteWriter w, int seat)
        {
            w.U8((byte)MessageType.ClaimSeat);
            w.U8((byte)seat);
        }

        public static int ReadClaimSeat(ByteReader r)
        {
            Expect(r, MessageType.ClaimSeat);
            var seat = r.U8();
            ExpectEnd(r, "seat claim");
            return seat;
        }

        public static void Write(ByteWriter w, in RoomAdvert advert)
        {
            w.Raw(AdvertMagic);
            w.U8(advert.Version);
            w.U32(advert.RoomId);
            w.U16(advert.GamePort);
            w.U8(advert.Players);
            w.U8(advert.Seats);
            WriteName(w, advert.HostName);
        }

        /// <summary>
        /// Anything can turn up on the discovery port, so this never throws: false for whatever isn't one of our adverts.
        /// An advert from another version reads as far as its version, so it can still be listed as needing an update.
        /// </summary>
        public static bool TryReadAdvert(ArraySegment<byte> datagram, out RoomAdvert advert)
        {
            advert = default;
            try
            {
                var r = new ByteReader(datagram);
                foreach (var b in AdvertMagic)
                    if (r.U8() != b) return false;

                advert.Version = r.U8();
                if (advert.Version != Version) return true; // the rest may be laid out differently
                advert.RoomId = r.U32();
                advert.GamePort = r.U16();
                advert.Players = r.U8();
                advert.Seats = r.U8();
                advert.HostName = ReadName(r);
                return advert.GamePort != 0 && r.Remaining == 0;
            }
            catch (ProtocolException)
            {
                return false;
            }
        }

        static void Write(ByteWriter w, in Loadout loadout)
        {
            w.U8(loadout.Species);
            w.U8(loadout.Fur);
            w.U8(loadout.Blower);
            WriteName(w, loadout.Name);
        }

        static Loadout ReadLoadout(ByteReader r) => new()
        {
            Species = r.U8(),
            Fur = r.U8(),
            Blower = r.U8(),
            Name = ReadName(r),
        };

        static void WriteName(ByteWriter w, string name)
        {
            var bytes = Encoding.UTF8.GetBytes(Loadout.CleanName(name));
            w.U8((byte)bytes.Length);
            w.Raw(bytes);
        }

        static string ReadName(ByteReader r)
        {
            var length = Bounded(r.U8(), Loadout.MaxNameBytes, "name length");
            return Loadout.CleanName(Encoding.UTF8.GetString(r.Raw(length))); // malformed UTF-8 decodes to replacement characters
        }

        public static void Write(ByteWriter w, Snapshot s)
        {
            w.U8((byte)MessageType.Snapshot);
            w.U32(s.Tick);
            w.U8((byte)s.Boats.Count);
            foreach (var boat in s.Boats)
            {
                w.I32(Quantize.Position(boat.Position.x));
                w.I32(Quantize.Position(boat.Position.y));
                w.U16(Quantize.Angle(boat.AngleRad));
                w.I16(Quantize.Velocity(boat.Velocity.x));
                w.I16(Quantize.Velocity(boat.Velocity.y));
                w.I16(Quantize.Spin(boat.AngularVelocity));
                w.I16(Quantize.Tilt(boat.Tilt.x));
                w.I16(Quantize.Tilt(boat.Tilt.y));
                w.U8(boat.Capsized ? BoatCapsized : (byte)0);
                w.U8(boat.CapsizeCount);
                w.U8(boat.GatesPassed);
                w.U32(boat.FinishTick);
                w.U8(boat.TeleportCount);
                w.U8((byte)boat.Crew.Count);
                foreach (var crew in boat.Crew)
                {
                    w.U16(Quantize.AngleDegrees(crew.LocalAimDeg));
                    w.U8(Quantize.Unit(crew.Spool));
                    w.U8(Quantize.Unit(crew.Air));
                    w.U8(crew.Braced ? CrewBraced : (byte)0);
                    w.U8(crew.BurstCount);
                    w.U8(crew.BlowerIndex);
                }
            }

            w.U8((byte)s.Props.Count);
            foreach (var prop in s.Props)
            {
                w.I32(Quantize.Position(prop.Position.x));
                w.I32(Quantize.Position(prop.Position.y));
                w.U16(Quantize.Angle(prop.AngleRad));
            }
        }

        /// <summary>Reads into <paramref name="into"/>, reusing its lists so steady-state receiving allocates nothing.</summary>
        public static void ReadSnapshot(ByteReader r, Snapshot into)
        {
            Expect(r, MessageType.Snapshot);
            into.Tick = r.U32();

            var boats = Bounded(r.U8(), MaxBoats, "boat count");
            while (into.Boats.Count < boats) into.Boats.Add(new BoatSnapshot());
            if (into.Boats.Count > boats) into.Boats.RemoveRange(boats, into.Boats.Count - boats);
            foreach (var boat in into.Boats)
            {
                boat.Position = new Vector2(Quantize.Position(r.I32()), Quantize.Position(r.I32()));
                boat.AngleRad = Quantize.Angle(r.U16());
                boat.Velocity = new Vector2(Quantize.Velocity(r.I16()), Quantize.Velocity(r.I16()));
                boat.AngularVelocity = Quantize.Spin(r.I16());
                boat.Tilt = new Vector2(Quantize.Tilt(r.I16()), Quantize.Tilt(r.I16()));
                boat.Capsized = (r.U8() & BoatCapsized) != 0;
                boat.CapsizeCount = r.U8();
                boat.GatesPassed = r.U8();
                boat.FinishTick = r.U32();
                boat.TeleportCount = r.U8();

                var crew = Bounded(r.U8(), MaxCrew, "crew count");
                boat.Crew.Clear();
                for (var i = 0; i < crew; i++)
                {
                    boat.Crew.Add(new CrewSnapshot
                    {
                        LocalAimDeg = Quantize.AngleDegrees(r.U16()),
                        Spool = Quantize.Unit(r.U8()),
                        Air = Quantize.Unit(r.U8()),
                        Braced = (r.U8() & CrewBraced) != 0,
                        BurstCount = r.U8(),
                        BlowerIndex = r.U8(),
                    });
                }
            }

            var props = Bounded(r.U8(), MaxProps, "prop count");
            into.Props.Clear();
            for (var i = 0; i < props; i++)
                into.Props.Add(new PropSnapshot
                {
                    Position = new Vector2(Quantize.Position(r.I32()), Quantize.Position(r.I32())),
                    AngleRad = Quantize.Angle(r.U16()),
                });

            ExpectEnd(r, "snapshot");
        }

        static void Expect(ByteReader r, MessageType type)
        {
            var actual = (MessageType)r.U8();
            if (actual != type) throw new ProtocolException($"Expected a {type} message, got 0x{(byte)actual:X2}.");
        }

        static void ExpectEnd(ByteReader r, string what)
        {
            if (r.Remaining != 0) throw new ProtocolException($"{r.Remaining} unexpected trailing byte(s) in a {what}.");
        }

        static int Bounded(byte count, int max, string what) =>
            count <= max ? count : throw new ProtocolException($"Implausible {what}: {count} (max {max}).");
    }
}
