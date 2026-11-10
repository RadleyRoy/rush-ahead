using UnityEngine;
using UnityEngine.UIElements;

namespace RushAhead.UI
{
    /// <summary>
    /// The start page: "Rush Ahead" with "created by Radley" beneath it, on dark glass cut by a few hot slanted
    /// streaks, while the game loads behind it. It stays a moment, can be tapped away after a short beat, then fades to
    /// the menu.
    /// </summary>
    public sealed class TitleScreen : GameScreen
    {
        const float ShowSeconds = 2.2f;
        const float SkippableAfter = 0.6f;
        const float FadeSeconds = 0.5f;
        const float LongestStep = 1f / 30f; // a loading hitch mustn't skip the fade
        static readonly Color Top = new(0.086f, 0.1f, 0.125f);
        static readonly Color Bottom = new(0.043f, 0.051f, 0.063f);
        static readonly Color Accent = new(1f, 0.416f, 0.169f);

        float shownFor;
        float fading = -1f;
        Label status;

        public TitleScreen(GameUi ui) : base(ui)
        {
        }

        public override bool Opaque => true;

        Texture2D backdrop;

        /// <summary>The dark gradient runs right to the screen's edges, past any notch (the words stay in the safe area).</summary>
        public override Texture2D Backdrop => backdrop;

        public override void Build()
        {
            backdrop = Gradients.Vertical(Top, Bottom);
            Root.style.backgroundImage = backdrop;
            Root.AddToClassList("center");

            var streaks = new VisualElement { pickingMode = PickingMode.Ignore };
            streaks.AddToClassList("layer");
            streaks.generateVisualContent += DrawStreaks;
            Root.Add(streaks);

            Root.Add(Ui.Text("Rush Ahead", "wordmark"));
            Root.Add(Ui.Text("created by Radley", "byline"));
            Root.Add(Ui.Spacer("spacer-l"));
            status = Ui.Text("Inflating the raft", "body");
            status.style.whiteSpace = WhiteSpace.NoWrap;
            status.style.width = 520;
            status.style.unityTextAlign = TextAnchor.MiddleLeft;
            status.style.paddingLeft = 70; // centres "Inflating the raft" while the dots come and go
            Root.Add(status);

            Root.RegisterCallback<PointerDownEvent>(_ =>
            {
                if (shownFor >= SkippableAfter) Leave();
            });
        }

        public override void Refresh()
        {
            var dt = Mathf.Min(Time.unscaledDeltaTime, LongestStep);
            shownFor += dt;
            Ui.Set(status, "Inflating the raft" + new string('.', 1 + (int)(Time.unscaledTime * 3f) % 3));
            if (fading < 0f && shownFor >= ShowSeconds) Leave();
            if (fading < 0f) return;

            fading += dt / FadeSeconds;
            Root.style.opacity = 1f - fading;
            if (fading >= 1f) Flow.TitleDone();
        }

        public override void OnHide() => Root.style.opacity = 1f;

        void Leave()
        {
            if (fading < 0f) fading = 0f;
        }

        /// <summary>Slanted streaks racing in from the left, one in the accent, and a thin accent rule under the name.</summary>
        void DrawStreaks(MeshGenerationContext context)
        {
            var rect = context.visualElement.contentRect;
            var painter = context.painter2D;
            var h = rect.height;
            var w = rect.width;
            (float y, float thick, float length, Color color)[] streaks =
            {
                (0.2f, 0.012f, 0.34f, new Color(1f, 1f, 1f, 0.05f)),
                (0.27f, 0.006f, 0.22f, new Color(1f, 1f, 1f, 0.08f)),
                (0.74f, 0.03f, 0.46f, new Color(Accent.r, Accent.g, Accent.b, 0.9f)),
                (0.79f, 0.01f, 0.3f, new Color(1f, 1f, 1f, 0.12f)),
                (0.83f, 0.004f, 0.18f, new Color(1f, 1f, 1f, 0.1f)),
            };
            foreach (var (y, thick, length, color) in streaks)
            {
                var top = h * y;
                var bottom = top + h * thick;
                var lean = (bottom - top) * 1.2f;
                painter.fillColor = color;
                painter.BeginPath();
                // From well off the left (the screen sits inside the safe area, so a notch's width is still to go).
                painter.MoveTo(new Vector2(-600f, top));
                painter.LineTo(new Vector2(w * length + lean, top));
                painter.LineTo(new Vector2(w * length, bottom));
                painter.LineTo(new Vector2(-600f, bottom));
                painter.ClosePath();
                painter.Fill();
            }

            // The same streaks, mirrored and fainter, leaving from the right.
            painter.fillColor = new Color(1f, 1f, 1f, 0.06f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(w * 0.7f, h * 0.12f));
            painter.LineTo(new Vector2(w, h * 0.12f));
            painter.LineTo(new Vector2(w, h * 0.135f));
            painter.LineTo(new Vector2(w * 0.7f - h * 0.018f, h * 0.135f));
            painter.ClosePath();
            painter.Fill();
        }
    }
}
