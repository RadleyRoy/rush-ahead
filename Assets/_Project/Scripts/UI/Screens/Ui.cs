using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace RushAhead.UI
{
    /// <summary>Small builders for the screens, so a layout reads top to bottom. Styling lives in Game.uss.</summary>
    public static class Ui
    {
        public static VisualElement El(string classes = null, params VisualElement[] children)
        {
            var element = new VisualElement();
            AddClasses(element, classes);
            foreach (var child in children) element.Add(child);
            return element;
        }

        /// <summary>Headings and chips read in uppercase.</summary>
        static bool Shouted(VisualElement element) =>
            element.ClassListContains("h1") || element.ClassListContains("h2") || element.ClassListContains("wordmark") || element.ClassListContains("chip") ||
            element.ClassListContains("btn") || element.ClassListContains("segment");

        static bool Shouted(string classes) => classes != null && (classes.Contains("h1") || classes.Contains("h2") || classes.Contains("wordmark") || classes.Contains("chip"));

        public static Label Text(string text, string classes = null)
        {
            var label = new Label(Shouted(classes) ? text?.ToUpperInvariant() : text);
            AddClasses(label, classes);
            return label;
        }

        /// <summary>Raised by every button press, for the click sound.</summary>
        public static event Action Clicked;

        /// <summary>A button styled by Game.uss alone (Unity's own button look is dropped): uppercase, and slanted unless it's a tab or round.</summary>
        public static Button Btn(string text, Action clicked, string classes = "btn")
        {
            var button = new Button(() =>
            {
                Clicked?.Invoke();
                clicked();
            }) { text = text?.ToUpperInvariant() };
            button.RemoveFromClassList(Button.ussClassName);
            AddClasses(button, classes);
            if (classes.Contains("btn") && !classes.Contains("btn--round")) Slant(button);
            return button;
        }

        static readonly CustomStyleProperty<Color> FillProperty = new("--fill");
        static readonly CustomStyleProperty<Color> EdgeProperty = new("--edge");
        static readonly CustomStyleProperty<float> SlantProperty = new("--slant");

        /// <summary>
        /// The sport shape: a parallelogram in the element's <c>--fill</c> colour with an <c>--edge</c> line, leaning by
        /// <c>--slant</c> of its height, drawn behind its text. Game.uss sets all three per state (hover, pressed, on).
        /// </summary>
        public static void Slant(VisualElement element)
        {
            var fill = Color.clear;
            var edge = Color.clear;
            var slant = 0.24f;
            element.RegisterCallback<CustomStyleResolvedEvent>(e =>
            {
                fill = e.customStyle.TryGetValue(FillProperty, out var f) ? f : Color.clear;
                edge = e.customStyle.TryGetValue(EdgeProperty, out var g) ? g : Color.clear;
                slant = e.customStyle.TryGetValue(SlantProperty, out var s) ? s : 0.24f;
                element.MarkDirtyRepaint();
            });
            // Painted before the element's own content (its text), so a solid fill sits behind the words.
            element.generateVisualContent = Paint + element.generateVisualContent;
            void Paint(MeshGenerationContext context)
            {
                var w = element.layout.width;
                var h = element.layout.height;
                if (w < 2f || h < 2f) return;
                var lean = Mathf.Min(h * slant, w * 0.25f);
                var painter = context.painter2D;
                painter.BeginPath();
                painter.MoveTo(new Vector2(lean, 0f));
                painter.LineTo(new Vector2(w, 0f));
                painter.LineTo(new Vector2(w - lean, h));
                painter.LineTo(new Vector2(0f, h));
                painter.ClosePath();
                if (fill.a > 0f)
                {
                    painter.fillColor = fill;
                    painter.Fill();
                }

                if (edge.a > 0f)
                {
                    painter.strokeColor = edge;
                    painter.lineWidth = 2f;
                    painter.Stroke();
                }
            }
        }

        public static VisualElement Spacer(string size = "spacer") => El(size);

        public static VisualElement Grow() => El("grow");

        public static void Show(VisualElement element, bool visible) =>
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        /// <summary>Enables or disables only on a change, so a screen can refresh every frame cheaply.</summary>
        public static void Enable(VisualElement element, bool on)
        {
            if (element.enabledSelf != on) element.SetEnabled(on);
        }

        /// <summary>Sets a label's text only when it changes, so a screen can refresh every frame without relayouts.</summary>
        public static void Set(Label label, string text)
        {
            if (Shouted(label)) text = text?.ToUpperInvariant();
            if (label.text != text) label.text = text;
        }

        public static void SetClass(VisualElement element, string className, bool on)
        {
            if (element.ClassListContains(className) != on) element.EnableInClassList(className, on);
        }

        public static string Time(float seconds) => seconds <= 0f ? "--" : $"{(int)(seconds / 60f)}:{seconds % 60f:00.0}";

        public static string Delta(float seconds) => $"{(seconds < 0f ? "-" : "+")}{Mathf.Abs(seconds):0.0}";

        static void AddClasses(VisualElement element, string classes)
        {
            if (string.IsNullOrEmpty(classes)) return;
            foreach (var name in classes.Split(' ', StringSplitOptions.RemoveEmptyEntries)) element.AddToClassList(name);
        }
    }
}
