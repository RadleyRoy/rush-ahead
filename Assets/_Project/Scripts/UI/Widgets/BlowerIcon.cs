using RushAhead.Render;
using UnityEngine;
using UnityEngine.UIElements;

namespace RushAhead.UI
{
    /// <summary>A blower's silhouette in its own colour, drawn, so each card on the Your animal screen looks like its blower.</summary>
    public sealed class BlowerIcon : VisualElement
    {
        static readonly Color Trim = new(0.2f, 0.22f, 0.27f);
        static readonly Color Wood = new(0.72f, 0.5f, 0.3f);
        static readonly Color Brass = new(0.92f, 0.72f, 0.3f);

        readonly BlowerKind kind;
        readonly Color colour;
        readonly float size;

        public BlowerIcon(BlowerKind kind, Color colour, float size = 100f)
        {
            this.kind = kind;
            this.colour = colour;
            this.size = size;
            style.width = size;
            style.height = size;
            style.flexShrink = 0;
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext context)
        {
            var p = context.painter2D;
            var s = size / 100f; // drawn on a 100 x 100 grid, y down
            Vector2 P(float x, float y) => new(x * s, y * s);

            void Shape(Color fill, params Vector2[] points)
            {
                p.fillColor = fill;
                p.BeginPath();
                p.MoveTo(points[0]);
                for (var i = 1; i < points.Length; i++) p.LineTo(points[i]);
                p.ClosePath();
                p.Fill();
            }

            void Circle(Color fill, Vector2 centre, float radius)
            {
                p.fillColor = fill;
                p.BeginPath();
                p.Arc(centre, radius * s, 0f, 360f);
                p.Fill();
            }

            void Line(Color stroke, float width, params Vector2[] points)
            {
                p.strokeColor = stroke;
                p.lineWidth = width * s;
                p.lineCap = LineCap.Round;
                p.BeginPath();
                p.MoveTo(points[0]);
                for (var i = 1; i < points.Length; i++) p.LineTo(points[i]);
                p.Stroke();
            }

            switch (kind)
            {
                case BlowerKind.HairDryer:
                    Shape(colour, P(40f, 62f), P(52f, 62f), P(46f, 92f), P(34f, 92f)); // grip
                    Circle(colour, P(42f, 40f), 24f);
                    Shape(Trim, P(60f, 30f), P(88f, 33f), P(88f, 47f), P(60f, 50f)); // nozzle
                    Circle(Trim, P(42f, 40f), 9f); // the grille at the back
                    break;
                case BlowerKind.Bellows:
                    Shape(Wood, P(10f, 30f), P(26f, 18f), P(70f, 34f), P(70f, 46f), P(26f, 50f), P(10f, 40f)); // boards
                    Shape(colour, P(28f, 30f), P(56f, 37f), P(56f, 43f), P(28f, 42f)); // painted band
                    Shape(new Color(0.36f, 0.21f, 0.13f), P(26f, 50f), P(70f, 46f), P(66f, 58f), P(28f, 66f)); // leather folds
                    Shape(Brass, P(68f, 36f), P(94f, 40f), P(94f, 44f), P(68f, 50f)); // spout
                    Line(Wood, 6f, P(16f, 34f), P(4f, 26f));
                    break;
                case BlowerKind.HandFan:
                    Line(Wood, 7f, P(50f, 70f), P(50f, 94f)); // stick
                    for (var k = 0; k < 9; k++)
                    {
                        var a0 = (-162f + k * 16f) * Mathf.Deg2Rad;
                        var a1 = a0 + 16f * Mathf.Deg2Rad;
                        var hub = P(50f, 70f);
                        Shape(k % 2 == 0 ? colour : Color.Lerp(colour, Color.white, 0.5f), hub,
                            hub + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * (54f * s), hub + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * (54f * s));
                    }

                    Circle(Trim, P(50f, 70f), 5f);
                    break;
                case BlowerKind.ShopVac:
                    Shape(colour, P(8f, 52f), P(42f, 52f), P(42f, 92f), P(8f, 92f)); // canister
                    Shape(Trim, P(6f, 48f), P(44f, 48f), P(44f, 56f), P(6f, 56f)); // lid
                    Line(Trim, 7f, P(36f, 50f), P(44f, 34f), P(58f, 28f)); // hose
                    Line(Trim, 7f, P(58f, 28f), P(80f, 24f)); // wand
                    Shape(colour, P(78f, 10f), P(94f, 12f), P(94f, 38f), P(78f, 36f)); // wide head
                    break;
                default:
                    Shape(colour, P(6f, 40f), P(34f, 40f), P(34f, 82f), P(6f, 82f)); // engine pack
                    Circle(Trim, P(20f, 40f), 6f);
                    Line(Trim, 8f, P(28f, 60f), P(40f, 54f)); // hose
                    Line(colour, 11f, P(38f, 54f), P(92f, 30f)); // the long tube
                    Line(Trim, 13f, P(88f, 32f), P(95f, 29f)); // tip
                    Shape(Trim, P(44f, 52f), P(52f, 49f), P(56f, 64f), P(48f, 66f)); // handle
                    break;
            }
        }
    }
}
