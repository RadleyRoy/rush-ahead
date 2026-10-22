using System;
using NUnit.Framework;
using RushAhead.Net;

namespace RushAhead.Tests
{
    /// <summary>
    /// The parts of online play that don't need Unity's servers. Hosting and joining through Relay itself is checked
    /// by hand with two devices, since it needs the build linked to a Unity Cloud project and a real internet connection.
    /// </summary>
    public class OnlineRoomsTests
    {
        [TestCase("BQ7K2X", "BQ7K2X")]
        [TestCase(" bq7k2x ", "BQ7K2X")]
        [TestCase("bq7-k2x", "BQ7K2X")]
        [TestCase("BQ7 K2X\n", "BQ7K2X")]
        [TestCase("", "")]
        [TestCase(null, "")]
        public void JoinCodes_ForgiveHowTheyWereTyped(string typed, string expected) =>
            Assert.That(OnlineRooms.CleanCode(typed), Is.EqualTo(expected));

        [Test]
        public void AnEmptyCode_IsExplainedPlainly()
        {
            var e = Assert.ThrowsAsync<ArgumentException>(async () => await OnlineRooms.JoinAsync("  - "));
            Assert.That(OnlineRooms.Explain(e), Does.Contain("join code"));
        }

        [Test]
        public void AnythingUnexpected_StillGivesASentence() =>
            Assert.That(OnlineRooms.Explain(new InvalidOperationException("boom")), Does.StartWith("Online play failed"));
    }
}
