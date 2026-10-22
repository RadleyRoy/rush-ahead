using System;
using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>One point along a river's smooth centreline.</summary>
    public struct RiverSample
    {
        public Vector2 Centre;

        /// <summary>Unit direction of the flow (downstream).</summary>
        public Vector2 Tangent;

        public float HalfWidth;

        /// <summary>Racing line position across the river, -1 (right bank) to +1 (left bank).</summary>
        public float LineOffset;

        /// <summary>Metres along the centreline from the first control point.</summary>
        public float Distance;

        /// <summary>Signed curvature, 1/metres; positive bends left.</summary>
        public float Curvature;

        /// <summary>Unit normal toward the left bank (looking downstream).</summary>
        public Vector2 Left => new(-Tangent.y, Tangent.x);

        public Vector2 LeftBank => Centre + Left * HalfWidth;
        public Vector2 RightBank => Centre - Left * HalfWidth;
        public Vector2 RacingPoint => Centre + Left * (LineOffset * HalfWidth);
    }

    /// <summary>Where a point is relative to the river: how far down it, and how far across.</summary>
    public struct RiverProjection
    {
        public float Distance;

        /// <summary>Metres from the centreline; positive toward the left bank.</summary>
        public float Lateral;

        public Vector2 Tangent;
        public float HalfWidth;
    }

    /// <summary>
    /// A river's shape: a centripetal Catmull-Rom curve through the control points (it never loops or overshoots
    /// between unevenly spaced points), sampled about every metre and a half, with width and racing line eased
    /// between points so the banks stay smooth. Pure geometry: banks, gates and currents are all built from it.
    /// </summary>
    public sealed class River
    {
        const float Spacing = 1.5f;

        readonly List<RiverSample> samples = new();

        public River(IReadOnlyList<RiverPoint> points)
        {
            if (points == null || points.Count < 2) throw new ArgumentException("A river needs at least two points.", nameof(points));
            Sample(points);
        }

        public IReadOnlyList<RiverSample> Samples => samples;
        public float Length => samples[^1].Distance;

        /// <summary>How sharply the tightest bend folds the inside bank: 1 means the bank turns back on itself there.</summary>
        public float TightestBend
        {
            get
            {
                var worst = 0f;
                foreach (var s in samples) worst = Mathf.Max(worst, Mathf.Abs(s.Curvature) * s.HalfWidth);
                return worst;
            }
        }

        /// <summary>The river at <paramref name="distance"/> metres down it (clamped to its ends).</summary>
        public RiverSample At(float distance)
        {
            distance = Mathf.Clamp(distance, 0f, Length);
            int lo = 0, hi = samples.Count - 1;
            while (hi - lo > 1)
            {
                var mid = (lo + hi) / 2;
                if (samples[mid].Distance <= distance) lo = mid;
                else hi = mid;
            }

            var a = samples[lo];
            var b = samples[hi];
            var span = b.Distance - a.Distance;
            var t = span > 0f ? (distance - a.Distance) / span : 0f;
            return new RiverSample
            {
                Centre = Vector2.Lerp(a.Centre, b.Centre, t),
                Tangent = Vector2.Lerp(a.Tangent, b.Tangent, t).normalized,
                HalfWidth = Mathf.Lerp(a.HalfWidth, b.HalfWidth, t),
                LineOffset = Mathf.Lerp(a.LineOffset, b.LineOffset, t),
                Distance = distance,
                Curvature = Mathf.Lerp(a.Curvature, b.Curvature, t),
            };
        }

        /// <summary>The closest point on the centreline to <paramref name="point"/>.</summary>
        public RiverProjection Project(Vector2 point)
        {
            var best = float.MaxValue;
            var result = default(RiverProjection);
            for (var i = 0; i < samples.Count - 1; i++)
            {
                var a = samples[i];
                var b = samples[i + 1];
                var segment = b.Centre - a.Centre;
                var lengthSq = segment.sqrMagnitude;
                var t = lengthSq > 0f ? Mathf.Clamp01(Vector2.Dot(point - a.Centre, segment) / lengthSq) : 0f;
                var closest = a.Centre + segment * t;
                var distanceSq = (point - closest).sqrMagnitude;
                if (distanceSq >= best) continue;

                best = distanceSq;
                var tangent = Vector2.Lerp(a.Tangent, b.Tangent, t).normalized;
                result = new RiverProjection
                {
                    Distance = Mathf.Lerp(a.Distance, b.Distance, t),
                    Lateral = Vector2.Dot(point - closest, new Vector2(-tangent.y, tangent.x)),
                    Tangent = tangent,
                    HalfWidth = Mathf.Lerp(a.HalfWidth, b.HalfWidth, t),
                };
            }

            return result;
        }

        /// <summary>
        /// The water's edge as one closed loop, counter-clockwise: down the right bank, across the end, up the left
        /// bank and across the start. The water is on the left of every edge, which is the way the cliffs build outward.
        /// </summary>
        public Vector2[] Outline()
        {
            var outline = new Vector2[samples.Count * 2];
            for (var i = 0; i < samples.Count; i++)
            {
                outline[i] = samples[i].RightBank;
                outline[outline.Length - 1 - i] = samples[i].LeftBank;
            }

            return outline;
        }

        void Sample(IReadOnlyList<RiverPoint> points)
        {
            // Phantom end points continue the first and last segments, so the curve starts and ends heading straight on.
            Vector2 P(int i) => i < 0 ? 2f * points[0].position - points[1].position
                : i >= points.Count ? 2f * points[^1].position - points[^2].position
                : points[i].position;

            for (var segment = 0; segment < points.Count - 1; segment++)
            {
                var p0 = P(segment - 1);
                var p1 = P(segment);
                var p2 = P(segment + 1);
                var p3 = P(segment + 2);
                var steps = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(p1, p2) / Spacing));
                for (var step = segment == 0 ? 0 : 1; step <= steps; step++)
                {
                    var u = step / (float)steps;
                    var ease = u * u * (3f - 2f * u); // width and line change smoothly, so the banks have no kinks at control points
                    samples.Add(new RiverSample
                    {
                        Centre = CentripetalCatmullRom(p0, p1, p2, p3, u),
                        HalfWidth = 0.5f * Mathf.Lerp(points[segment].width, points[segment + 1].width, ease),
                        LineOffset = Mathf.Lerp(points[segment].lineOffset, points[segment + 1].lineOffset, ease),
                    });
                }
            }

            var distance = 0f;
            for (var i = 0; i < samples.Count; i++)
            {
                var s = samples[i];
                if (i > 0) distance += Vector2.Distance(samples[i - 1].Centre, s.Centre);
                s.Distance = distance;
                var ahead = samples[Mathf.Min(i + 1, samples.Count - 1)].Centre;
                var behind = samples[Mathf.Max(i - 1, 0)].Centre;
                s.Tangent = (ahead - behind).normalized;
                samples[i] = s;
            }

            for (var i = 0; i < samples.Count; i++)
            {
                var before = samples[Mathf.Max(i - 1, 0)];
                var after = samples[Mathf.Min(i + 1, samples.Count - 1)];
                var turn = Vector2.SignedAngle(before.Tangent, after.Tangent) * Mathf.Deg2Rad;
                var s = samples[i];
                s.Curvature = after.Distance > before.Distance ? turn / (after.Distance - before.Distance) : 0f;
                samples[i] = s;
            }
        }

        /// <summary>Barry and Goldman's pyramid form with alpha = 0.5, evaluated at <paramref name="u"/> in [0, 1] between p1 and p2.</summary>
        static Vector2 CentripetalCatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float u)
        {
            static float Knot(float t, Vector2 a, Vector2 b) => t + Mathf.Max(Mathf.Sqrt(Vector2.Distance(a, b)), 1e-4f);

            const float t0 = 0f;
            var t1 = Knot(t0, p0, p1);
            var t2 = Knot(t1, p1, p2);
            var t3 = Knot(t2, p2, p3);
            var t = Mathf.Lerp(t1, t2, u);

            var a1 = (t1 - t) / (t1 - t0) * p0 + (t - t0) / (t1 - t0) * p1;
            var a2 = (t2 - t) / (t2 - t1) * p1 + (t - t1) / (t2 - t1) * p2;
            var a3 = (t3 - t) / (t3 - t2) * p2 + (t - t2) / (t3 - t2) * p3;
            var b1 = (t2 - t) / (t2 - t0) * a1 + (t - t0) / (t2 - t0) * a2;
            var b2 = (t3 - t) / (t3 - t1) * a2 + (t - t1) / (t3 - t1) * a3;
            return (t2 - t) / (t2 - t1) * b1 + (t - t1) / (t2 - t1) * b2;
        }
    }
}
