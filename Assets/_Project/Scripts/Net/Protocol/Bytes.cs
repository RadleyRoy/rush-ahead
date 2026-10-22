using System;

namespace RushAhead.Net
{
    /// <summary>Thrown for any malformed, truncated or unsupported message. Never an out-of-range read.</summary>
    public class ProtocolException : Exception
    {
        public ProtocolException(string message) : base(message) { }
    }

    /// <summary>The other side runs a build with a different wire format. Not a fault: both need the same version.</summary>
    public sealed class VersionMismatchException : ProtocolException
    {
        public VersionMismatchException(byte theirs)
            : base($"The other device runs protocol {theirs}; this one runs {Protocol.Version}. Install the same version of Rush Ahead on both.") =>
            Theirs = theirs;

        public byte Theirs { get; }
    }

    /// <summary>Little-endian writer into a growable buffer. Reuse one per sender; <see cref="Reset"/> between messages.</summary>
    public sealed class ByteWriter
    {
        byte[] buffer;

        public ByteWriter(int capacity = 128) => buffer = new byte[capacity];

        public int Length { get; private set; }
        public ArraySegment<byte> Written => new(buffer, 0, Length);

        public void Reset() => Length = 0;

        public byte[] ToArray()
        {
            var copy = new byte[Length];
            Buffer.BlockCopy(buffer, 0, copy, 0, Length);
            return copy;
        }

        public void U8(byte value)
        {
            Ensure(1);
            buffer[Length++] = value;
        }

        public void U16(ushort value)
        {
            Ensure(2);
            buffer[Length++] = (byte)value;
            buffer[Length++] = (byte)(value >> 8);
        }

        public void I16(short value) => U16((ushort)value);

        public void U32(uint value)
        {
            Ensure(4);
            for (var shift = 0; shift < 32; shift += 8) buffer[Length++] = (byte)(value >> shift);
        }

        public void I32(int value) => U32((uint)value);

        public void Raw(byte[] bytes)
        {
            Ensure(bytes.Length);
            Buffer.BlockCopy(bytes, 0, buffer, Length, bytes.Length);
            Length += bytes.Length;
        }

        void Ensure(int extra)
        {
            if (Length + extra > buffer.Length) Array.Resize(ref buffer, Math.Max(buffer.Length * 2, Length + extra));
        }
    }

    /// <summary>Bounds-checked little-endian reader over one received message.</summary>
    public sealed class ByteReader
    {
        readonly byte[] data;
        readonly int end;
        int position;

        public ByteReader(ArraySegment<byte> message)
        {
            data = message.Array ?? throw new ProtocolException("Empty message.");
            position = message.Offset;
            end = message.Offset + message.Count;
        }

        public int Remaining => end - position;

        public byte U8()
        {
            Need(1);
            return data[position++];
        }

        public ushort U16()
        {
            Need(2);
            var value = (ushort)(data[position] | data[position + 1] << 8);
            position += 2;
            return value;
        }

        public short I16() => (short)U16();

        public uint U32()
        {
            Need(4);
            var value = (uint)(data[position] | data[position + 1] << 8 | data[position + 2] << 16 | data[position + 3] << 24);
            position += 4;
            return value;
        }

        public int I32() => (int)U32();

        public byte[] Raw(int count)
        {
            Need(count);
            var bytes = new byte[count];
            Buffer.BlockCopy(data, position, bytes, 0, count);
            position += count;
            return bytes;
        }

        void Need(int count)
        {
            if (end - position < count)
                throw new ProtocolException($"Message truncated: needed {count} more byte(s), {end - position} left.");
        }
    }
}
