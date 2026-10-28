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

        public static Label Text(string text, string classes = null)
        {
            var label = new Label(text);
            AddClasses(label, classes);
            return label;
        }

        /// <summary>A button styled by Game.uss alone (Unity's own button look is dropped).</summary>
        public static Button Btn(string text, Action clicked, string classes = "btn")
        {
            var button = new Button(clicked) { text = text };
            button.RemoveFromClassList(Button.ussClassName);
            AddClasses(button, classes);
            return button;
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
