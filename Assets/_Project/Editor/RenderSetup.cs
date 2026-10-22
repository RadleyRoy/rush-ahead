using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RushAhead.EditorTools
{
    /// <summary>
    /// Moves the project from URP's 2D Renderer to the 3D Universal Renderer, for the third-person look.
    /// Every quality level shares one pipeline asset, so swapping its renderer switches the whole project.
    /// </summary>
    static class RenderSetup
    {
        const string PipelinePath = "Assets/Settings/UniversalRP.asset";
        const string RendererPath = "Assets/Settings/UniversalRenderer.asset";

        // Where UniversalRenderPipelineAsset.LoadBuiltinRendererData always creates its renderer.
        const string BuiltinRendererPath = "Assets/UniversalRenderer.asset";

        public static void UseUniversalRenderer()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline.rendererDataList[0] is not UniversalRendererData)
            {
                var serializedPipeline = new SerializedObject(pipeline);
                var existing = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
                if (existing != null)
                {
                    serializedPipeline.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue = existing;
                    serializedPipeline.ApplyModifiedPropertiesWithoutUndo();
                }
                else
                {
                    // The same public path the "Create > URP Universal Renderer" menu takes, then moved beside the pipeline.
                    pipeline.LoadBuiltinRendererData(RendererType.UniversalRenderer);
                    var error = AssetDatabase.MoveAsset(BuiltinRendererPath, RendererPath);
                    if (!string.IsNullOrEmpty(error)) Debug.LogError($"[RushAhead] Could not move the renderer asset: {error}");
                }
            }

            var settings = new SerializedObject(pipeline);
            settings.FindProperty("m_MainLightShadowsSupported").boolValue = true;
            settings.FindProperty("m_SoftShadowsSupported").boolValue = true;
            settings.FindProperty("m_ShadowDistance").floatValue = 60f;
            settings.FindProperty("m_MSAA").intValue = 4; // primitives have hard silhouettes; tile GPUs make 4x cheap
            settings.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Sun, sky, ambient light and haze for a bright outdoor cartoon look, saved with the open scene.</summary>
        public static void LightScene(Material sky)
        {
            var legacy2DLight = GameObject.Find("Global Light 2D");
            if (legacy2DLight != null) Object.DestroyImmediate(legacy2DLight);

            var sun = GameObject.Find("Sun");
            if (sun == null) sun = new GameObject("Sun");
            // Not `GetComponent() ?? AddComponent()`: in the editor a missing component is a fake-null object that `??` treats as real.
            if (!sun.TryGetComponent(out Light light)) light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.intensity = 1.25f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.55f;
            sun.transform.rotation = Quaternion.Euler(52f, -35f, 0f);

            RenderSettings.skybox = sky;
            RenderSettings.sun = light;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.66f, 0.78f, 0.96f);
            RenderSettings.ambientEquatorColor = new Color(0.6f, 0.66f, 0.72f);
            RenderSettings.ambientGroundColor = new Color(0.4f, 0.34f, 0.28f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.76f, 0.88f, 1f);
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 150f;
        }
    }
}
