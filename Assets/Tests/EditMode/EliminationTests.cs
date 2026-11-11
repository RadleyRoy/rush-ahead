using System.Collections.Generic;
using NUnit.Framework;
using RushAhead.Sim;
using RushAhead.UI;

namespace RushAhead.Tests
{
    public class EliminationTests
    {
        readonly List<int> knocked = new();

        [Test]
        public void KnockoutGates_AreSpreadDownTheCourse_TheLastBeingTheFinish()
        {
            Assert.That(KnockoutRule.Gates(4, 6), Is.EqualTo(new[] { 2, 4, 6 }));
            Assert.That(KnockoutRule.Gates(3, 6), Is.EqualTo(new[] { 3, 6 }));
            Assert.That(KnockoutRule.Gates(4, 3), Is.EqualTo(new[] { 1, 2, 3 }), "one per checkpoint when they're few");
            Assert.That(KnockoutRule.Gates(2, 5), Is.EqualTo(new[] { 5 }));
            Assert.That(KnockoutRule.Gates(4, 2), Is.EqualTo(new[] { 1, 2, 2 }), "too few checkpoints: two at the finish");
            for (var gates = 3; gates <= 9; gates++)
            {
                var knockout = KnockoutRule.Gates(4, gates);
                for (var k = 1; k < knockout.Length; k++) Assert.That(knockout[k], Is.GreaterThan(knockout[k - 1]));
                Assert.That(knockout[^1], Is.EqualTo(gates));
            }
        }

        [Test]
        public void TheLastRaftThroughAKnockoutGate_IsOut()
        {
            // Four rafts, six checkpoints: only three may pass checkpoint 2.
            KnockoutRule.Decide(new[] { 2, 2, 1, 1 }, new[] { false, false, false, false }, 6, knocked);
            Assert.That(knocked, Is.Empty, "two through, two still to come");

            KnockoutRule.Decide(new[] { 2, 2, 2, 1 }, new[] { false, false, false, false }, 6, knocked);
            Assert.That(knocked, Is.EqualTo(new[] { 3 }));
        }

        [Test]
        public void ARaftOutOfLives_TakesTheKnockout_SoTheGateDoesNothing()
        {
            KnockoutRule.Decide(new[] { 2, 2, 2, 1 }, new[] { false, false, false, true }, 6, knocked);
            Assert.That(knocked, Is.Empty);

            // And the next gate (checkpoint 4) lets two through: with three left, the third is out once two are.
            KnockoutRule.Decide(new[] { 4, 4, 3, 1 }, new[] { false, false, false, true }, 6, knocked);
            Assert.That(knocked, Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public void AtTheFinish_TheFirstRaftHomeWins_AndTheRestAreOut()
        {
            KnockoutRule.Decide(new[] { 6, 5, 2, 1 }, new[] { false, false, true, true }, 6, knocked);
            Assert.That(knocked, Is.EqualTo(new[] { 1 }));
            Assert.That(KnockoutRule.Winner(new[] { 900, 0, 0, 0 }, new[] { false, true, true, true }), Is.EqualTo(0));
        }

        [Test]
        public void TheOnlyRaftLeft_Wins_FinishedOrNot()
        {
            Assert.That(KnockoutRule.Winner(new[] { 0, 0, 0, 0 }, new[] { true, false, true, true }), Is.EqualTo(1));
            Assert.That(KnockoutRule.Winner(new[] { 0, 0, 0, 0 }, new[] { false, false, true, true }), Is.EqualTo(-1), "two still going");
            Assert.That(KnockoutRule.Winner(new[] { 0, 0, 0 }, new[] { true, true, true }), Is.EqualTo(-1), "nobody left");
        }

        [Test]
        public void Places_FinishersFirst_ThenTheLastToGoOut()
        {
            var places = KnockoutRule.Places(new[] { 0, 1200, 0, 0 }, new[] { 900, 0, 500, 1190 }, new[] { 0.6f, 1f, 0.3f, 0.95f });
            Assert.That(places, Is.EqualTo(new[] { 1, 3, 0, 2 }));
        }

        [Test]
        public void TheRiver_RunsFaster_ForEveryRaftOut()
        {
            Assert.That(KnockoutRule.FlowBoost(4, 0), Is.EqualTo(1f));
            Assert.That(KnockoutRule.FlowBoost(4, 2), Is.EqualTo(1.24f).Within(1e-5f));
            Assert.That(KnockoutRule.FlowBoost(4, 9), Is.EqualTo(1.36f).Within(1e-5f), "at most once per raft but the last");
        }

        [Test]
        public void Elimination_ResultsCome_WhenOnlyOneRaftIsLeft()
        {
            const int rate = 60;
            var finishes = new[] { 0, 0, 0, 0 };
            var outs = new[] { 600, 0, 700, 800 };
            Assert.That(ResultsRule.Due(finishes, outs, 800 + rate * 3, rate), Is.False, "a Team Race waits for every raft");
            Assert.That(ResultsRule.Due(finishes, outs, 800 + rate, rate, lastOneStanding: true), Is.False, "a moment to take it in");
            Assert.That(ResultsRule.Due(finishes, outs, 800 + rate * 3, rate, lastOneStanding: true), Is.True);
            Assert.That(ResultsRule.Due(finishes, new[] { 600, 0, 0, 800 }, 800 + rate * 3, rate, lastOneStanding: true), Is.False, "two still racing");
        }

        [Test]
        public void ThreeOrFourRafts_StartInTwoRows()
        {
            Assert.That(Course.LaneAhead(0, 4), Is.EqualTo(Course.RowGap));
            Assert.That(Course.LaneAhead(3, 4), Is.EqualTo(0f));
            Assert.That(Course.LaneAhead(1, 2), Is.EqualTo(0f), "two rafts stay side by side");
            Assert.That(Course.LaneAhead(2, 3), Is.EqualTo(0f));
        }
    }
}
