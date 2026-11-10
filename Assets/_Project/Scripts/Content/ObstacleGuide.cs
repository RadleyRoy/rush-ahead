using System.Collections.Generic;
using System.Linq;

namespace RushAhead.Content
{
    /// <summary>What to do about something on the river: the word shown when it's coming up.</summary>
    public enum Counter
    {
        Brace,
        Dodge,
        TimeIt,
        GoRound,
        BlowAway,
        RideIt,
        Use,
    }

    /// <summary>One page of the Field Guide: an obstacle, or something else on the river worth knowing.</summary>
    public sealed class GuideEntry
    {
        /// <summary>Stable name for its picture (<c>Guide/&lt;Id&gt;</c>) and for what the player has seen.</summary>
        public readonly string Id;
        public readonly string Name;
        /// <summary>The obstacle it describes, or null for a course feature (rocks, currents, boost lanes...).</summary>
        public readonly ObstacleKind? Kind;
        public readonly string What;
        public readonly string Beat;
        public readonly Counter Counter;

        public GuideEntry(string id, string name, ObstacleKind? kind, Counter counter, string what, string beat)
        {
            Id = id;
            Name = name;
            Kind = kind;
            Counter = counter;
            What = what;
            Beat = beat;
        }

        /// <summary>The word for the HUD: what to do as it comes up.</summary>
        public string CounterWord => Words(Counter);

        public static string Words(Counter counter) => counter switch
        {
            Counter.Brace => "Brace",
            Counter.Dodge => "Dodge",
            Counter.TimeIt => "Time it",
            Counter.GoRound => "Go round",
            Counter.BlowAway => "Blow it away",
            Counter.RideIt => "Ride it",
            _ => "Use it",
        };
    }

