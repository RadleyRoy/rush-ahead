namespace RushAhead.Sim
{
    /// <summary>
    /// What the players have told their AI crewmates to do. Four is deliberate: each has an obvious use, they fit on
    /// one thumb, and a child can tell them apart. Any player may give one; the host's AI carries it out.
    /// </summary>
    public enum CrewOrder : byte
    {
        /// <summary>Push the raft along: down the racing line on a course, or the way the players are pushing.</summary>
        Go = 0,

        /// <summary>Stop blowing and refill the tanks, until told to go again.</summary>
        Hold = 1,

        /// <summary>Stop blowing and steady the raft for a few seconds, then carry on.</summary>
        Brace = 2,

        /// <summary>Push the raft toward a point the player tapped, until it gets there, then carry on.</summary>
        PushHere = 3,
    }
}
