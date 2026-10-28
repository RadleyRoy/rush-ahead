namespace RushAhead.UI
{
    /// <summary>Whether a game is on, and what kind.</summary>
    public enum SessionKind
    {
        /// <summary>At the menus.</summary>
        None,

        /// <summary>This device alone, with AI crewmates.</summary>
        Solo,
        Hosting,
        Joined,
    }
}
