using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>One of the newer obstacles, which all sit on an <see cref="ObstacleSpot"/>.</summary>
    public interface ISpotted
    {
        ObstacleSpot Spot { get; }
    }

    /// <summary>Builds the obstacle for one of a layout's <see cref="ObstacleSpot"/>s (the kinds from Phase 17 on).</summary>
    public static class NewObstacles
    {
        public static Obstacle Create(Course course, Transform parent, ObstacleSpot spot) => spot.kind switch
        {
            ObstacleKind.Kraken => new Kraken(course, spot),
            ObstacleKind.TidalBore => new TidalBore(course, spot),
            ObstacleKind.JumpRamp => new JumpRamp(course, spot),
            ObstacleKind.GooseGang => new GooseGang(course, parent, spot),
            ObstacleKind.ChainedMines => new ChainedMines(course, spot),
            ObstacleKind.WreckingBall => new WreckingBall(course, spot),
            ObstacleKind.StormCloud => new StormCloud(course, spot),
            ObstacleKind.PaddleSteamer => new PaddleSteamer(course, parent, spot),
            ObstacleKind.MillRace => new MillRace(course, parent, spot),
            ObstacleKind.Drawbridge => new Drawbridge(course, parent, spot),
            ObstacleKind.CargoCrane => new CargoCrane(course, spot),
            ObstacleKind.LogFlume => new LogFlume(course, spot),
            ObstacleKind.CalvingGlacier => new CalvingGlacier(course, spot),
            ObstacleKind.DynamiteFloat => new DynamiteFloat(course, spot),
            ObstacleKind.LeapingFrogs => new LeapingFrogs(course, spot),
            ObstacleKind.GiantFan => new GiantFan(course, spot),
            _ => null,
        };

        /// <summary>Whether a kind is one of these (it goes in a layout's <c>extras</c>).</summary>
        public static bool Covers(ObstacleKind kind) => kind >= ObstacleKind.Kraken;
    }
}
