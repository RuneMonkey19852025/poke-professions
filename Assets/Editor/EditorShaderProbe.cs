using System.IO;
using UnityEditor;
using UnityEngine;

namespace PokeProfessions.EditorTools
{
    /// <summary>
    /// Diagnostic probe: reports shader import messages and renders test quads
    /// to /tmp so we can verify what the custom shaders actually output.
    /// Run WITHOUT -nographics so the GPU is available.
    /// </summary>
    public static class EditorShaderProbe
    {
        public static void Run()
        {
            var sh = Resources.Load<Shader>("Shaders/UnlitColor");
            Debug.Log("[Probe] UnlitColor=" + (sh != null ? sh.name : "NULL"));
            if (sh != null)
            {
                Debug.Log("[Probe] messageCount=" + ShaderUtil.GetShaderMessageCount(sh) +
                          " isSupported=" + sh.isSupported);
                foreach (var m in ShaderUtil.GetShaderMessages(sh))
                    Debug.Log("[Probe] msg(sev=" + m.severity + "): " + m.message);
            }

            var sh2 = Resources.Load<Shader>("Shaders/VertexColorUnlit");
            Debug.Log("[Probe] VertexColorUnlit=" + (sh2 != null ? sh2.name : "NULL"));
            if (sh2 != null)
            {
                Debug.Log("[Probe] vcol messageCount=" + ShaderUtil.GetShaderMessageCount(sh2) +
                          " isSupported=" + sh2.isSupported);
                foreach (var m in ShaderUtil.GetShaderMessages(sh2))
                    Debug.Log("[Probe] vcol msg(sev=" + m.severity + "): " + m.message);
            }

            var legacy = Shader.Find("Legacy Shaders/Diffuse");
            Debug.Log("[Probe] legacy=" + (legacy != null ? legacy.name : "NULL") +
                      " supported=" + (legacy != null ? legacy.isSupported.ToString() : "-"));

            // Render a red quad with the custom unlit shader
            var camGo = new GameObject("ProbeCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            var rt = new RenderTexture(128, 128, 16);
            cam.targetTexture = rt;

            var quadGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quadGo.transform.position = new Vector3(0, 0, 5);
            var mr = quadGo.GetComponent<MeshRenderer>();

            if (sh != null)
            {
                var m = new Material(sh);
                m.color = Color.red;
                mr.sharedMaterial = m;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
                tex.Apply();
                File.WriteAllBytes("/tmp/probe_custom.png", tex.EncodeToPNG());
                var c = tex.GetPixel(64, 64);
                Debug.Log("[Probe] custom center pixel=" + c);
            }

            if (legacy != null)
            {
                var sunGo = new GameObject("ProbeSun");
                var sun = sunGo.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1f;
                var m2 = new Material(legacy);
                mr.sharedMaterial = m2;
                cam.Render();
                RenderTexture.active = rt;
                var tex2 = new Texture2D(128, 128, TextureFormat.RGBA32, false);
                tex2.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
                tex2.Apply();
                File.WriteAllBytes("/tmp/probe_legacy.png", tex2.EncodeToPNG());
                var c2 = tex2.GetPixel(64, 64);
                Debug.Log("[Probe] legacy center pixel=" + c2);
            }

            RenderTexture.active = null;
            Debug.Log("[Probe] DONE");
            EditorApplication.Exit(0);
        }
    }
}
