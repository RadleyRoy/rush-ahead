using System;
using UnityEngine.UIElements;

namespace RushAhead.UI
{
    /// <summary>
    /// One screen of the game: built once, then shown and hidden as the flow moves. <see cref="Refresh"/> runs every
    /// frame while it's on screen, and only then, so a hidden screen costs nothing.
    /// </summary>
    public abstract class GameScreen
    {
        protected GameScreen(GameUi ui)
        {
            App = ui;
            Root = Ui.El("screen layer");
        }

        protected GameUi App { get; }
        protected GameController Game => App.Game;
        protected AppFlow Flow => App.Flow;

        public VisualElement Root { get; }

        /// <summary>Whether the 3D world shows through (most screens) or the screen covers it.</summary>
        public virtual bool Opaque => false;

        public abstract void Build();

        public virtual void OnShow()
        {
        }

        public virtual void OnHide()
        {
        }

        public virtual void Refresh()
        {
        }

        /// <summary>A button whose action the scripted demo can also press by name, checking it's really there and enabled.</summary>
        protected Button Btn(string name, string text, Action clicked, string classes = "btn")
        {
            var button = Ui.Btn(text, clicked, classes);
            button.name = name;
            App.Register(name, button, clicked);
            return button;
        }
    }
}