    /// <summary>
    /// The Field Guide's text: every obstacle and every other thing on the river, what it does and how to beat it. The
    /// in-game guide, the warnings during a race and the web page (<c>docs/FIELD_GUIDE.md</c>) all read it from here.
    /// </summary>
    public static class ObstacleGuide
    {
        static readonly GuideEntry[] entries =
        {
            // The river itself.
            new("Edge", "The edge", null, Counter.GoRound,
                "The river runs along the top of a ridge with no banks to stop you. Drift past the stone lip and over you go.",
                "Keep off the lip, especially on bends: the faster you go, the wider you swing."),
            new("Current", "Currents", null, Counter.Use,
                "Streaks of fast water. Arrows show which way they run: some speed you on, some push you sideways.",
                "Ride the ones going your way; cut across the others at an angle."),
            new("Whirlpool", "Whirlpool", null, Counter.GoRound,
                "A funnel of spinning water that drags rafts round and in towards its middle.",
                "Skirt the rim on the side turning your way, or blow hard straight out of it."),
            new("BoostLane", "Boost lane", null, Counter.Use,
                "Glowing chevrons on the water. Ride over them for a big surge of speed.",
                "Line up early. They never send you faster than the next bend allows."),
            new("Rock", "Rocks", null, Counter.GoRound,
                "Boulders standing in the river. Solid: a raft that hits one stops dead and wobbles.",
                "Steer round; bounce off the foam ring, not the stone."),
            new("Barrel", "Barrels", null, Counter.BlowAway,
                "Loose barrels bobbing in the current. They bump you about.",
                "Blow them out of the way with your air."),
            new("DriftingLog", "Drifting logs", null, Counter.BlowAway,
                "Loose logs drifting with the water, turning as they go.",
                "Blow them aside, or slip past their ends."),

            // The first four.
            new("Wheel", "Paddle wheel", ObstacleKind.Wheel, Counter.TimeIt,
                "A big wooden wheel on a post, its paddles sweeping round across the river.",
                "Pass between paddles, on the side sweeping the way you're going."),
            new("Crocodile", "Crocodile", ObstacleKind.Crocodile, Counter.Dodge,
                "Lurks by the bank, then lunges across the river with its jaws open. A bite shoves and heels the raft.",
                "Watch it coil back, then go behind it or wait for it to swim home. Bracing softens a bite."),
            new("SwingingLog", "Swinging log", ObstacleKind.SwingingLog, Counter.Brace,
                "A log hanging from a rope bridge, swinging across the river. Low enough to hit the crew at the bottom of its swing.",
                "Brace together as it comes over: a braced crew ducks under it."),
            new("Geyser", "Geyser", ObstacleKind.Geyser, Counter.Brace,
                "A vent in the riverbed. Bubbles rise, then it blasts a column of water that throws a raft up and outward.",
                "Avoid the bubbling ring, or brace if you're on it when it goes."),

            new("Rockslide", "Rockslide", ObstacleKind.Rockslide, Counter.TimeIt,
                "A crag on the bank with boulders balanced on top. One teeters, rolls down the scree chute and across the river.",
                "Watch the top of the crag: when a boulder rocks, hang back or race past. Bracing helps a little."),
            new("Hippo", "Hippo", ObstacleKind.Hippo, Counter.Brace,
                "Bubbles and two little ears, then a hippo heaves up under the water. Anything on top goes flying.",
                "Steer round the bubbles; brace if you can't."),
            new("Sluice", "Sluice gate", ObstacleKind.Sluice, Counter.TimeIt,
                "A timber wall across the river with one opening that slides from side to side.",
                "Aim for where the gap is going, not where it is."),
            new("Sweeper", "Sweeper", ObstacleKind.Sweeper, Counter.TimeIt,
                "A long log on a post in the river, spinning flat across the water.",
                "Go through behind the sweep, or round the short side."),
            new("Cannon", "Water cannon", ObstacleKind.Cannon, Counter.GoRound,
                "A cannon on the bank sweeps a jet of water across the river, shoving rafts towards the far edge.",
                "Its aim line shows on the water before it fires. Pass behind the jet, or blow back against it."),
            new("Rapids", "Rapids", ObstacleKind.Rapids, Counter.RideIt,
                "White water: boiling foam and standing waves that kick the raft about.",
                "Go straight through and brace in the biggest waves."),
            new("Weir", "Weir", ObstacleKind.Weir, Counter.Brace,
                "A step down in the river. You speed up over the lip and land hard below.",
                "Square up and brace for the landing."),
            new("Bumper", "Bumper buoys", ObstacleKind.Bumper, Counter.GoRound,
                "Springy buoys that bounce a raft away hard, sometimes towards the edge.",
                "Give them room, and never bounce off one towards the lip."),
            new("Ferry", "Ferry", ObstacleKind.Ferry, Counter.TimeIt,
                "A heavy cable ferry crossing back and forth. Solid and slow.",
                "Pass behind it, never across its bow."),
            new("Bear", "Bear", ObstacleKind.Bear, Counter.GoRound,
                "A bear on the bank swipes at rafts passing close, flinging them across the river.",
                "Keep to the far side when you pass it."),
            new("FallingTree", "Falling tree", ObstacleKind.FallingTree, Counter.TimeIt,
                "A dead tree on the bank creaks, shakes and crashes across the river once, as the first rafts come. Then it lies there with a gap past its crown.",
                "If it's shaking, wait or race under it braced; once it's down, aim for the gap at its top."),
            new("Turtle", "Snapping turtle", ObstacleKind.Turtle, Counter.GoRound,
                "A huge turtle paddling slowly across. Solid, and it snaps at rafts that come close, setting them spinning.",
                "Pass well behind it."),
            new("PiranhaShoal", "Piranha shoal", ObstacleKind.PiranhaShoal, Counter.GoRound,
                "A reddish churn full of fins drifting down the river. Inside it you're nibbled: slowed and wobbled.",
                "Steer round it; it moves with the water."),
            new("Waterspout", "Waterspout", ObstacleKind.Waterspout, Counter.Dodge,
                "A spinning column of spray under a dark cloud, wandering across the river. It drags rafts in and flings them out.",
                "Keep away from its foot; brace if it catches you."),
            new("ElectricEels", "Electric eels", ObstacleKind.ElectricEels, Counter.TimeIt,
                "Eels in a strip across the river. The water crackles blue, then pulses: blowers cut out for a second.",
                "Cross between pulses, or brace through it."),
            new("Mudslide", "Mudslide", ObstacleKind.Mudslide, Counter.GoRound,
                "Brown slurry pours off a slope on the bank, shoving rafts across and slowing them.",
                "Hug the far side while it flows."),
            new("IceFloes", "Ice floes", ObstacleKind.IceFloes, Counter.GoRound,
                "Big flat slabs of ice riding the current in a zig-zag. Solid: they block and squeeze.",
                "Watch the gaps open and slip through."),
            new("BeaverDam", "Beaver dam", ObstacleKind.BeaverDam, Counter.TimeIt,
                "A dam across the river with three gaps the beavers open and shut in turn.",
                "Head for the gap that's opening next."),
            new("Pelican", "Diving pelican", ObstacleKind.Pelican, Counter.Brace,
                "A pelican circles high, its shadow marks the spot, then it plunges. A hit knocks the raft over.",
                "Steer off the shadow, or brace."),
            new("NetBoat", "Net boat", ObstacleKind.NetBoat, Counter.GoRound,
                "A fishing boat crosses dragging a net. Snagged rafts are dragged along with it.",
                "Pass behind the net, not between the boat and its float."),

            new("Kraken", "Kraken", ObstacleKind.Kraken, Counter.Dodge,
                "Rings of ripples mark where giant tentacles will rise, then they slam down on the water.",
                "Steer clear of the rings; brace if a tentacle's coming down on you."),
            new("TidalBore", "Tidal bore", ObstacleKind.TidalBore, Counter.Brace,
                "A wall of white water rolls up the river towards you, right across it.",
                "Meet it square and braced, and you'll ride over it."),
            new("JumpRamp", "Jump ramp", ObstacleKind.JumpRamp, Counter.Use,
                "A log ramp in the river. Hit it fast and the raft flies, sailing over what's below.",
                "Line up, go flat out, and brace for the landing."),
            new("GooseGang", "Goose gang", ObstacleKind.GooseGang, Counter.BlowAway,
                "Angry geese paddle out from the bank and chase rafts, pecking: you slow down and wobble.",
                "Blow them away with your air."),
            new("ChainedMines", "Chained mines", ObstacleKind.ChainedMines, Counter.Dodge,
                "Old spiked mines bobbing on chains. They blink faster as you come close and go off if touched.",
                "Thread between them; their chains keep them in place."),
            new("WreckingBall", "Wrecking ball", ObstacleKind.WreckingBall, Counter.TimeIt,
                "A huge iron ball swinging down and up the river from a gantry.",
                "Go under the gantry just after the ball swings away from you."),
            new("StormCloud", "Storm cloud", ObstacleKind.StormCloud, Counter.Dodge,
                "A black cloud drifting over the river. A glowing ring marks each lightning strike a second before it hits: blowers cut out.",
                "Steer out of the glowing rings."),
            new("PaddleSteamer", "Paddle steamer", ObstacleKind.PaddleSteamer, Counter.GoRound,
                "A big paddle boat churning along one half of the river. Its paddles knock and its wake rocks rafts behind.",
                "Pass on the other half, well clear of the paddles."),
            new("MillRace", "Mill race", ObstacleKind.MillRace, Counter.BlowAway,
                "A mill on the bank draws a strong side current towards its turning wheel.",
                "Keep to the far side and blow against the pull."),
            new("Drawbridge", "Drawbridge", ObstacleKind.Drawbridge, Counter.TimeIt,
                "Two bridge leaves lower from the banks to meet over the river, then lift. Bells and lights warn first.",
                "Go through while it's up, or through the gap as it opens."),
            new("CargoCrane", "Cargo crane", ObstacleKind.CargoCrane, Counter.Dodge,
                "A crane on the bank swings out over the river and drops crates. Their shadows show where.",
                "Steer off the shadows."),
            new("LogFlume", "Log flume", ObstacleKind.LogFlume, Counter.Dodge,
                "A sawmill sends logs down a wooden chute into the river, where they drift down loose.",
                "Watch the chute and pass between logs; blow them aside."),
            new("CalvingGlacier", "Calving glacier", ObstacleKind.CalvingGlacier, Counter.Brace,
                "An ice cliff on the bank cracks and drops a block. The splash sends a wave across the river and the block floats off.",
                "Brace for the wave, then steer round the block."),
            new("DynamiteFloat", "Dynamite float", ObstacleKind.DynamiteFloat, Counter.GoRound,
                "A crate of dynamite drifting down with its fuse fizzing. When the fuse runs out, it blows.",
                "Keep well away from it, or blow it away from you."),
            new("LeapingFrogs", "Leaping frogs", ObstacleKind.LeapingFrogs, Counter.TimeIt,
                "Giant frogs hop from lily pad to lily pad across the river. Each landing splashes rafts aside.",
                "Pass between hops; watch which pad they crouch towards."),
            new("GiantFan", "Giant fan", ObstacleKind.GiantFan, Counter.BlowAway,
                "A huge fan on the bank blows gusts across the river. The windsock shows when.",
                "Blow back into it, or brace and hold your line."),
        };

