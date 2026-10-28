using System.Collections.Generic;
using NUnit.Framework;
using RushAhead.Content;
using RushAhead.UI;
using UnityEngine;

namespace RushAhead.Tests
{
    public class TouchControlsTests
    {
        const float Width = 2000f;
        TouchControls touch;
        readonly List<TouchSample> fingers = new();
        double now;

        [SetUp]
        public void SetUp()
        {
            touch = new TouchControls { ScreenWidth = Width, StickRadius = 100f };
            touch.AddButton(TouchControls.BurstButton, new Rect(1800f, 0f, 200f, 200f));
            touch.AddButton(TouchControls.BraceButton, new Rect(1550f, 0f, 200f, 200f));
            touch.AddButton("pause", new Rect(0f, 900f, 100f, 100f));
            fingers.Clear();
            now = 0;
        }

        void Down(int id, Vector2 at) => fingers.Add(new TouchSample { Id = id, Start = at, Position = at, StartTime = now, Time = now });

        void Move(int id, Vector2 to)
        {
            var i = fingers.FindIndex(f => f.Id == id);
            var f = fingers[i];
            f.Position = to;
            f.Time = now;
            fingers[i] = f;
        }

        void Up(int id)
        {
            var i = fingers.FindIndex(f => f.Id == id);
            var f = fingers[i];
            f.Ended = true;
            f.Time = now;
            fingers[i] = f;
        }

        void Frame()
        {
            now += 1.0 / 60.0;
            touch.Update(fingers);
            fingers.RemoveAll(f => f.Ended);
        }

        [Test]
        public void TheLeftThumbSteers_WhileTheRightPressesBurstAndHoldsBrace()
        {
            Down(1, new Vector2(300f, 300f));
            Frame();
            Move(1, new Vector2(300f, 400f));
            Down(2, new Vector2(1900f, 100f));
            Frame();
            Assert.That(touch.Stick[0], Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(touch.BurstPressed[0], Is.True, "burst fires as the button goes down");

            Frame();
            Assert.That(touch.BurstPressed[0], Is.False, "once per press");
            Up(2);
            Down(3, new Vector2(1600f, 100f));
            Frame();
            Frame();
            Assert.That(touch.BraceHeld, Is.True);
            Assert.That(touch.Stick[0].y, Is.EqualTo(1f), "still steering throughout");
            Up(3);
            Frame();
            Assert.That(touch.BraceHeld, Is.False);
        }

        [Test]
        public void AFingerBelongsToWhatItFirstTouched()
        {
            Down(1, new Vector2(1900f, 100f)); // on Burst
            Frame();
            Move(1, new Vector2(900f, 500f)); // slides off, over the stick's half
            Frame();
            Assert.That(touch.StickActive[0], Is.False, "sliding off a button never steers");

            Down(2, new Vector2(1200f, 600f)); // the right half: no stick there in the one-stick layout
            Frame();
            Assert.That(touch.StickActive[0], Is.False);
        }

        [Test]
        public void AFingerThatWentDownWhileBlocked_NeverBecomesAStick()
        {
            touch.Blocked = true;
            Down(1, new Vector2(300f, 300f)); // tapping Resume on the pause menu
            Frame();
            touch.Blocked = false;
            Move(1, new Vector2(300f, 500f));
            Frame();
            Assert.That(touch.StickActive[0], Is.False);
        }

        [Test]
        public void PauseAndOrders_AreTappedOnce()
        {
            Down(1, new Vector2(50f, 950f));
            Frame();
            Assert.That(touch.Tapped, Is.EqualTo(new[] { "pause" }));
            Frame();
            Assert.That(touch.Tapped, Is.Empty);
        }

        [Test]
        public void PushHere_TakesTheNextTapOffTheButtons()
        {
            Vector2? point = null;
            touch.CaptureNextTap(p => point = p);
            Down(1, new Vector2(800f, 700f));
            Frame();
            Assert.That(point, Is.EqualTo(new Vector2(800f, 700f)));
            Assert.That(touch.StickActive[0], Is.False, "the tap isn't a stick");
            Assert.That(touch.Capturing, Is.False);
        }

        [Test]
        public void TwoSticks_UseBothHalves_AndAQuickTapBursts()
        {
            touch.TwoSticks = true;
            touch.ClearButtons();
            Down(1, new Vector2(300f, 300f));
            Down(2, new Vector2(1700f, 300f));
            Frame();
            Move(2, new Vector2(1600f, 300f));
            Frame();
            Assert.That(touch.StickActive[0] && touch.StickActive[1], Is.True);
            Assert.That(touch.Stick[1], Is.EqualTo(new Vector2(-1f, 0f)));

            Up(1);
            Frame();
            Assert.That(touch.BurstPressed[0], Is.True, "a quick tap on a stick bursts");
        }

        [Test]
        public void LeftHanded_PutsTheStickOnTheRight()
        {
            touch.LeftHanded = true;
            Down(1, new Vector2(300f, 300f));
            Frame();
            Assert.That(touch.StickActive[0], Is.False);
            Down(2, new Vector2(1200f, 500f));
            Frame();
            Assert.That(touch.StickActive[0], Is.True);
        }

        [Test]
        public void AFingerLiftedUnseen_IsForgotten()
        {
            Down(1, new Vector2(300f, 300f));
            Frame();
            fingers.Clear(); // the end was never reported
            Frame();
            Down(2, new Vector2(400f, 400f));
            Frame();
            Assert.That(touch.StickActive[0], Is.True, "the stick is free again");
        }
    }

    public class BlowerStatsTests
    {
        static BlowerDef Blower(float thrust, float spool, float range, float burst, BlowerSpecial special = BlowerSpecial.None)
        {
            var b = ScriptableObject.CreateInstance<BlowerDef>();
            b.thrustNewtons = thrust;
            b.spoolUpMs = spool;
            b.range = range;
            b.burstImpulse = burst;
            b.special = special;
            return b;
        }

        [Test]
        public void EveryBar_StaysBetweenZeroAndOne()
        {
            foreach (var b in new[] { Blower(45f, 260f, 4.5f, 60f), Blower(999f, 1f, 99f, 999f), Blower(0f, 260f, 3f, 110f) })
            {
                var s = new BlowerStats(b);
                foreach (var bar in new[] { s.Power, s.QuickStart, s.Reach, s.Burst }) Assert.That(bar, Is.InRange(0f, 1f));
                Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void TheBellows_HasNoSteadyPower_AndTheBiggestBurst()
        {
            var bellows = Blower(0f, 260f, 3f, 110f);
            var leaf = Blower(45f, 260f, 4.5f, 60f);
            Assert.That(new BlowerStats(bellows).Power, Is.Zero);
            Assert.That(new BlowerStats(bellows).QuickStart, Is.Zero);
            Assert.That(new BlowerStats(bellows).Burst, Is.GreaterThan(new BlowerStats(leaf).Burst));
            Assert.That(BlowerStats.Tag(bellows), Is.EqualTo("Bursts only"));
            Assert.That(BlowerStats.Tag(leaf), Is.Null);
            Object.DestroyImmediate(bellows);
            Object.DestroyImmediate(leaf);
        }

        [Test]
        public void TheShopVac_PullsAndHasNoBurst()
        {
            var vac = Blower(40f, 400f, 5f, 0f, BlowerSpecial.Vacuum);
            Assert.That(new BlowerStats(vac).Burst, Is.Zero);
            Assert.That(BlowerStats.Tag(vac), Is.EqualTo("Pulls instead"));
            Object.DestroyImmediate(vac);
        }
    }
}
