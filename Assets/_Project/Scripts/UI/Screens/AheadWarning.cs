using System;
using RushAhead.Content;
using RushAhead.Render;
using RushAhead.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace RushAhead.UI
{
    /// <summary>
    /// The race HUD's look-out. A chip under the pause button names the next thing coming down the river (its picture,
    /// its name and what to do about it: BRACE, DODGE, TIME IT...) about three seconds before you reach it, and turns to
    /// BRACE NOW when it's about to need it; a marker floats over the thing itself; and the first time this player meets
    /// something, a card says what it is (kept on the device, <see cref="Records.Seen"/>). <see cref="Culprit"/> works out
    /// what got you when you capsize or go over, for "What hit me?" on the results.
    /// </summary>
    sealed class AheadWarning
    {
        const float LookSeconds = 3.2f; // warn this long before reaching it
        const float LookAtLeast = 22f; // metres, however slow you're going
        const float Passed = 3f; // metres past its marker before it's behind you
        const float CardSeconds = 5f;

        readonly VisualElement chip;
        readonly VisualElement chipPicture;
        readonly Label chipName;
        readonly Label chipCounter;
        readonly VisualElement pin;
        readonly VisualElement card;
        readonly VisualElement cardPicture;
        readonly Label cardName;
        readonly Label cardWhat;
        float cardUntil = -1f;
        string chipFor;
        CourseDef featuresMarked;

        public AheadWarning(VisualElement hud, VisualElement world)
        {
            chipPicture = Ui.El("ahead-picture");
            chipName = Ui.Text("", "ahead-name");
            chipCounter = Ui.Text("", "ahead-counter");
            chip = Ui.El("panel ahead", chipPicture, Ui.El("col", chipName, chipCounter));
            chip.pickingMode = PickingMode.Ignore;
            chip.style.position = Position.Absolute;
            chip.style.left = 36;
            chip.style.top = 168;
            hud.Add(chip);

            pin = new VisualElement { pickingMode = PickingMode.Ignore };
            pin.style.position = Position.Absolute;
            pin.style.width = 44;
            pin.style.height = 40;
            pin.generateVisualContent += DrawPin;
            world.Add(pin);

            cardPicture = Ui.El("ahead-card-picture");
            cardName = Ui.Text("", "h2");
            cardWhat = Ui.Text("", "small");
            card = Ui.El("panel col ahead-card", Ui.Text("NEW ON THE RIVER", "ahead-counter"), cardPicture, cardName, cardWhat,
                Ui.Text("It's in your Field Guide.", "small muted"));
            card.pickingMode = PickingMode.Ignore;
            card.style.position = Position.Absolute;
            card.style.right = 36;
            card.style.top = 128;
            hud.Add(card);
            Hide();
        }

        void Hide()
        {
            Ui.Show(chip, false);
            Ui.Show(pin, false);
            Ui.Show(card, false);
            chipFor = null;
        }

        /// <param name="toPanel">A world point as a position in <paramref name="world"/>'s layer, or null when behind the camera.</param>
        public void Refresh(GameController game, RaceInfo run, IBoatState boat, Func<Vector3, Vector2?> toPanel)
        {
            var course = game.CurrentCourse;
            if (!run.Active || run.Countdown > 0 || run.Finished || run.Out || run.Fallen || boat == null || course == null)
            {
                Hide();
                return;
            }

            MarkFeatures(course.Def);
            var river = course.River;
            var here = river.Project(boat.RenderPosition).Distance;
            var reach = Mathf.Max(LookAtLeast, boat.Velocity.magnitude * LookSeconds);
            Hazards.Marker? next = null;
            foreach (var marker in course.Hazards.Markers)
            {
                if (marker.Id == "BoostLane" || marker.Distance + Passed < here) continue;
                if (marker.Distance - here <= reach) next = marker;
                break;
            }

            var entry = next.HasValue ? ObstacleGuide.ById(next.Value.Id) : null;
            if (entry == null)
            {
                Ui.Show(chip, false);
                Ui.Show(pin, false);
                chipFor = null;
            }
            else
            {
                var m = next.Value;
                if (chipFor != entry.Id)
                {
                    chipFor = entry.Id;
                    GuidePictures.Paint(chipPicture, entry);
                    chipName.text = entry.Name.ToUpperInvariant();
                    if (Records.MarkSeen(entry.Id)) ShowCard(entry);
                }

                var now = course.Hazards.BraceNeeded(boat.RenderPosition, game.ObstacleClock, 0.7f);
                Ui.Set(chipCounter, now ? "BRACE NOW!" : entry.CounterWord.ToUpperInvariant());
                Ui.SetClass(chipCounter, "ahead-counter--now", now);
                var pulse = now ? 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 16f) : 1f;
                chip.style.scale = new Scale(new Vector2(pulse, pulse));
                Ui.Show(chip, true);

                // The marker over the thing itself, until you're nearly on it.
                var at = river.At(m.Distance);
                var spot = at.Centre + at.Left * m.Across;
                var away = m.Distance - here;
                var point = away > 7f ? toPanel(SimSpace.ToWorld(spot, 3f)) : null;
                Ui.Show(pin, point.HasValue);
                if (point.HasValue)
                {
                    var bob = 6f * Mathf.Sin(Time.unscaledTime * 5f);
                    pin.style.left = point.Value.x - 22f;
                    pin.style.top = point.Value.y - 40f + bob;
                }
            }

            Ui.Show(card, Time.unscaledTime < cardUntil);
        }

        void ShowCard(GuideEntry entry)
        {
            GuidePictures.Paint(cardPicture, entry);
            cardName.text = entry.Name.ToUpperInvariant();
            cardWhat.text = $"{entry.What} {entry.Beat}";
            cardUntil = Time.unscaledTime + CardSeconds;
        }

        /// <summary>The river's own features count as met once you've raced a course that has them.</summary>
        void MarkFeatures(CourseDef def)
        {
            if (def == featuresMarked) return;
            featuresMarked = def;
            foreach (var entry in ObstacleGuide.Entries)
                if (entry.Kind == null && entry.Id != "Whirlpool" && ObstacleGuide.Appears(entry, def))
                    Records.MarkSeen(entry.Id);
        }

        /// <summary>A small downward arrow in the accent colour with a dark edge.</summary>
        static void DrawPin(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            painter.BeginPath();
            painter.MoveTo(new Vector2(3f, 3f));
            painter.LineTo(new Vector2(41f, 3f));
            painter.LineTo(new Vector2(22f, 37f));
            painter.ClosePath();
            painter.fillColor = new Color(1f, 0.416f, 0.169f, 0.95f);
            painter.Fill();
            painter.strokeColor = new Color(0.04f, 0.05f, 0.06f, 0.9f);
            painter.lineWidth = 3f;
            painter.Stroke();
        }

        /// <summary>
        /// What most likely got a raft that capsized or went over the edge here: the nearest thing on the river just
        /// upstream of it (or just ahead), weighted toward the same side; the edge itself for a fall with nothing near.
        /// </summary>
        public static string Culprit(Course course, Vector2 position, bool fell)
        {
            if (course == null) return null;
            var at = course.River.Project(position);
            string best = null;
            var score = float.MaxValue;
            foreach (var marker in course.Hazards.Markers)
            {
                if (marker.Id == "BoostLane") continue;
                var past = at.Distance - marker.Distance;
                if (past < -6f || past > 18f) continue;
                var s = Mathf.Abs(past) + 0.4f * Mathf.Abs(at.Lateral - marker.Across);
                if (s >= score) continue;
                score = s;
                best = marker.Id;
            }

            return best ?? (fell ? "Edge" : null);
        }
    }
}
