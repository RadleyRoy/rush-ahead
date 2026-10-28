using System;

namespace RushAhead.UI
{
    /// <summary>Reads <c>-flag value</c> pairs from the player's command line.</summary>
    static class CommandLine
    {
        static readonly string[] Args = Environment.GetCommandLineArgs();

        public static bool Has(string flag) => Array.IndexOf(Args, flag) >= 0;

        /// <summary>The value after <paramref name="flag"/>, or null if it's absent or followed by another flag.</summary>
        public static string After(string flag)
        {
            var i = Array.IndexOf(Args, flag);
            return i >= 0 && i + 1 < Args.Length && !Args[i + 1].StartsWith("-") ? Args[i + 1] : null;
        }
    }
}
