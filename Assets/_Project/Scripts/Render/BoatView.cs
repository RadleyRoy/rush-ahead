using RushAhead.Content;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>
    /// Placeholder visuals for one boat. Reads simulation state every frame and never writes it.
    /// Lean is faked in 2D: the hull squashes along the lean axis and slides toward the low side while
    /// the shadow stays put, which reads as the boat rocking.
    /// </summary>
    public sealed class BoatView : MonoBehaviour
    {
        public static readonly Color[] SeatColors =
        {
            new(1f, 0.6f, 0.2f), new(0.3f, 0.85f, 1f), new(1f, 0.4f, 0.75f), new(0.55f, 0.95f, 0.35f),
        };

        static readonly Color Wood = new(0.62f, 0.42f, 0.24f);
        static readonly Color Danger = new(0.95f, 0.2f, 0.15f);
        static readonly Vector2 ShadowOffset = new(0.18f, -0.22f);

        Boat boat;
        Transform hullPivot;
        SpriteRenderer hull, shadow;
        SeatView[] seats;

        sealed class SeatView
        {
            public SpriteRenderer Body, Nozzle, Cone, AirBack, AirFill;
            public BlowerDef ConeFor;
            public float ConeHalfAngle, ConeRange;
        }

        public static BoatView Attach(Boat boat, SimConfig config)
        {
            var view = boat.Body.gameObject.AddComponent<BoatView>();
            view.Build(boat, config);
            return view;
        }

        void Build(Boat target, SimConfig config)
        {
            boat = target;
            var hullSprite = ShapeSprites.Polygon(config.HullOutline());
            shadow = ShapeSprites.Spawn(transform, "Shadow", hullSprite, new Color(0f, 0f, 0.1f, 0.35f), 0);

            hullPivot = new GameObject("Hull").transform;
            hullPivot.SetParent(transform, false);
            hull = ShapeSprites.Spawn(hullPivot, "Planks", hullSprite, Wood, 2);

            seats = new SeatView[boat.Crew.Length];
            for (var i = 0; i < seats.Length; i++)
            {
                var offset = boat.Crew[i].SeatOffset;
                var seat = new SeatView
                {
                    Cone = ShapeSprites.Spawn(transform, $"Cone {i}", null, new Color(0.9f, 0.97f, 1f, 0f), 1),
                    Body = ShapeSprites.Spawn(hullPivot, $"Crew {i}", ShapeSprites.Circle(0.28f), SeatColors[i], 4),
                    Nozzle = ShapeSprites.Spawn(transform, $"Nozzle {i}", ShapeSprites.Square, new Color(0.2f, 0.2f, 0.25f), 5),
                    AirBack = ShapeSprites.Spawn(hullPivot, $"Air {i}", ShapeSprites.Square, new Color(0f, 0f, 0f, 0.55f), 6),
                };
                seat.AirFill = ShapeSprites.Spawn(seat.AirBack.transform, "Fill", ShapeSprites.Square, Color.white, 7);

                seat.Body.transform.localPosition = offset;
                seat.Nozzle.transform.localScale = new Vector3(0.5f, 0.13f, 1f);
                seat.AirBack.transform.localPosition = offset + new Vector2(-0.38f, 0f);
                seat.AirBack.transform.localScale = new Vector3(0.1f, 0.5f, 1f);
                seats[i] = seat;
            }
        }

        void LateUpdate()
        {
            var tilt = boat.Tilt.Tilt; // x roll toward port (+y), y pitch toward the bow (+x)
            var capsized = boat.State == BoatState.Capsized;

            hullPivot.localScale = capsized
                ? new Vector3(0.9f, -0.8f, 1f)
                : new Vector3(1f - 0.35f * Mathf.Abs(tilt.y), 1f - 0.35f * Mathf.Abs(tilt.x), 1f);
            hullPivot.localPosition = capsized ? Vector3.zero : new Vector3(0.6f * tilt.y, 0.6f * tilt.x, 0f);
            shadow.transform.position = (Vector2)transform.position + ShadowOffset;

            var danger = boat.TiltDanger;
            hull.color = capsized ? new Color(0.25f, 0.18f, 0.12f) : Color.Lerp(Wood, Danger, danger * danger);

            for (var i = 0; i < seats.Length; i++) UpdateSeat(seats[i], boat.Crew[i], capsized);
        }

        void UpdateSeat(SeatView seat, CrewMember crew, bool capsized)
        {
            var nozzleAt = (Vector2)hullPivot.TransformPoint(crew.SeatOffset);
            var airDirection = crew.AirDirection;
            var rotation = Quaternion.Euler(0f, 0f, crew.AimAngle * Mathf.Rad2Deg);

            seat.Nozzle.transform.SetPositionAndRotation(nozzleAt + 0.3f * airDirection, rotation);
            seat.Nozzle.enabled = !capsized;
            seat.Body.color = crew.Braced ? Color.Lerp(SeatColors[crew.Seat], Color.black, 0.4f) : SeatColors[crew.Seat];

            var spec = crew.Blower;
            if (seat.ConeFor != spec || !Mathf.Approximately(seat.ConeHalfAngle, spec.coneHalfAngleDeg) || !Mathf.Approximately(seat.ConeRange, spec.range))
            {
                seat.Cone.sprite = ShapeSprites.Cone(spec.coneHalfAngleDeg, spec.range);
                seat.ConeFor = spec;
                seat.ConeHalfAngle = spec.coneHalfAngleDeg;
                seat.ConeRange = spec.range;
            }

            var burstFlash = Mathf.Clamp01(1f - crew.SecondsSinceBurst / 0.2f);
            var coneColor = seat.Cone.color;
            coneColor.a = capsized ? 0f : Mathf.Max(0.28f * (crew.IsBlowing ? crew.Spool : 0f), 0.6f * burstFlash);
            seat.Cone.color = coneColor;
            seat.Cone.transform.SetPositionAndRotation(nozzleAt, rotation);

            var air = crew.Tank.Fraction;
            seat.AirFill.transform.localScale = new Vector3(1f, air, 1f);
            seat.AirFill.transform.localPosition = new Vector3(0f, (air - 1f) * 0.5f, 0f);
            seat.AirFill.color = air < 0.25f ? new Color(1f, 0.35f, 0.3f) : Color.white;
        }
    }
}
