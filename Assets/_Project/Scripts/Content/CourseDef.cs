using System;
using UnityEngine;

namespace RushAhead.Content
{
    /// <summary>A control point on a river's centreline.</summary>
    [Serializable]
    public struct RiverPoint
    {
        public Vector2 position;

        [Tooltip("Bank to bank, in metres. The raft is 3.2 m across.")]
        [Min(4f)] public float width;

        [Tooltip("Where the racing line runs here: -1 hugs the right bank, 0 the middle, +1 the left bank. Steer it round rocks.")]
        [Range(-0.8f, 0.8f)] public float lineOffset;

        public RiverPoint(float x, float y, float width, float lineOffset = 0f)
        {
            position = new Vector2(x, y);
            this.width = width;
            this.lineOffset = lineOffset;
        }
    }

    [Serializable]
    public struct RockSpot
    {
        public Vector2 position;
        [Min(0.3f)] public float radius;

        public RockSpot(float x, float y, float radius)
        {
            position = new Vector2(x, y);
            this.radius = radius;
        }
    }

    /// <summary>A stretch of river where the water itself moves: with the flow (positive) or against it.</summary>
    [Serializable]
    public struct CurrentZone
    {
        [Tooltip("Metres along the river.")] public float from;
        [Tooltip("Metres along the river.")] public float to;
        [Tooltip("Metres per second down the river at mid-stream; negative runs upstream. It eases off toward the banks.")]
        public float speed;

        public CurrentZone(float from, float to, float speed)
        {
            this.from = from;
            this.to = to;
            this.speed = speed;
        }
    }

    /// <summary>A log floating loose: it drifts with the current, and rafts (and air) shove it about.</summary>
    [Serializable]
    public struct LogSpot
    {
        public Vector2 position;
        [Tooltip("Which way it lies, in degrees (0 points along +x).")] public float angleDeg;
        [Min(1f)] public float length;

        public LogSpot(Vector2 position, float angleDeg, float length)
        {
            this.position = position;
            this.angleDeg = angleDeg;
            this.length = length;
        }
    }

    /// <summary>Water turning round a point: it carries a raft round and draws it toward the middle.</summary>
    [Serializable]
    public struct WhirlpoolSpot
    {
        public Vector2 position;
        [Min(1f)] public float radius;
        [Tooltip("Metres per second round at its strongest, halfway out. Positive turns anticlockwise seen from above.")]
        public float spin;
        [Tooltip("Metres per second toward the middle at its strongest.")]
        [Min(0f)] public float pull;

        public WhirlpoolSpot(Vector2 position, float radius, float spin, float pull)
        {
            this.position = position;
            this.radius = radius;
            this.spin = spin;
            this.pull = pull;
        }
    }

    /// <summary>A paddle wheel turning in the river: its arms sweep rafts sideways, toward the edge.</summary>
    [Serializable]
    public struct WheelSpot
    {
        public Vector2 position;
        [Tooltip("Arm length, in metres.")] [Min(1f)] public float radius;
        [Range(2, 6)] public int arms;
        [Tooltip("Turning speed; positive is anticlockwise seen from above.")] public float degreesPerSecond;

        public WheelSpot(Vector2 position, float radius, int arms, float degreesPerSecond)
        {
            this.position = position;
            this.radius = radius;
            this.arms = arms;
            this.degreesPerSecond = degreesPerSecond;
        }
    }

    /// <summary>A crocodile waiting at the river's edge that lunges across on a timetable and rams whatever is there.</summary>
    [Serializable]
    public struct CrocSpot
    {
        [Tooltip("Metres down the river.")] public float distance;
        [Tooltip("Which edge it waits at: +1 the left, -1 the right.")] public float side;
        [Tooltip("How far across it lunges, as a share of the river's width.")] [Range(0.2f, 0.9f)] public float reach;
        [Tooltip("Seconds between lunges.")] [Min(2f)] public float period;
        [Tooltip("Seconds added to the clock, so crocs don't all lunge together.")] public float offset;

        public CrocSpot(float distance, float side, float reach, float period, float offset)
        {
            this.distance = distance;
            this.side = side;
            this.reach = reach;
            this.period = period;
            this.offset = offset;
        }
    }

