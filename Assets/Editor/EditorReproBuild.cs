using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace PokeProfessions.EditorTools
{
    /// <summary>Runtime behaviour used by the repro scene: creates objects the way WorldBuilder does.</summary>
    public class ReproRuntimeBootstrap : MonoBehaviour
    {
        void Awake()
        {
            // A: quad with runtime material from Resources shader (like FlatMat)
            var a = GameObject.CreatePrimitive(PrimitiveType.Quad);
            a.transform.position = new Vector3(-1.2f, 0f, 5f);
            var sh = Resources.Load<Shader>("Shaders/UnlitColor");
            Debug.Log("[ReproRT] resources shader=" + (sh != null ? sh.name : "NULL"));
            var m = new Material(sh);
            m.color = Color.red;
            a.GetComponent<MeshRenderer>().sharedMaterial = m;

            // B: quad with NO explicit material (Default-Material / built-in Standard)
            var b = GameObject.CreatePrimitive(PrimitiveType.Quad);
            b.transform.position = new Vector3(1.2f, 0f, 5f);
            Debug.Log("[ReproRT] B default mat shader=" + b.GetComponent<MeshRenderer>().sharedMaterial.shader.name +
                      " supported=" + b.GetComponent<MeshRenderer>().sharedMaterial.shader.isSupported);

            // C: quad with Shader.Find legacy diffuse (like the FlatMat fallback chain)
            var c = GameObject.CreatePrimitive(PrimitiveType.Quad);
            c.transform.position = new Vector3(0f, -1.2f, 5f);
            var sh3 = Shader.Find("Legacy Shaders/Diffuse");
            var m3 = new Material(sh3);
            c.GetComponent<MeshRenderer>().sharedMaterial = m3;
            Debug.Log("[ReproRT] C legacy diffuse found=" + (sh3 != null) + " supported=" + (sh3 != null && sh3.isSupported));
        }
    }

    /// <summary>
    /// Second repro: EMPTY scene, everything created at RUNTIME in the player.
    ///   Unity -batchmode -quit -projectPath ... -executeMethod PokeProfessions.EditorTools.EditorReproBuild.BuildReproRuntime
    /// </summary>
    public static class EditorReproBuild
    {
        public static void BuildReproRuntime()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var root = new GameObject("ReproRuntimeBootstrap");
            root.AddComponent<ReproRuntimeBootstrap>();

            var camGo = new GameObject("MainCamera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            camGo.transform.position = Vector3.zero;
            camGo.AddComponent<AudioListener>();

            const string scenePath = "Assets/Scenes/ReproRuntimeScene.unity";
            EditorSceneManager.SaveScene(scene, scenePath);

            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneLinux64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneLinux64,
                new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLCore });

            var report = BuildPipeline.BuildPlayer(
                new[] { scenePath },
                "Builds/Repro/ReproRT.x86_64",
                BuildTarget.StandaloneLinux64,
                BuildOptions.None);

            Debug.Log("[Repro] result=" + report.summary.result +
                      " size=" + (report.summary.totalSize / (1024 * 1024)) + "MB");
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 3);
        }
    }
}
