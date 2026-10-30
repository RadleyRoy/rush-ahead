using System;
using System.Collections.Generic;
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
    /// from the camera). The stick points the animal's blower, so the raft goes the other way; the "aim the raft"
    /// setting flips that (the stick points where the raft should go).
    /// Devices feed four input slots: slot 0 is the left touch stick, WASD or a lone pad's left stick; slot 1 the right
    /// touch stick, arrows or that pad's right stick; slots 2 and 3 are IJKL and the numpad. With two or more pads,
    /// pad N is slot N. <see cref="SetCrewCount"/> decides which seat each slot drives.
    /// </summary>
    public sealed class CrewInputRouter : MonoBehaviour
    {
        const float Deadzone = 0.15f;
        const int SlotCount = 4;

        // Seat per input slot (-1 = unused). Starts as the whole 4-player crew, best drivers first.
        int[] slotSeats = { 2, 3, 0, 1 };

        public readonly SeatSource[] Seats = { new(), new(), new(), new() };
        readonly List<TouchSample> samples = new();

        /// <summary>The race's touch stick and buttons. The HUD reports its button areas here and draws what it reads back.</summary>
        public TouchControls Touch { get; } = new();

        /// <summary>True while the menus, the pause menu or a dialog have the screen: no device steers then.</summary>
        public Func<bool> Blocked;

        /// <summary>An on-screen button (other than Burst and Brace) pressed: Pause, or an order.</summary>
        public event Action<string> ButtonTapped;

        /// <summary>The camera's heading on the water in sim space. Null means sticks are world-aligned.</summary>
        public Func<Vector2> CameraForward;

        /// <summary>The stick points where the raft should go instead of where the blower points.</summary>
        public bool AimTheRaft { get; set; }

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
                    var air = Owner.AimTheRaft ? -direction : direction;
                    input.AimAngle = Mathf.Atan2(air.y, air.x);
                    input.HasAim = true;
                    input.Throttle = Mathf.InverseLerp(Deadzone, 1f, magnitude);
                }

                return Last = input;
            }
        }

        void Awake()
        {
            foreach (var seat in Seats) seat.Owner = this;
        }

        /// <summary>Offline: this device drives the whole crew, best drivers on the sticks.</summary>
        public void SetCrewCount(int crewCount) => SetSeats(CrewSectors.DriveOrder(crewCount));

        /// <summary>
        /// The seats this device drives, best driver first: slot 0 gets the first, slot 1 the second, and so on. With a
        /// single seat both sticks drive it. Seats owned by other players on the network are simply not listed.
        /// </summary>
        public void SetSeats(IReadOnlyList<int> seats)
        {
            slotSeats = new int[SlotCount];
            for (var slot = 0; slot < SlotCount; slot++)
                slotSeats[slot] = slot < seats.Count ? seats[slot] : seats.Count == 1 && slot == 1 ? seats[0] : -1;
        }

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
            if (seat < 0) return; // a slot this device doesn't drive (e.g. a spectator with no seat)
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

            var blocked = Blocked?.Invoke() ?? false;
            ReadTouch(blocked);
            if (blocked) return;
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

        void ReadTouch(bool blocked)
        {
            samples.Clear();
            foreach (var touch in ETouch.activeTouches)
                samples.Add(new TouchSample
                {
                    Id = touch.touchId,
                    Start = touch.startScreenPosition,
                    Position = touch.screenPosition,
                    StartTime = touch.startTime,
                    Time = touch.time,
                    Ended = touch.phase is TouchPhase.Ended or TouchPhase.Canceled,
                });

            Touch.Blocked = blocked;
            Touch.ScreenWidth = Screen.width;
            Touch.StickRadius = StickRadius;
            Touch.Update(samples);
            if (blocked) return;

            for (var slot = 0; slot < 2; slot++) Contribute(slot, Touch.Stick[slot], Touch.BurstPressed[slot], slot == 0 && Touch.BraceHeld);
            foreach (var id in Touch.Tapped) ButtonTapped?.Invoke(id);
        }

        /// <summary>How far a thumb moves for full throttle: about a centimetre, or an eighth of the screen if the phone won't say.</summary>
        public static float StickRadius => Screen.dpi > 0f ? Screen.dpi * 0.45f : Screen.height * 0.12f;

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
