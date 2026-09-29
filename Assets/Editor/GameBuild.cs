using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PokeProfessions.EditorTools
{
    /// <summary>
    /// CLI build entry point:
    ///   Unity -batchmode -quit -projectPath ... -executeMethod PokeProfessions.EditorTools.GameBuild.BuildLinux
    /// </summary>
    public static class GameBuild
    {
        public static void BuildLinux()
        {
            var scenes = new[] { "Assets/Scenes/GameScene.unity" };
            foreach (var s in scenes)
                if (!File.Exists(s)) { Debug.LogError("Missing scene: " + s); EditorApplication.Exit(2); }

            // All UI is created at runtime, so nothing serialized references the
            // uGUI shaders — without this they get stripped and every panel
            // renders with Hidden/InternalErrorShader (the pink screen).
            EnsureAlwaysIncluded("UI/Default");
            EnsureAlwaysIncluded("Sprites/Default");
            EnsureAlwaysIncluded("Legacy Shaders/Diffuse");

            var report = BuildPipeline.BuildPlayer(
                scenes,
                "Builds/Linux/EmberwildVale.x86_64",
                BuildTarget.StandaloneLinux64,
                BuildOptions.None);

            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("Build failed: " + report.summary.result);
                EditorApplication.Exit(3);
            }

            Debug.Log("Build OK: " + report.summary.outputPath +
                      "  size=" + (report.summary.totalSize / (1024 * 1024)) + "MB");
            EditorApplication.Exit(0);
        }

        /// <summary>Adds a shader to Project Settings > Graphics > Always Included Shaders.</summary>
        static void EnsureAlwaysIncluded(string shaderName)
        {
            var sh = Shader.Find(shaderName);
            if (sh == null) { Debug.LogWarning("[Build] shader not found for inclusion: " + shaderName); return; }

            var graphicsAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (graphicsAssets == null || graphicsAssets.Length == 0)
            { Debug.LogError("[Build] could not load GraphicsSettings.asset"); return; }

            var so = new SerializedObject(graphicsAssets[0]);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            if (arr == null) { Debug.LogError("[Build] m_AlwaysIncludedShaders missing"); return; }

            for (int i = 0; i < arr.arraySize; i++)
                if (arr.GetArrayElementAtIndex(i).objectReferenceValue == sh)
                { Debug.Log("[Build] already included: " + shaderName); return; }

            arr.InsertArrayElementAtIndex(arr.arraySize);
            arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = sh;
            so.ApplyModifiedProperties();
            Debug.Log("[Build] added to Always Included Shaders: " + shaderName);
        }

        /// <summary>Vulkan-only build: bakes player settings to Vulkan as the sole graphics API.</summary>
        public static void BuildLinuxVulkan()
        {
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneLinux64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneLinux64,
                new[] { UnityEngine.Rendering.GraphicsDeviceType.Vulkan });
            Debug.Log("[Build] Graphics APIs forced to: Vulkan");
            BuildLinux();
        }

        /// <summary>Dual-API build: GLCore first (default), Vulkan as fallback at runtime via -force-vulkan.</summary>
        public static void BuildLinuxGL()
        {
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneLinux64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneLinux64,
                new[]
                {
                    UnityEngine.Rendering.GraphicsDeviceType.OpenGLCore,
                    UnityEngine.Rendering.GraphicsDeviceType.Vulkan
                });
            Debug.Log("[Build] Graphics APIs set to: OpenGLCore, Vulkan");
            BuildLinux();
        }
    }
}