        static readonly Dictionary<ObstacleKind, GuideEntry> byKind = new();
        static readonly Dictionary<string, GuideEntry> byId = new();

        static ObstacleGuide()
        {
            foreach (var entry in entries)
            {
                byId[entry.Id] = entry;
                if (entry.Kind is { } kind) byKind[kind] = entry;
            }
        }

        /// <summary>Every page, the river's features first, then the obstacles in the order they were added.</summary>
        public static IReadOnlyList<GuideEntry> Entries => entries;

        public static GuideEntry For(ObstacleKind kind) => byKind.TryGetValue(kind, out var entry) ? entry : null;

        public static GuideEntry ById(string id) => id != null && byId.TryGetValue(id, out var entry) ? entry : null;

        /// <summary>Whether this can turn up on a course: an obstacle on its menu, or a feature its river has.</summary>
        public static bool Appears(GuideEntry entry, CourseDef course)
        {
            if (entry == null || course == null) return false;
            if (entry.Kind is { } kind) return course.menu != null && course.menu.Any(c => c.kind == kind && c.weight > 0f);
            return entry.Id switch
            {
                "Current" => course.currents is { Length: > 0 },
                "Whirlpool" => course.whirlpools is { Length: > 0 },
                "Rock" => course.rocks is { Length: > 0 },
                "Barrel" => course.barrels is { Length: > 0 },
                "DriftingLog" => course.logs is { Length: > 0 },
                _ => true, // the edge, and boost lanes (dealt every round)
            };
        }
    }
}
