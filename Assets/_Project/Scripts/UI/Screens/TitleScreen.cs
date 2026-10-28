using UnityEngine;
using UnityEngine.UIElements;

namespace RushAhead.UI
{
    /// <summary>
    /// The start page: "Rush Ahead" with "created by Radley" beneath it, over the colours of the water, while the
    /// game loads behind it. It stays a moment, can be tapped away after a short beat, then fades to the menu.
    /// </summary>
    public sealed class TitleScreen : GameScreen
    {
        const float ShowSeconds = 2.2f;
        const float SkippableAfter = 0.6f;
        const float FadeSeconds = 0.5f;
        static readonly Color Sky = new(0.45f, 0.82f, 1f);
        static readonly Color Water = new(0.06f, 0.55f, 0.82f);

        float shownFor;
        float fading = -1f;
        Label status;

        public TitleScreen(GameUi ui) : base(ui)
        {
        }

        public override bool Opaque => true;

        public override void Build()
        {
            Root.style.backgroundImage = Gradients.Vertical(Sky, Water);
            Root.AddToClassList("center");

            var waves = new VisualElement { pickingMode = PickingMode.Ignore };
            waves.AddToClassList("layer");
            waves.generateVisualContent += DrawWaves;
            Root.Add(waves);

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
            shownFor += Time.unscaledDeltaTime;
            Ui.Set(status, "Inflating the raft" + new string('.', 1 + (int)(Time.unscaledTime * 3f) % 3));
            if (fading < 0f && shownFor >= ShowSeconds) Leave();
            if (fading < 0f) return;

            fading += Time.unscaledDeltaTime / FadeSeconds;
            Root.style.opacity = 1f - fading;
            if (fading >= 1f) Flow.TitleDone();
        }

        public override void OnHide() => Root.style.opacity = 1f;

        void Leave()
        {
            if (fading < 0f) fading = 0f;
        }

        void DrawWaves(MeshGenerationContext context)
        {
            var rect = context.visualElement.contentRect;
            var painter = context.painter2D;
            painter.lineWidth = 6f;
            painter.strokeColor = new Color(1f, 1f, 1f, 0.22f);
            for (var line = 0; line < 3; line++)
            {
                var y = rect.height * (0.72f + line * 0.07f);
                painter.BeginPath();
                for (var x = 0f; x <= rect.width; x += 24f)
                {
                    var point = new Vector2(x, y + Mathf.Sin(x * 0.012f + line * 1.7f) * 10f);
                    if (x == 0f) painter.MoveTo(point);
                    else painter.LineTo(point);
                }

                painter.Stroke();
            }
        }
    }
}
