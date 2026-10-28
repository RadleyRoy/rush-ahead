using NUnit.Framework;
using RushAhead.UI;

namespace RushAhead.Tests
{
    public class ResultsRuleTests
    {
        const int Rate = 60;

        [Test]
        public void CoOp_ShowsTheResultsAMomentAfterTheFinish()
        {
            Assert.That(ResultsRule.Due(new[] { 0 }, 5000, Rate), Is.False, "still racing");
            Assert.That(ResultsRule.Due(new[] { 1000 }, 1000 + Rate * 2, Rate), Is.False, "too soon to see themselves finish");
            Assert.That(ResultsRule.Due(new[] { 1000 }, 1000 + (int)(Rate * 2.5f), Rate), Is.True);
        }

        [Test]
        public void TeamRace_WaitsForBothRafts_ButNotForever()
        {
            Assert.That(ResultsRule.Due(new[] { 1000, 0 }, 1000 + Rate * 10, Rate), Is.False, "the other raft is still coming");
            Assert.That(ResultsRule.Due(new[] { 1000, 1300 }, 1300 + Rate * 3, Rate), Is.True, "both home");
            Assert.That(ResultsRule.Due(new[] { 0, 1000 }, 1000 + Rate * 30, Rate), Is.True, "the loser can't hold the room");
        }

        [Test]
        public void NobodyRacing_NeverShowsResults()
        {
            Assert.That(ResultsRule.Due(new int[0], 99999, Rate), Is.False);
            Assert.That(ResultsRule.Due(new[] { 0, 0 }, 99999, Rate), Is.False);
        }
    }

    public class RunTrackerTests
    {
        [Test]
        public void RecordsEachCheckpoint_AndComparesItWithTheBestRun()
        {
            var tracker = new RunTracker();
            var best = new[] { 20f, 45f, 90f };

            tracker.Update(100, 0, 5f, best);
            Assert.That(tracker.LastGate, Is.Zero);
            Assert.That(tracker.LastDelta, Is.Null);

            tracker.Update(100, 1, 19.2f, best);
            Assert.That(tracker.LastGate, Is.EqualTo(1));
            Assert.That(tracker.LastDelta, Is.EqualTo(-0.8f).Within(1e-4f), "faster than the best at the first checkpoint");

            tracker.Update(100, 1, 30f, best);
            Assert.That(tracker.Splits, Has.Count.EqualTo(1), "one split per checkpoint");

            tracker.Update(100, 2, 46.5f, best);
            Assert.That(tracker.LastDelta, Is.EqualTo(1.5f).Within(1e-4f));
            Assert.That(tracker.Splits, Is.EqualTo(new[] { 19.2f, 46.5f }));
        }

        [Test]
        public void ANewStart_StartsAFreshRun()
        {
            var tracker = new RunTracker();
            tracker.Update(100, 2, 40f, null);
            tracker.Update(900, 0, 0f, null);
            Assert.That(tracker.LastGate, Is.Zero);
            Assert.That(tracker.Splits, Is.Empty);
        }

        [Test]
        public void WithoutABestRun_ThereIsNothingToCompare()
        {
            var tracker = new RunTracker();
            tracker.Update(100, 1, 20f, new float[0]);
            Assert.That(tracker.LastGate, Is.EqualTo(1));
            Assert.That(tracker.LastDelta, Is.Null);
        }
    }
}
