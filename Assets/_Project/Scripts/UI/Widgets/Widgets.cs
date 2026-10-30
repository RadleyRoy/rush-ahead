using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace RushAhead.UI
{
    /// <summary>One to three stars, drawn (the built-in font has no star glyph).</summary>
    public sealed class StarRow : VisualElement
    {
        static readonly Color Lit = new(1f, 0.79f, 0.24f);
        static readonly Color Unlit = new(1f, 1f, 1f, 0.18f);
        readonly float size;
        int lit;

        public StarRow(float size = 44f)
        {
            this.size = size;
            style.width = size * 3f + size * 0.3f * 2f;
            style.height = size;
            style.flexShrink = 0;
            generateVisualContent += Draw;
        }

        public int Count
        {
            get => lit;
            set
            {
                if (lit == value) return;
                lit = value;
                MarkDirtyRepaint();
            }
        }

        void Draw(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            for (var i = 0; i < 3; i++)
            {
                var centre = new Vector2(size * 0.5f + i * size * 1.3f, size * 0.53f);
                painter.fillColor = i < lit ? Lit : Unlit;
                painter.BeginPath();
                for (var p = 0; p < 10; p++)
                {
                    var radius = p % 2 == 0 ? size * 0.5f : size * 0.21f;
                    var angle = (-90f + p * 36f) * Mathf.Deg2Rad;
                    var point = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    if (p == 0) painter.MoveTo(point);
                    else painter.LineTo(point);
                }

                painter.ClosePath();
                painter.Fill();
            }
        }
    }

    /// <summary>A raft's lives as hearts, full or empty (the built-in font has no heart either).</summary>
    public sealed class HeartRow : VisualElement
    {
        static readonly Color Full = new(1f, 0.36f, 0.42f);
        static readonly Color Empty = new(1f, 1f, 1f, 0.2f);
        readonly float size;
        readonly int hearts;
        int full;

        public HeartRow(float size = 40f, int hearts = 3)
        {
            this.size = size;
            this.hearts = hearts;
            full = hearts;
            style.width = size * hearts + size * 0.25f * (hearts - 1);
            style.height = size;
            style.flexShrink = 0;
            generateVisualContent += Draw;
        }

        public int Count
        {
            get => full;
            set
            {
                if (full == value) return;
                full = value;
                MarkDirtyRepaint();
            }
        }

        void Draw(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            for (var i = 0; i < hearts; i++)
            {
                var c = new Vector2(size * 0.5f + i * size * 1.25f, size * 0.5f);
                var s = size;
                painter.fillColor = i < full ? Full : Empty;
                painter.BeginPath();
                painter.MoveTo(c + new Vector2(0f, s * 0.42f));
                painter.BezierCurveTo(c + new Vector2(-s * 0.62f, s * 0.02f), c + new Vector2(-s * 0.5f, -s * 0.5f), c + new Vector2(0f, -s * 0.2f));
                painter.BezierCurveTo(c + new Vector2(s * 0.5f, -s * 0.5f), c + new Vector2(s * 0.62f, s * 0.02f), c + new Vector2(0f, s * 0.42f));
                painter.ClosePath();
                painter.Fill();
            }
        }
    }

    /// <summary>A round swatch of colour: fur colours and team colours.</summary>
    public sealed class Swatch : VisualElement
    {
        public Swatch(Color colour, float size = 40f)
        {
            style.width = size;
            style.height = size;
            style.flexShrink = 0;
            style.backgroundColor = colour;
            style.borderTopLeftRadius = style.borderTopRightRadius = style.borderBottomLeftRadius = style.borderBottomRightRadius = size * 0.5f;
        }

        public Color Colour
        {
            set => style.backgroundColor = value;
        }
    }

    /// <summary>A row of choices, one lit: Co-op or Team Race, 1 to 4 animals, the AI's skill.</summary>
    public sealed class Segmented : VisualElement
    {
        readonly List<Button> options = new();
        int selected = -1;

        /// <param name="name">Each choice is named name-0, name-1 and so on, for the scripted demo.</param>
        public Segmented(GameUi app, string name, IReadOnlyList<string> labels, Action<int> picked)
        {
            AddToClassList("segmented");
            for (var i = 0; i < labels.Count; i++)
            {
                var index = i;
                Action choose = () => picked(index);
                var option = Ui.Btn(labels[i], choose, "segment");
                option.name = $"{name}-{i}";
                app.Register(option.name, option, choose);
                options.Add(option);
                Add(option);
            }
        }

        public int Selected
        {
            set
            {
                if (value == selected) return;
                selected = value;
                for (var i = 0; i < options.Count; i++) Ui.SetClass(options[i], "segment--on", i == value);
            }
        }

        public void Enable(int option, bool on)
        {
            if (options[option].enabledSelf != on) options[option].SetEnabled(on);
        }
    }

    /// <summary>A named bar from 0 to 1: a blower's power, quick start, reach or burst.</summary>
    public sealed class StatBar : VisualElement
    {
        public StatBar(string label, float value)
        {
            style.flexGrow = 1;
            style.marginRight = 18;
            Add(Ui.Text(label, "small muted"));
            var track = new VisualElement();
            track.style.height = 14;
            track.style.marginTop = 6;
            track.style.backgroundColor = new Color(1f, 1f, 1f, 0.14f);
            track.style.borderTopLeftRadius = track.style.borderTopRightRadius = track.style.borderBottomLeftRadius = track.style.borderBottomRightRadius = 7;
            var fill = new VisualElement();
            fill.style.height = 14;
            fill.style.width = new Length(Mathf.Clamp01(value) * 100f, LengthUnit.Percent);
            fill.style.backgroundColor = new Color(1f, 0.79f, 0.24f);
            fill.style.borderTopLeftRadius = fill.style.borderTopRightRadius = fill.style.borderBottomLeftRadius = fill.style.borderBottomRightRadius = 7;
            track.Add(fill);
            Add(track);
        }
    }

    public static class Gradients
    {
        /// <summary>A tall, thin texture fading from top to bottom, to stretch behind a screen.</summary>
        public static Texture2D Vertical(Color top, Color bottom, int height = 128)
        {
            var texture = new Texture2D(1, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (var y = 0; y < height; y++) texture.SetPixel(0, y, Color.Lerp(bottom, top, y / (height - 1f)));
            texture.Apply();
            return texture;
        }
    }
}
