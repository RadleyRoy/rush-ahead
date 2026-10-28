using RushAhead.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace RushAhead.EditorTools
{
    /// <summary>
    /// Makes the panel every screen draws into: scaled to a 1920x1080 reference matched on height (so a phone held
    /// sideways shows the same layout at any width from 4:3 to 21:9), themed by RushAheadTheme.tss (Unity's default
    /// runtime theme plus Game.uss), and drawn over the 3D world. Called from <see cref="ContentBootstrap.Run"/>.
    /// The screens themselves are built in code (Scripts/UI/Screens).
    /// </summary>
    public static class UiBootstrap
    {
        const string Folder = "Assets/_Project/UI";
        const string ThemePath = Folder + "/RushAheadTheme.tss";
        const string PanelPath = Folder + "/GamePanelSettings.asset";

        public static PanelSettings EnsurePanelSettings()
        {
            AssetDatabase.ImportAsset(ThemePath, ImportAssetOptions.ForceSynchronousImport);
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (theme == null) Debug.LogError($"[RushAhead] {ThemePath} didn't import as a theme style sheet.");

            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, PanelPath);
            }

            panel.themeStyleSheet = theme;
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 1f;
            panel.clearColor = false;
            panel.sortingOrder = 0;
            EditorUtility.SetDirty(panel);
            return panel;
        }

        /// <summary>
        /// Hands the panel to the game controller in the open scene, which builds the screens at startup. There's no
        /// EventSystem: UI Toolkit takes its input straight from the Input System.
        /// </summary>
        public static void Wire(GameController game)
        {
            var panel = EnsurePanelSettings();
            var serialized = new SerializedObject(game);
            serialized.FindProperty("panelSettings").objectReferenceValue = panel;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
