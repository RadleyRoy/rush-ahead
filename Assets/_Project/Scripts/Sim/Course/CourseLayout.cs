using System;
using RushAhead.Content;

namespace RushAhead.Sim
{
    /// <summary>
    /// Where a round's obstacles are: the same lists a <see cref="CourseDef"/> can hold, dealt fresh each round by the
    /// <see cref="LayoutGenerator"/> from a seed (which is all that goes over the network), or copied from the course's
    /// fixed lists when it has no menu.
    /// </summary>
    public sealed class CourseLayout
    {
        /// <summary>The seed it was dealt from; 0 for a fixed layout.</summary>
        public int Seed;

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

        /// <summary>How many obstacles in all.</summary>
        public int Count => wheels.Length + crocodiles.Length + swingers.Length + geysers.Length + rockslides.Length + hippos.Length +
                            sluices.Length + sweepers.Length + cannons.Length + rapids.Length + weirs.Length + bumpers.Length + ferries.Length + bears.Length +
                            trees.Length + turtles.Length + shoals.Length + waterspouts.Length + eels.Length + mudslides.Length + floes.Length +
                            dams.Length + pelicans.Length + netBoats.Length;

        /// <summary>A course's own fixed lists, as a layout.</summary>
        public static CourseLayout Fixed(CourseDef def) => new()
        {
            wheels = def.wheels, crocodiles = def.crocodiles, swingers = def.swingers, geysers = def.geysers, rockslides = def.rockslides,
            hippos = def.hippos, sluices = def.sluices, sweepers = def.sweepers, cannons = def.cannons, rapids = def.rapids, weirs = def.weirs,
            bumpers = def.bumpers, ferries = def.ferries, bears = def.bears, trees = def.trees, turtles = def.turtles, shoals = def.shoals,
            waterspouts = def.waterspouts, eels = def.eels, mudslides = def.mudslides, floes = def.floes, dams = def.dams, pelicans = def.pelicans,
            netBoats = def.netBoats,
        };
    }
}