    /// <summary>A bridge over the river with a log swinging beneath it: brace to duck under it, or be knocked over.</summary>
    [Serializable]
    public struct SwingSpot
    {
        [Tooltip("Metres down the river.")] public float distance;
        [Tooltip("Seconds for a swing across and back.")] [Min(2f)] public float period;
        public float offset;
        [Min(1f)] public float logLength;

        public SwingSpot(float distance, float period, float offset, float logLength)
        {
            this.distance = distance;
            this.period = period;
            this.offset = offset;
            this.logLength = logLength;
        }
    }

    /// <summary>A geyser in the riverbed: it bubbles a warning, then spouts and throws any raft over it off balance.</summary>
    [Serializable]
    public struct GeyserSpot
    {
        public Vector2 position;
        [Min(1f)] public float radius;
        [Tooltip("Seconds between spouts.")] [Min(3f)] public float period;
        public float offset;

        public GeyserSpot(Vector2 position, float radius, float period, float offset)
        {
            this.position = position;
            this.radius = radius;
            this.period = period;
            this.offset = offset;
        }
    }

    /// <summary>Where a course is: it sets the look of the banks, not the rules.</summary>
    public enum CourseScene
    {
        /// <summary>Red earth cliffs, grass, trees and bamboo.</summary>
        Creek,

        /// <summary>Low sandy shores, reeds and lily pads.</summary>
        Lake,

        /// <summary>Tall grey rock, pines and white water.</summary>
        Rapids,
    }

    /// <summary>
    /// A river course. Everything is laid out along a smooth centreline through <see cref="river"/>: the banks, the
    /// checkpoint gates (by distance down the river), the racing line the autopilot follows, and the currents.
    /// Edit the points by dragging them in the Scene view with the asset selected.
    /// </summary>
    [CreateAssetMenu(menuName = "Rush Ahead/Course", fileName = "Course")]
    public sealed class CourseDef : ScriptableObject
    {
        public string displayName = "Course";
        [TextArea] public string blurb = "";
        public CourseScene scene = CourseScene.Creek;

        [Tooltip("Stars a player needs, across every course, before this one opens.")]
        [Min(0)] public int starsToUnlock;

        [Header("River")]
        public RiverPoint[] river = Array.Empty<RiverPoint>();

        [Tooltip("Where the raft starts, in metres down the river.")]
        [Min(0f)] public float startDistance = 8f;

        [Tooltip("Checkpoint gates, in metres down the river, in order. The last one is the finish line.")]
        public float[] gates = Array.Empty<float>();

        [Header("Water")]
        [Tooltip("How fast the river runs everywhere, in metres per second down the river at mid-stream: it carries the rafts along.")]
        [Min(0f)] public float flow = 1.2f;

        public RockSpot[] rocks = Array.Empty<RockSpot>();
        public Vector2[] barrels = Array.Empty<Vector2>();
        public LogSpot[] logs = Array.Empty<LogSpot>();
        public CurrentZone[] currents = Array.Empty<CurrentZone>();
        public WhirlpoolSpot[] whirlpools = Array.Empty<WhirlpoolSpot>();

        [Header("Obstacles")]
        public WheelSpot[] wheels = Array.Empty<WheelSpot>();
        public CrocSpot[] crocodiles = Array.Empty<CrocSpot>();
        public SwingSpot[] swingers = Array.Empty<SwingSpot>();
        public GeyserSpot[] geysers = Array.Empty<GeyserSpot>();

        [Header("Scoring (seconds)")]
        [Tooltip("Finish within par for one star.")]
        [Min(1f)] public float parSeconds = 120f;
        [Min(1f)] public float twoStarSeconds = 100f;
        [Min(1f)] public float threeStarSeconds = 80f;

        /// <summary>0 to 3 stars for a finishing time.</summary>
        public int Stars(float seconds) => seconds <= threeStarSeconds ? 3 : seconds <= twoStarSeconds ? 2 : seconds <= parSeconds ? 1 : 0;

        public float FinishDistance => gates.Length > 0 ? gates[^1] : 0f;
    }
}
