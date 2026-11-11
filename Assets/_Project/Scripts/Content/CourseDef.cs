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

    /// <summary>A rocky outcrop on one side: boulders roll off it, across the river and over the far edge.</summary>
    [Serializable]
    public struct RockslideSpot
    {
        [Tooltip("Metres down the river.")] public float distance;
        [Tooltip("Which side the outcrop is on: +1 the left, -1 the right.")] public float side;
        [Tooltip("Seconds between boulders.")] [Min(2f)] public float period;
        public float offset;
        [Tooltip("The boulder's radius, in metres.")] [Min(0.5f)] public float radius;

        public RockslideSpot(float distance, float side, float period, float offset, float radius)
        {
            this.distance = distance;
            this.side = side;
            this.period = period;
            this.offset = offset;
            this.radius = radius;
        }
    }

    /// <summary>A hippo under the water: bubbles, then it surges up and flips anything on top, then sinks again.</summary>
    [Serializable]
    public struct HippoSpot
    {
        public float distance;
        [Tooltip("Metres from the middle of the river; positive is the left.")] public float across;
        [Min(2f)] public float period;
        public float offset;

        public HippoSpot(float distance, float across, float period, float offset)
        {
            this.distance = distance;
            this.across = across;
            this.period = period;
            this.offset = offset;
        }
    }

    /// <summary>A timber wall across the river with one gap that slides from side to side.</summary>
    [Serializable]
    public struct SluiceSpot
    {
        public float distance;
        [Tooltip("How wide the gap is, in metres (a raft is 3.2 m across).")] [Min(3.6f)] public float gap;
        [Tooltip("Seconds for the gap to slide across and back.")] [Min(2f)] public float period;
        public float offset;

        public SluiceSpot(float distance, float gap, float period, float offset)
        {
            this.distance = distance;
            this.gap = gap;
            this.period = period;
            this.offset = offset;
        }
    }

    /// <summary>A long log on a post in the river, spinning flat across it: time your way past.</summary>
    [Serializable]
    public struct SweeperSpot
    {
        public float distance;
        public float across;
        [Tooltip("End to end, in metres.")] [Min(2f)] public float length;
        [Tooltip("Positive is anticlockwise seen from above.")] public float degreesPerSecond;

        public SweeperSpot(float distance, float across, float length, float degreesPerSecond)
        {
            this.distance = distance;
            this.across = across;
            this.length = length;
            this.degreesPerSecond = degreesPerSecond;
        }
    }

    /// <summary>A water cannon on the lip, sweeping its jet across the river and shoving whoever it catches.</summary>
    [Serializable]
    public struct CannonSpot
    {
        public float distance;
        [Tooltip("Which side it stands on: +1 the left, -1 the right.")] public float side;
        [Tooltip("Seconds for a sweep up and back.")] [Min(1f)] public float period;
        public float offset;
        [Tooltip("How far up and down the river the jet swings, in degrees either way.")] [Range(0f, 60f)] public float sweepDeg;

        public CannonSpot(float distance, float side, float period, float offset, float sweepDeg)
        {
            this.distance = distance;
            this.side = side;
            this.period = period;
            this.offset = offset;
            this.sweepDeg = sweepDeg;
        }
    }

    /// <summary>A stretch of white water: the current surges and the raft gets thrown about.</summary>
    [Serializable]
    public struct RapidsSpot
    {
        public float from;
        public float to;
        [Tooltip("Extra speed the water picks up, in metres per second.")] public float surge;

        public RapidsSpot(float from, float to, float surge)
        {
            this.from = from;
            this.to = to;
            this.surge = surge;
        }
    }

    /// <summary>A low stone weir across the river: the water speeds up over it and the raft lands hard below.</summary>
    [Serializable]
    public struct WeirSpot
    {
        public float distance;

        public WeirSpot(float distance) => this.distance = distance;
    }

    /// <summary>A springy buoy that bounces rafts away hard.</summary>
    [Serializable]
    public struct BumperSpot
    {
        public Vector2 position;
        [Min(0.4f)] public float radius;

        public BumperSpot(Vector2 position, float radius)
        {
            this.position = position;
            this.radius = radius;
        }
    }

    /// <summary>A cable ferry crossing back and forth across the river: heavy, solid and in the way.</summary>
    [Serializable]
    public struct FerrySpot
    {
        public float distance;
        [Tooltip("Seconds to cross and come back.")] [Min(3f)] public float period;
        public float offset;

        public FerrySpot(float distance, float period, float offset)
        {
            this.distance = distance;
            this.period = period;
            this.offset = offset;
        }
    }

    /// <summary>A bear on the lip that rears up and swipes at rafts passing its side, flinging them across.</summary>
    [Serializable]
    public struct BearSpot
    {
        public float distance;
        [Tooltip("Which side it stands on: +1 the left, -1 the right.")] public float side;
        [Min(2f)] public float period;
        public float offset;

        public BearSpot(float distance, float side, float period, float offset)
        {
            this.distance = distance;
            this.side = side;
            this.period = period;
            this.offset = offset;
        }
    }

    /// <summary>
    /// A creature or thing that goes back and forth across the river on a timetable: a snapping turtle, a piranha
    /// shoal, a waterspout, a net boat. What it does depends on the kind; this is just where and when.
    /// </summary>
    [Serializable]
    public struct CrossingSpot
    {
        public float distance;
        [Tooltip("Seconds to cross and come back.")] [Min(2f)] public float period;
        public float offset;

        public CrossingSpot(float distance, float period, float offset)
        {
            this.distance = distance;
            this.period = period;
            this.offset = offset;
        }
    }

    /// <summary>Something on one side that acts on a timetable: a mudslide pouring in, a pelican diving.</summary>
    [Serializable]
    public struct SideSpot
    {
        public float distance;
        [Tooltip("+1 the left, -1 the right.")] public float side;
        [Min(2f)] public float period;
        public float offset;

        public SideSpot(float distance, float side, float period, float offset)
        {
            this.distance = distance;
            this.side = side;
            this.period = period;
            this.offset = offset;
        }
    }

    /// <summary>Something right across the river that acts on a timetable: electric eels, a beaver dam, ice floes.</summary>
    [Serializable]
    public struct AcrossSpot
    {
        public float distance;
        [Min(2f)] public float period;
        public float offset;

        public AcrossSpot(float distance, float period, float offset)
        {
            this.distance = distance;
            this.period = period;
            this.offset = offset;
        }
    }


    /// <summary>
    /// One of the newer obstacles (Phase 17 on: the kraken, tidal bore, jump ramp and the rest), all in one list: its
    /// kind, where it is, and its timing and size, each kind reading the fields it needs.
    /// </summary>
    [Serializable]
    public struct ObstacleSpot
    {
        public ObstacleKind kind;
        [Tooltip("Metres down the river.")] public float distance;
        [Tooltip("+1 the left bank, -1 the right, for things on a bank (0 for none).")] public float side;
        [Tooltip("Metres across from the middle (positive the left), for things in the water.")] public float across;
        [Tooltip("Seconds round its timetable.")] [Min(0f)] public float period;
        [Tooltip("Seconds into its timetable at the race clock's zero.")] public float offset;
        [Tooltip("How big, how far or how many, as the kind reads it.")] public float size;

        public ObstacleSpot(ObstacleKind kind, float distance, float side = 0f, float across = 0f, float period = 4f, float offset = 0f, float size = 0f)
        {
            this.kind = kind;
            this.distance = distance;
            this.side = side;
            this.across = across;
            this.period = period;
            this.offset = offset;
            this.size = size;
        }
    }

    /// <summary>A boost lane: glowing chevrons on the water that give a raft riding them a surge of speed. Dealt with the round.</summary>
    [Serializable]
    public struct BoostSpot
    {
        [Tooltip("Where it starts, down the river.")] public float distance;
        [Tooltip("Its middle, across the river (positive is the left).")] public float across;

        public BoostSpot(float distance, float across)
        {
            this.distance = distance;
            this.across = across;
        }
    }

    /// <summary>The kinds of obstacle a course can be dealt each round (see <see cref="CourseDef.menu"/>).</summary>
    public enum ObstacleKind
    {
        Wheel,
        Crocodile,
        SwingingLog,
        Geyser,
        Rockslide,
        Hippo,
        Sluice,
        Sweeper,
        Cannon,
        Rapids,
        Weir,
        Bumper,
        Ferry,
        Bear,
        FallingTree,
        Turtle,
        PiranhaShoal,
        Waterspout,
        ElectricEels,
        Mudslide,
        IceFloes,
        BeaverDam,
        Pelican,
        NetBoat,
        Kraken,
        TidalBore,
        JumpRamp,
        GooseGang,
        ChainedMines,
        WreckingBall,
        StormCloud,
        PaddleSteamer,
        MillRace,
        Drawbridge,
        CargoCrane,
        LogFlume,
        CalvingGlacier,
        DynamiteFloat,
        LeapingFrogs,
        GiantFan,
    }

    /// <summary>One kind on a course's menu, and how often it comes up compared with the others.</summary>
    [Serializable]
    public struct ObstacleChance
    {
        public ObstacleKind kind;
        [Min(0f)] public float weight;

        public ObstacleChance(ObstacleKind kind, float weight = 1f)
        {
            this.kind = kind;
            this.weight = weight;
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

        /// <summary>Snowfields, grey-blue rock, dark pines and turquoise meltwater.</summary>
        Glacier,

        /// <summary>Red sandstone walls, desert scrub and green-brown water.</summary>
        Canyon,
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

        [Header("Obstacles dealt each round")]
        [Tooltip("The kinds of obstacle this course deals a fresh random layout from every round, and how often each comes up. " +
                 "Empty: the fixed lists below are the layout, every time.")]
        public ObstacleChance[] menu = Array.Empty<ObstacleChance>();
        [Min(0)] public int minObstacles = 6;
        [Min(0)] public int maxObstacles = 9;

        [Header("Obstacles (a fixed layout, when the menu is empty)")]
        public WheelSpot[] wheels = Array.Empty<WheelSpot>();
        public CrocSpot[] crocodiles = Array.Empty<CrocSpot>();
        public SwingSpot[] swingers = Array.Empty<SwingSpot>();
        public GeyserSpot[] geysers = Array.Empty<GeyserSpot>();
        public RockslideSpot[] rockslides = Array.Empty<RockslideSpot>();
        public HippoSpot[] hippos = Array.Empty<HippoSpot>();
        public SluiceSpot[] sluices = Array.Empty<SluiceSpot>();
        public SweeperSpot[] sweepers = Array.Empty<SweeperSpot>();
        public CannonSpot[] cannons = Array.Empty<CannonSpot>();
        public RapidsSpot[] rapids = Array.Empty<RapidsSpot>();
        public WeirSpot[] weirs = Array.Empty<WeirSpot>();
        public BumperSpot[] bumpers = Array.Empty<BumperSpot>();
        public FerrySpot[] ferries = Array.Empty<FerrySpot>();
        public BearSpot[] bears = Array.Empty<BearSpot>();
        public SideSpot[] trees = Array.Empty<SideSpot>();
        public CrossingSpot[] turtles = Array.Empty<CrossingSpot>();
        public CrossingSpot[] shoals = Array.Empty<CrossingSpot>();
        public CrossingSpot[] waterspouts = Array.Empty<CrossingSpot>();
        public AcrossSpot[] eels = Array.Empty<AcrossSpot>();
        public SideSpot[] mudslides = Array.Empty<SideSpot>();
        public AcrossSpot[] floes = Array.Empty<AcrossSpot>();
        public AcrossSpot[] dams = Array.Empty<AcrossSpot>();
        public SideSpot[] pelicans = Array.Empty<SideSpot>();
        public CrossingSpot[] netBoats = Array.Empty<CrossingSpot>();
        [Tooltip("The newer obstacles (kraken, tidal bore, jump ramp...), each with its kind.")] public ObstacleSpot[] extras = Array.Empty<ObstacleSpot>();
        public BoostSpot[] boosts = Array.Empty<BoostSpot>();

        /// <summary>Whether every round deals a fresh random layout (otherwise the fixed lists are the layout).</summary>
        public bool HasMenu => menu != null && menu.Length > 0;

        public float FinishDistance => gates.Length > 0 ? gates[^1] : 0f;

        /// <summary>A rough time for a decent crew (3 m/s on average): the AI tests give each skill a multiple of it.</summary>
        public float ExpectedSeconds => FinishDistance / 6f;

        /// <summary>Stars for a run: one for each life left at the finish (any layout is fair that way); none without finishing.</summary>
        public static int Stars(bool finished, int livesLeft) => finished ? Mathf.Clamp(livesLeft, 0, 3) : 0;
    }
}
