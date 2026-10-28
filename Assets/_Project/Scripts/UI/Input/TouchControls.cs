using System;
using System.Collections.Generic;
using UnityEngine;

namespace RushAhead.UI
{
    /// <summary>One finger on the screen, as the Input System last reported it. Positions are in screen pixels, y up.</summary>
    public struct TouchSample
    {
        public int Id;
        public Vector2 Start;
        public Vector2 Position;
        public double StartTime;
        public double Time;
        public bool Ended;
    }

    /// <summary>
    /// The race's touch controls, without any Input System types so they can be tested: a floating stick on one half
    /// of the screen (both halves when the player drives two seats), and buttons (Burst, Brace, Pause, orders) whose
    /// areas the HUD reports each frame. Each finger belongs to whatever it first touched for as long as it stays down,
    /// so sliding off a button never steers, and a finger that went down while the controls were blocked (tapping
    /// Resume, say) never becomes a stick.
    /// UI Toolkit tracks only one shared finger, which isn't enough for steering with one thumb while tapping with the
    /// other; so in a race the HUD only draws, and every touch comes through here.
    /// </summary>
    public sealed class TouchControls
    {
        public const float TapSeconds = 0.2f;
        public const string BurstButton = "burst";
        public const string BraceButton = "brace";

        enum Use
        {
            Ignored,
            Stick,
            Button,
            Capture,
        }

        sealed class Claim
        {
            public Use Use;
            public int Stick;
            public string Button;
        }

        readonly Dictionary<int, Claim> claims = new();
        readonly List<(string id, Rect area)> buttons = new();
        readonly List<int> gone = new();
        Action<Vector2> capture;

        /// <summary>The player drives two seats from this device: a stick on each half, a quick tap on either bursts.</summary>
        public bool TwoSticks;

        /// <summary>One-stick layout: the stick on the right half (the buttons move left).</summary>
        public bool LeftHanded;

        public float StickRadius = 100f;
        public float ScreenWidth = 1920f;

        /// <summary>Menus, the pause menu or a dialog have the screen: new fingers are ignored for their whole life.</summary>
        public bool Blocked;

        // What the fingers say this frame.
        public readonly Vector2[] Stick = new Vector2[2];
        public readonly bool[] StickActive = new bool[2];
        public readonly Vector2[] StickOrigin = new Vector2[2];
        public readonly Vector2[] StickKnob = new Vector2[2];
        public readonly bool[] BurstPressed = new bool[2];
        public bool BraceHeld;

        /// <summary>Buttons pressed this frame (other than Burst and Brace).</summary>
        public readonly List<string> Tapped = new();

        /// <summary>Buttons a finger is on, for drawing them pressed.</summary>
        public readonly HashSet<string> Held = new();

        public bool Capturing => capture != null;

        /// <summary>The HUD reports where its buttons are, each frame, in screen pixels (y up).</summary>
        public void ClearButtons() => buttons.Clear();

        public void AddButton(string id, Rect area) => buttons.Add((id, area));

        /// <summary>The next finger that goes down off the buttons is handed over (for "push here": tap the water).</summary>
        public void CaptureNextTap(Action<Vector2> onTap) => capture = onTap;

        public void CancelCapture() => capture = null;

        public void Update(IReadOnlyList<TouchSample> touches)
        {
            for (var i = 0; i < 2; i++)
            {
                Stick[i] = Vector2.zero;
                StickActive[i] = false;
                BurstPressed[i] = false;
            }

            BraceHeld = false;
            Tapped.Clear();
            Held.Clear();

            foreach (var key in claims.Keys)
            {
                var present = false;
                foreach (var touch in touches) present |= touch.Id == key;
                if (!present) gone.Add(key);
            }

            foreach (var key in gone) claims.Remove(key); // lifted without our seeing it end
            gone.Clear();

            foreach (var touch in touches)
            {
                if (!claims.TryGetValue(touch.Id, out var claim))
                {
                    claim = ClaimFor(touch);
                    claims[touch.Id] = claim;
                    Pressed(claim, touch);
                }

                if (touch.Ended)
                {
                    Released(claim, touch);
                    claims.Remove(touch.Id);
                    continue;
                }

                Holding(claim, touch);
            }
        }

        Claim ClaimFor(TouchSample touch)
        {
            if (Blocked) return new Claim { Use = Use.Ignored };
            foreach (var (id, area) in buttons)
                if (area.Contains(touch.Start)) return new Claim { Use = Use.Button, Button = id };
            if (capture != null) return new Claim { Use = Use.Capture };

            var leftHalf = touch.Start.x < ScreenWidth * 0.5f;
            var stick = TwoSticks ? leftHalf ? 0 : 1 : leftHalf != LeftHanded ? 0 : -1;
            if (stick < 0) return new Claim { Use = Use.Ignored };
            foreach (var other in claims.Values)
                if (other.Use == Use.Stick && other.Stick == stick) return new Claim { Use = Use.Ignored }; // that stick's taken
            return new Claim { Use = Use.Stick, Stick = stick };
        }

        void Pressed(Claim claim, TouchSample touch)
        {
            switch (claim.Use)
            {
                case Use.Button when claim.Button == BurstButton:
                    BurstPressed[0] = true;
                    break;
                case Use.Button when claim.Button != BraceButton:
                    Tapped.Add(claim.Button);
                    break;
                case Use.Capture:
                    var onTap = capture;
                    capture = null;
                    onTap(touch.Start);
                    break;
            }
        }

        void Holding(Claim claim, TouchSample touch)
        {
            switch (claim.Use)
            {
                case Use.Button:
                    Held.Add(claim.Button);
                    if (claim.Button == BraceButton) BraceHeld = true;
                    break;
                case Use.Stick:
                    var i = claim.Stick;
                    var offset = Vector2.ClampMagnitude((touch.Position - touch.Start) / Mathf.Max(1f, StickRadius), 1f);
                    Stick[i] = offset;
                    StickActive[i] = true;
                    StickOrigin[i] = touch.Start;
                    StickKnob[i] = touch.Start + offset * StickRadius;
                    break;
            }
        }

        void Released(Claim claim, TouchSample touch)
        {
            if (claim.Use != Use.Stick || !TwoSticks) return;
            var quick = touch.Time - touch.StartTime < TapSeconds && (touch.Position - touch.Start).magnitude < StickRadius * 0.2f;
            if (quick) BurstPressed[claim.Stick] = true; // two sticks have no room for buttons: a quick tap bursts
        }
    }
}
