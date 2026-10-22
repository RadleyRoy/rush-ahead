using System;
using RushAhead.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace RushAhead.UI
{
    /// <summary>
    /// Turns every local device into per-seat <see cref="CrewInput"/>. Sticks are camera-relative (up pushes away
    /// from the camera). By default the stick points where you want the boat to go and the animal aims its nozzle
    /// the opposite way; "direct nozzle aim" flips that.
    /// Devices feed four input slots: slot 0 is the left touch stick, WASD or a lone pad's left stick; slot 1 the right
    /// touch stick, arrows or that pad's right stick; slots 2 and 3 are IJKL and the numpad. With two or more pads,
    /// pad N is slot N. <see cref="SetCrewCount"/> decides which seat each slot drives.
    /// </summary>
    public sealed class CrewInputRouter : MonoBehaviour
    {
        const float Deadzone = 0.15f;
        const float TapSeconds = 0.2f;
        const int SlotCount = 4;

        // Seat per input slot, by crew size (-1 = unused). The two sticks always get the seats that can drive the raft
        // forward: the rear quarters for 3 and 4 players, port and starboard for 2, and the soloist on both for 1.
        static readonly int[][] SlotSeatsByCrew =
        {
            new[] { 0, 0, -1, -1 },
            new[] { 0, 1, -1, -1 },
            new[] { 1, 2, 0, -1 },
            new[] { 2, 3, 0, 1 },
        };

        int[] slotSeats = SlotSeatsByCrew[3];

        public readonly SeatSource[] Seats = { new(), new(), new(), new() };
        public readonly TouchStick[] Sticks = { new(), new() };

        /// <summary>Screen-space (y-up) areas where touches belong to on-screen buttons, not sticks.</summary>
        public Func<Vector2, bool> IsOverUi;

        /// <summary>The camera's heading on the water in sim space. Null means sticks are world-aligned.</summary>
        public Func<Vector2> CameraForward;

        public bool DirectNozzleAim { get; set; }

        /// <summary>When true, device input is ignored and seats are driven through <see cref="SetScripted"/>.</summary>
        public bool Scripted { get; set; }

        public sealed class SeatSource : ICrewInputSource
        {
            internal Vector2 Stick;
            internal bool BurstPending;
            internal bool Brace;
            internal CrewInputRouter Owner;

            /// <summary>The input most recently handed to the simulation, for the HUD.</summary>
            public CrewInput Last { get; private set; }

            public CrewInput ReadInput()
            {
                var input = new CrewInput { Burst = BurstPending, Brace = Brace };
                BurstPending = false;

                var magnitude = Mathf.Min(1f, Stick.magnitude);
                if (magnitude > Deadzone)
                {
                    var direction = Owner.CameraRelative(Stick / Stick.magnitude);
                    var air = Owner.DirectNozzleAim ? direction : -direction;
                    input.AimAngle = Mathf.Atan2(air.y, air.x);
                    input.HasAim = true;
                    input.Throttle = Mathf.InverseLerp(Deadzone, 1f, magnitude);
                }

                return Last = input;
            }
        }

        public sealed class TouchStick
        {
            public bool Active;
            public Vector2 Origin, Position;
            public int TouchId = -1;
        }

        void Awake()
        {
            foreach (var seat in Seats) seat.Owner = this;
        }

        public void SetCrewCount(int crewCount) => slotSeats = SlotSeatsByCrew[crewCount - 1];

        /// <summary>The seat an input slot drives, or -1 if unused at this crew size.</summary>
        public int SeatForSlot(int slot) => slotSeats[slot];

        /// <summary>Stick up is the camera's forward on the water; stick right is the camera's right.</summary>
        Vector2 CameraRelative(Vector2 stick)
        {
            if (CameraForward == null) return stick;
            var forward = CameraForward();
            var right = new Vector2(forward.y, -forward.x);
            return stick.x * right + stick.y * forward;
        }

        void OnEnable()
        {
            EnhancedTouchSupport.Enable();
#if UNITY_EDITOR || UNITY_STANDALONE
            TouchSimulation.Enable(); // drag with the mouse to use the touch sticks on desktop
#endif
        }

        void OnDisable()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            TouchSimulation.Disable();
#endif
            EnhancedTouchSupport.Disable();
        }

        public void SetScripted(int seat, Vector2 stick, bool burst = false, bool brace = false)
        {
            Seats[seat].Stick = stick;
            Seats[seat].Brace = brace;
            if (burst) Seats[seat].BurstPending = true;
        }

        void Update()
        {
            if (Scripted) return;

            foreach (var seat in Seats)
            {
                seat.Stick = Vector2.zero;
                seat.Brace = false;
            }

            ReadTouch();
            ReadKeyboard();
            ReadGamepads();
        }

        void Contribute(int slot, Vector2 stick, bool burst, bool brace)
        {
            var seat = slotSeats[slot];
            if (seat < 0) return;

            var source = Seats[seat];
            if (stick.sqrMagnitude > source.Stick.sqrMagnitude) source.Stick = stick;
            source.BurstPending |= burst;
            source.Brace |= brace;
        }

        void ReadTouch()
        {
            var radius = Screen.dpi > 0f ? Screen.dpi * 0.45f : Screen.height * 0.12f;
            foreach (var stick in Sticks) stick.Active = false;

            foreach (var touch in ETouch.activeTouches)
            {
                if (IsOverUi != null && IsOverUi(touch.startScreenPosition)) continue;

                var side = touch.startScreenPosition.x < Screen.width * 0.5f ? 0 : 1;
                var delta = (touch.screenPosition - touch.startScreenPosition) / radius;

                if (touch.phase is TouchPhase.Ended or TouchPhase.Canceled)
                {
                    var quickTap = touch.time - touch.startTime < TapSeconds && delta.magnitude < 0.2f;
                    if (quickTap) Contribute(side, Vector2.zero, burst: true, brace: false);
                    continue;
                }

                var stick = Sticks[side];
                stick.Active = true;
                stick.TouchId = touch.touchId;
                stick.Origin = touch.startScreenPosition;
                stick.Position = touch.startScreenPosition + Vector2.ClampMagnitude(delta, 1f) * radius;
                Contribute(side, Vector2.ClampMagnitude(delta, 1f), false, false);
            }
        }

        void ReadKeyboard()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            Cluster(0, kb.wKey, kb.aKey, kb.sKey, kb.dKey, kb.leftShiftKey, kb.leftCtrlKey);
            Cluster(1, kb.upArrowKey, kb.leftArrowKey, kb.downArrowKey, kb.rightArrowKey, kb.rightShiftKey, kb.rightCtrlKey);
            Cluster(2, kb.iKey, kb.jKey, kb.kKey, kb.lKey, kb.uKey, kb.oKey);
            Cluster(3, kb.numpad8Key, kb.numpad4Key, kb.numpad5Key, kb.numpad6Key, kb.numpad0Key, kb.numpadPeriodKey);
        }

        void Cluster(int slot, KeyControl up, KeyControl left, KeyControl down, KeyControl right, KeyControl burst, KeyControl brace)
        {
            var stick = new Vector2((right.isPressed ? 1 : 0) - (left.isPressed ? 1 : 0), (up.isPressed ? 1 : 0) - (down.isPressed ? 1 : 0));
            Contribute(slot, stick.normalized, burst.wasPressedThisFrame, brace.isPressed);
        }

        void ReadGamepads()
        {
            var pads = Gamepad.all;
            if (pads.Count == 1)
            {
                var pad = pads[0];
                var brace = pad.buttonEast.isPressed;
                Contribute(0, pad.leftStick.ReadValue(), pad.leftShoulder.wasPressedThisFrame || pad.leftTrigger.wasPressedThisFrame, brace);
                Contribute(1, pad.rightStick.ReadValue(), pad.rightShoulder.wasPressedThisFrame || pad.rightTrigger.wasPressedThisFrame, brace);
                return;
            }

            for (var i = 0; i < pads.Count && i < SlotCount; i++)
            {
                var pad = pads[i];
                var burst = pad.buttonSouth.wasPressedThisFrame || pad.rightTrigger.wasPressedThisFrame;
                Contribute(i, pad.leftStick.ReadValue(), burst, pad.buttonEast.isPressed);
            }
        }
    }
}
