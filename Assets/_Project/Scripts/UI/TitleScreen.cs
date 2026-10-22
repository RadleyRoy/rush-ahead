using UnityEngine;

namespace RushAhead.UI
{
    /// <summary>
    /// The start page: "Rush Ahead" with "created by Radley" beneath it, over the colours of the water, shown while the
    /// game builds the lagoon behind it. It stays a couple of seconds, then fades away; a tap skips it.
    /// </summary>
    public sealed class TitleScreen : MonoBehaviour
    {
        const float ShowSeconds = 2.2f;
        const float FadeSeconds = 0.6f;
        const float SkippableAfter = 0.6f;

        static readonly Color Sky = new(0.45f, 0.82f, 1f);
        static readonly Color Water = new(0.06f, 0.55f, 0.82f);

        Texture2D gradient;
        GUIStyle title, credit, loading, shadow;
        float shownFor;
        float fade = -1f; // < 0 until it starts fading

        /// <summary>Visible at all, so the touch sticks know to leave taps alone.</summary>
        public bool Showing => enabled;

        public static TitleScreen Show(GameObject host) => host.AddComponent<TitleScreen>();

        /// <summary>Starts the fade now, e.g. on a tap or for a scripted demo.</summary>
        public void Dismiss()
        {
            if (fade < 0f) fade = 0f;
        }

        void Update()
        {
            shownFor += Time.unscaledDeltaTime;
            var pointer = UnityEngine.InputSystem.Pointer.current;
            if (shownFor > SkippableAfter && pointer != null && pointer.press.wasPressedThisFrame) Dismiss();
            if (shownFor > ShowSeconds) Dismiss();
            if (fade < 0f) return;

            fade += Time.unscaledDeltaTime / FadeSeconds;
            if (fade >= 1f) Destroy(this);
        }

        void OnDestroy()
        {
            if (gradient != null) Destroy(gradient);
        }

        void OnGUI()
        {
            GUI.depth = -100; // in front of the HUD
            if (gradient == null) Build();

            var alpha = fade < 0f ? 1f : 1f - Mathf.SmoothStep(0f, 1f, fade);
            var w = Screen.width;
            var h = Screen.height;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(new Rect(0, 0, w, h), gradient, ScaleMode.StretchToFill);

            // A few soft wave lines across the lower half.
            GUI.color = new Color(1f, 1f, 1f, 0.12f * alpha);
            for (var i = 0; i < 4; i++)
            {
                var y = h * (0.66f + i * 0.08f) + Mathf.Sin(Time.unscaledTime * 1.4f + i) * h * 0.006f;
                GUI.DrawTexture(new Rect(w * 0.12f + i * w * 0.03f, y, w * (0.76f - i * 0.06f), Mathf.Max(2f, h * 0.006f)), Texture2D.whiteTexture);
            }

            title.fontSize = Mathf.RoundToInt(h * 0.16f);
            credit.fontSize = Mathf.RoundToInt(h * 0.045f);
            loading.fontSize = Mathf.RoundToInt(h * 0.03f);
            Shadowed(new Rect(0, h * 0.26f, w, h * 0.22f), "Rush Ahead", title, alpha, h * 0.008f);
            Shadowed(new Rect(0, h * 0.5f, w, h * 0.08f), "created by Radley", credit, alpha, h * 0.004f);

            var dots = new string('.', 1 + (int)(Time.unscaledTime * 3f) % 3);
            GUI.color = new Color(1f, 1f, 1f, 0.75f * alpha);
            GUI.Label(new Rect(0, h * 0.84f, w, h * 0.06f), $"Inflating the raft{dots}", loading);
            GUI.color = Color.white;
        }

        void Build()
        {
            gradient = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (var y = 0; y < 64; y++) gradient.SetPixel(0, y, Color.Lerp(Water, Sky, y / 63f)); // texture y runs up
            gradient.Apply();

            title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            credit = new GUIStyle(title) { fontStyle = FontStyle.BoldAndItalic, normal = { textColor = new Color(1f, 0.97f, 0.88f) } };
            loading = new GUIStyle(title) { fontStyle = FontStyle.Normal };
        }

        /// <summary>
        /// Text with a drop shadow. The shadow needs a style of its own: IMGUI caches drawn text per style and content,
        /// so recolouring one style between the two draws leaves both in whichever colour came first.
        /// </summary>
        void Shadowed(Rect rect, string text, GUIStyle style, float alpha, float offset)
        {
            shadow ??= new GUIStyle(style);
            shadow.font = style.font;
            shadow.fontSize = style.fontSize;
            shadow.fontStyle = style.fontStyle;
            shadow.alignment = style.alignment;
            shadow.normal.textColor = new Color(0.1f, 0.13f, 0.25f, 0.55f);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(new Rect(rect.x + offset, rect.y + offset, rect.width, rect.height), text, shadow);
            GUI.Label(rect, text, style);
        }
    }
}
