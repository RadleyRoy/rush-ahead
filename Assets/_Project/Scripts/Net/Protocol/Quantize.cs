using UnityEngine;

namespace RushAhead.Net
{
    /// <summary>
    /// The single shared definition of how floats become integers on the wire. Both ends use these tables,
    /// so they must never be duplicated or changed without bumping <see cref="Protocol.Version"/>.
    /// </summary>
    public static class Quantize
    {
        const float PositionScale = 256f;  // int32: 1/256 m, far beyond any course's size
        const float VelocityScale = 256f;  // int16: 1/256 m/s, up to +-128 m/s
        const float SpinScale = 512f;      // int16: 1/512 rad/s, up to +-64 rad/s
        const float TiltScale = 4096f;     // int16: 1/4096 rad, up to +-8 rad
        const float TwoPi = 2f * Mathf.PI;

        public static int Position(float metres) => Mathf.RoundToInt(metres * PositionScale);
        public static float Position(int value) => value / PositionScale;

        public static short Velocity(float metresPerSecond) => Clamp16(metresPerSecond * VelocityScale);
        public static float Velocity(short value) => value / VelocityScale;

        public static short Spin(float radiansPerSecond) => Clamp16(radiansPerSecond * SpinScale);
        public static float Spin(short value) => value / SpinScale;

        public static short Tilt(float radians) => Clamp16(radians * TiltScale);
        public static float Tilt(short value) => value / TiltScale;

        /// <summary>A full turn in 65,536 steps (0.0055 degrees), wrapping.</summary>
        public static ushort Angle(float radians) => (ushort)(Mathf.RoundToInt(Mathf.Repeat(radians, TwoPi) / TwoPi * 65536f) & 0xFFFF);
        public static float Angle(ushort value) => value / 65536f * TwoPi;

        public static ushort AngleDegrees(float degrees) => Angle(degrees * Mathf.Deg2Rad);
        public static float AngleDegrees(ushort value) => Mathf.DeltaAngle(0f, Angle(value) * Mathf.Rad2Deg);

        /// <summary>0..1 in 256 steps.</summary>
        public static byte Unit(float value) => (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);
        public static float Unit(byte value) => value / 255f;

        static short Clamp16(float value) => (short)Mathf.Clamp(Mathf.RoundToInt(value), short.MinValue, short.MaxValue);
    }
}
