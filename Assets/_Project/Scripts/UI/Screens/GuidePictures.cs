using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;
using UnityEngine.UIElements;

namespace RushAhead.UI
{
    /// <summary>
    /// The Field Guide's photographs, one per entry: <c>Resources/Guide/&lt;Id&gt;</c>, taken by the gallery
    /// (<see cref="Gallery"/>) and imported by the editor. An entry without one gets a plain tile with its initial.
    /// </summary>
    public static class GuidePictures
    {
        static readonly Dictionary<string, Texture2D> cache = new();

        public static Texture2D For(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (!cache.TryGetValue(id, out var picture)) cache[id] = picture = Resources.Load<Texture2D>("Guide/" + id);
            return picture;
        }

        /// <summary>Fills an element with an entry's photograph, cropped to cover it, or a tile with its initial.</summary>
        public static void Paint(VisualElement element, GuideEntry entry)
        {
            var picture = For(entry?.Id);
            var initial = element.Q<Label>("guide-initial");
            if (picture != null)
            {
                element.style.backgroundImage = new StyleBackground(picture);
                element.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Cover));
                element.style.backgroundColor = Color.clear;
                if (initial != null) Ui.Show(initial, false);
                return;
            }

            element.style.backgroundImage = StyleKeyword.None;
            element.style.backgroundColor = new Color(0.1f, 0.25f, 0.33f, 1f);
            if (initial == null)
            {
                initial = Ui.Text("", "guide-initial");
                initial.name = "guide-initial";
                initial.pickingMode = PickingMode.Ignore;
                element.Add(initial);
            }

            Ui.Show(initial, true);
            initial.text = entry == null || string.IsNullOrEmpty(entry.Name) ? "?" : entry.Name.Substring(0, 1).ToUpperInvariant();
        }
    }
}
