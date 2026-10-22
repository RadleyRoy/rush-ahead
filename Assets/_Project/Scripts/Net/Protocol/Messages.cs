using System;
using System.Collections.Generic;
using UnityEngine;

namespace RushAhead.Net
{
    public enum MessageType : byte
    {
        /// <summary>Host to one client, reliable: which seats are theirs and how big the crew is.</summary>
        Seats = 0x01,

        /// <summary>Host to every client, unreliable, 30 Hz: the whole visible world.</summary>
        Snapshot = 0x10,

        /// <summary>Client to host, unreliable, 30 Hz: one seat's controls.</summary>
        Input = 0x20,
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

    public struct SeatsMessage
    {
        public byte CrewCount;
        public byte[] Seats;
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
        public readonly List<CrewSnapshot> Crew = new();
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
        public const byte Version = 1;

        const byte FlagHasAim = 1, FlagBrace = 2;
        const byte BoatCapsized = 1;
        const byte CrewBraced = 1;
        const int MaxBoats = 8, MaxCrew = 4, MaxProps = 64;

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

        public static void Write(ByteWriter w, in SeatsMessage m)
        {
            w.U8((byte)MessageType.Seats);
            w.U8(Version);
            w.U8(m.CrewCount);
            w.U8((byte)m.Seats.Length);
            foreach (var seat in m.Seats) w.U8(seat);
        }

        public static SeatsMessage ReadSeats(ByteReader r)
        {
            Expect(r, MessageType.Seats);
            var version = r.U8();
            if (version != Version) throw new ProtocolException($"Host speaks protocol {version}; this build speaks {Version}. Update both to the same version.");
            var m = new SeatsMessage { CrewCount = r.U8() };
            if (m.CrewCount is < 1 or > MaxCrew) throw new ProtocolException($"Bad crew size {m.CrewCount}.");
            m.Seats = new byte[Bounded(r.U8(), MaxCrew, "seat count")];
            for (var i = 0; i < m.Seats.Length; i++)
            {
                m.Seats[i] = r.U8();
                if (m.Seats[i] >= m.CrewCount) throw new ProtocolException($"Seat {m.Seats[i]} doesn't exist in a crew of {m.CrewCount}.");
            }

            return m;
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

            if (r.Remaining != 0) throw new ProtocolException($"{r.Remaining} unexpected trailing byte(s) in a snapshot.");
        }

        static void Expect(ByteReader r, MessageType type)
        {
            var actual = (MessageType)r.U8();
            if (actual != type) throw new ProtocolException($"Expected a {type} message, got 0x{(byte)actual:X2}.");
        }

        static int Bounded(byte count, int max, string what) =>
            count <= max ? count : throw new ProtocolException($"Implausible {what}: {count} (max {max}).");
    }
}
