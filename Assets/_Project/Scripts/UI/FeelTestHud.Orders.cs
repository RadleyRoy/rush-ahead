using RushAhead.Render;
using RushAhead.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RushAhead.UI
{
    /// <summary>
    /// The order bar for the AI crewmates: Go, Hold, Brace, and Push here (then tap the water where the raft should
    /// go). Any player can give orders; the current one is lit for everyone. Keys 1 to 4 do the same on a keyboard,
    /// with Push here aimed at the mouse.
    /// </summary>
    public sealed partial class FeelTestHud
    {
        static readonly (CrewOrder order, string label)[] Orders =
        {
            (CrewOrder.Go, "Go"), (CrewOrder.Hold, "Hold"), (CrewOrder.Brace, "Brace"), (CrewOrder.PushHere, "Push here"),
        };

        Rect orderBar;
        bool pickingPoint;
        GUIStyle orderHint;

        void UpdateOrders()
        {
            if (!Game.HasAiCrew)
            {
                pickingPoint = false;
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) Game.GiveOrder(CrewOrder.Go);
                if (keyboard.digit2Key.wasPressedThisFrame) Game.GiveOrder(CrewOrder.Hold);
                if (keyboard.digit3Key.wasPressedThisFrame) Game.GiveOrder(CrewOrder.Brace);
                if (keyboard.digit4Key.wasPressedThisFrame && Mouse.current != null && WaterPoint(Mouse.current.position.ReadValue()) is { } aimed)
                    Game.GiveOrder(CrewOrder.PushHere, aimed);
            }

            // Push here: the next tap on the water (anywhere but the bar itself) is where the crew should push.
            var pointer = Pointer.current;
            if (!pickingPoint || pointer == null || !pointer.press.wasPressedThisFrame) return;
            var screen = pointer.position.ReadValue();
            var gui = new Vector2(screen.x, Screen.height - screen.y) / Scale;
            if (orderBar.Contains(gui)) return;

            pickingPoint = false;
            if (WaterPoint(screen) is { } point) Game.GiveOrder(CrewOrder.PushHere, point);
        }

        void DrawOrders(float width, float height)
        {
            orderBar = Rect.zero;
            if (!Game.HasAiCrew || Game.Run.Finished) return;

            orderHint ??= new GUIStyle(GUI.skin.box) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            const float button = 92f;
            orderBar = new Rect(width * 0.5f - Orders.Length * button * 0.5f - 6f, height - 62f, Orders.Length * button + 12f, 52f);
            GUI.Box(orderBar, GUIContent.none);

            var current = Game.CurrentOrder;
            for (var i = 0; i < Orders.Length; i++)
            {
                var (order, label) = Orders[i];
                var rect = new Rect(orderBar.x + 6f + i * button, orderBar.y + 6f, button - 4f, 40f);
                var lit = order == CrewOrder.PushHere ? pickingPoint || current.Order == order : current.Order == order;
                if (!GUI.Toggle(rect, lit, label, GUI.skin.button) || lit) continue;

                if (order == CrewOrder.PushHere) Later(() => pickingPoint = true);
                else Later(() =>
                {
                    pickingPoint = false;
                    Game.GiveOrder(order);
                });
            }

            if (pickingPoint)
                GUI.Box(new Rect(width * 0.5f - 170f, orderBar.y - 40f, 340f, 32f), "Tap the water where the crew should push", orderHint);

            // Mark where they're pushing to.
            if (current.Order == CrewOrder.PushHere && Camera.main != null)
            {
                var screen = Camera.main.WorldToScreenPoint(SimSpace.ToWorld(current.Point, 0.3f));
                if (screen.z > 0f)
                {
                    var at = new Vector2(screen.x, Screen.height - screen.y) / Scale;
                    GUI.color = new Color(1f, 0.85f, 0.15f, 0.9f);
                    GUI.DrawTexture(new Rect(at.x - 14f, at.y - 14f, 28f, 28f), circle);
                    GUI.color = Color.white;
                }
            }
        }

        /// <summary>Where a point on the screen lands on the water, in sim coordinates; null above the horizon.</summary>
        static Vector2? WaterPoint(Vector2 screen)
        {
            var camera = Camera.main;
            if (camera == null) return null;
            var ray = camera.ScreenPointToRay(screen);
            if (ray.direction.y > -1e-3f) return null;
            var hit = ray.origin + ray.direction * (-ray.origin.y / ray.direction.y);
            return new Vector2(hit.x, hit.z);
        }
    }
}
