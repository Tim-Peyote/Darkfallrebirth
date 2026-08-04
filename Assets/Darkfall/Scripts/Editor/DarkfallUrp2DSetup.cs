#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Darkfall.Editor
{
    [InitializeOnLoad]
    public static class DarkfallUrp2DSetup
    {
        private const string SettingsFolder = "Assets/Darkfall/Settings";
        private const string RendererPath = SettingsFolder + "/Darkfall2DRenderer.asset";
        private const string PipelinePath = SettingsFolder + "/DarkfallURP.asset";

        static DarkfallUrp2DSetup()
        {
            EditorApplication.delayCall += EnsureConfigured;
        }

        [MenuItem("Darkfall/Rendering/Configure URP 2D")]
        public static void EnsureConfigured()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += EnsureConfigured;
                return;
            }

            if (!AssetDatabase.IsValidFolder(SettingsFolder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Darkfall")) AssetDatabase.CreateFolder("Assets", "Darkfall");
                AssetDatabase.CreateFolder("Assets/Darkfall", "Settings");
            }

            var renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<Renderer2DData>();
                ResourceReloader.ReloadAllNullIn(renderer, UniversalRenderPipelineAsset.packagePath);
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }
            ConfigureRenderer(renderer);

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.name = "Darkfall URP 2D";
                pipeline.supportsHDR = true;
                pipeline.msaaSampleCount = 4;
                pipeline.renderScale = 1f;
                pipeline.useSRPBatcher = true;
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureRenderer(Renderer2DData renderer)
        {
            var serialized = new SerializedObject(renderer);
            serialized.FindProperty("m_DefaultMaterialType").intValue = 0;
            serialized.FindProperty("m_LightRenderTextureScale").floatValue = 1f;
            serialized.FindProperty("m_HDREmulationScale").floatValue = 2f;
            serialized.FindProperty("m_MaxShadowRenderTextureCount").uintValue = 4;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
