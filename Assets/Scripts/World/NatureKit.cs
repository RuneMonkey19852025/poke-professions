using System.Collections.Generic;
using UnityEngine;

namespace PokeProfessions.World
{
    /// <summary>
    /// Runtime loader for the Quaternius Stylized Nature free kit (CC0)
    /// under Resources/Models/Quaternius/Nature (+ NatureTextures).
    /// Props render through the KitProp shader: the pack's diffuse PNG bound
    /// to the game's toon-style lighting with alpha clipping for leaf cards.
    /// Every lookup fails gracefully so the procedural props remain the
    /// fallback.
    /// </summary>
    public static class NatureKit
    {
        const string ModelRoot = "Models/Quaternius/Nature/";
        const string TexRoot = "Models/Quaternius/NatureTextures/";

        static readonly Dictionary<string, GameObject> modelCache = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();
        static readonly Dictionary<string, Texture2D> texCache = new Dictionary<string, Texture2D>();

        static Shader vertexShader;

        // ---- diffuse texture(s) referenced by each kit FBX (per mesh part) ----
        // Derived from the FBX texture bindings; bark + leaf parts each need
        // their own material. Names here exclude the .png extension.
        static readonly Dictionary<string, string[]> ModelTextures = new Dictionary<string, string[]>
        {
            { "Bush_Common", new[] { "Leaves_TwistedTree_C" } },
            { "Bush_Common_Flowers", new[] { "Flowers" } },
            { "Clover_1", new[] { "Leaves" } },
            { "Clover_2", new[] { "Leaves" } },
            { "CommonTree_1", new[] { "Bark_NormalTree", "Leaves_NormalTree_C" } },
            { "CommonTree_2", new[] { "Bark_NormalTree", "Leaves_NormalTree_C" } },
            { "CommonTree_3", new[] { "Bark_NormalTree", "Leaves_NormalTree_C" } },
            { "CommonTree_4", new[] { "Bark_NormalTree", "Leaves_NormalTree_C" } },
            { "CommonTree_5", new[] { "Bark_NormalTree", "Leaves_NormalTree_C" } },
            { "DeadTree_1", new[] { "Bark_DeadTree" } },
            { "DeadTree_2", new[] { "Bark_DeadTree" } },
            { "DeadTree_3", new[] { "Bark_DeadTree" } },
            { "DeadTree_4", new[] { "Bark_DeadTree" } },
            { "DeadTree_5", new[] { "Bark_DeadTree" } },
            { "Fern_1", new[] { "Leaves" } },
            { "Flower_3_Group", new[] { "Flowers", "Leaves" } },
            { "Flower_3_Single", new[] { "Flowers", "Leaves" } },
            { "Flower_4_Group", new[] { "Flowers", "Leaves" } },
            { "Flower_4_Single", new[] { "Flowers", "Leaves" } },
            { "Grass_Common_Short", new[] { "Grass" } },
            { "Grass_Common_Tall", new[] { "Grass" } },
            { "Grass_Wispy_Short", new[] { "Grass" } },
            { "Grass_Wispy_Tall", new[] { "Grass" } },
            { "Mushroom_Common", new[] { "Mushrooms" } },
            { "Mushroom_Laetiporus", new[] { "Mushrooms" } },
            { "Pebble_Round_1", new[] { "PathRocks_Diffuse" } },
            { "Pebble_Round_2", new[] { "PathRocks_Diffuse" } },
            { "Pebble_Round_3", new[] { "PathRocks_Diffuse" } },
            { "Pebble_Round_4", new[] { "PathRocks_Diffuse" } },
            { "Pebble_Round_5", new[] { "PathRocks_Diffuse" } },
            { "Pebble_Square_1", new[] { "PathRocks_Diffuse" } },
            { "Pebble_Square_2", new[] { "PathRocks_Diffuse" } },
            { "Pebble_Square_3", new[] { "PathRocks_Diffuse" } },
            { "Pebble_Square_4", new[] { "PathRocks_Diffuse" } },
            { "Pebble_Square_5", new[] { "PathRocks_Diffuse" } },
            { "Pebble_Square_6", new[] { "PathRocks_Diffuse" } },
            { "Petal_1", new[] { "Flowers" } },
            { "Petal_2", new[] { "Flowers" } },
            { "Petal_3", new[] { "Flowers" } },
            { "Petal_4", new[] { "Flowers" } },
            { "Petal_5", new[] { "Flowers" } },
            { "Pine_1", new[] { "Bark_NormalTree", "Leaf_Pine_C" } },
            { "Pine_2", new[] { "Bark_NormalTree", "Leaf_Pine_C" } },
            { "Pine_3", new[] { "Bark_NormalTree", "Leaf_Pine_C" } },
            { "Pine_4", new[] { "Bark_NormalTree", "Leaf_Pine_C" } },
            { "Pine_5", new[] { "Bark_NormalTree", "Leaf_Pine_C" } },
            { "Plant_1", new[] { "Leaves" } },
            { "Plant_1_Big", new[] { "Leaves" } },
            { "Plant_7", new[] { "Leaves" } },
            { "Plant_7_Big", new[] { "Leaves" } },
            { "RockPath_Round_Small_1", new[] { "PathRocks_Diffuse" } },
            { "RockPath_Round_Small_2", new[] { "PathRocks_Diffuse" } },
            { "RockPath_Round_Small_3", new[] { "PathRocks_Diffuse" } },
            { "RockPath_Round_Thin", new[] { "PathRocks_Diffuse" } },
            { "RockPath_Round_Wide", new[] { "PathRocks_Diffuse" } },
            { "RockPath_Square_Small_1", new[] { "PathRocks_Diffuse" } },
            { "RockPath_Square_Small_2", new[] { "PathRocks_Diffuse" } },
            { "RockPath_Square_Small_3", new[] { "PathRocks_Diffuse" } },
            { "RockPath_Square_Thin", new[] { "PathRocks_Diffuse" } },
            { "RockPath_Square_Wide", new[] { "PathRocks_Diffuse" } },
            { "Rock_Medium_1", new[] { "Rocks_Diffuse" } },
            { "Rock_Medium_2", new[] { "Rocks_Diffuse" } },
            { "Rock_Medium_3", new[] { "Rocks_Diffuse" } },
            { "TwistedTree_1", new[] { "Bark_TwistedTree", "Leaves_TwistedTree_C" } },
            { "TwistedTree_2", new[] { "Bark_TwistedTree", "Leaves_TwistedTree_C" } },
            { "TwistedTree_3", new[] { "Bark_TwistedTree", "Leaves_TwistedTree_C" } },
            { "TwistedTree_4", new[] { "Bark_TwistedTree", "Leaves_TwistedTree_C" } },
            { "TwistedTree_5", new[] { "Bark_TwistedTree", "Leaves_TwistedTree_C" } },
        };

        /// <summary>True when the nature kit FBX are present in the build.</summary>
        public static bool Available => LoadProp("CommonTree_1") != null;

        /// <summary>Load a nature kit prop prefab by model name (no extension), or null.</summary>
        public static GameObject LoadProp(string modelName)
        {
            if (string.IsNullOrEmpty(modelName)) return null;
            GameObject go;
            if (modelCache.TryGetValue(modelName, out go) && go != null) return go;
            go = Resources.Load<GameObject>(ModelRoot + modelName);
            if (go != null) modelCache[modelName] = go;
            return go;
        }

        static Texture2D Tex(string texName)
        {
            if (string.IsNullOrEmpty(texName)) return null;
            Texture2D t;
            if (texCache.TryGetValue(texName, out t) && t != null) return t;
            t = Resources.Load<Texture2D>(TexRoot + texName);
            if (t != null) texCache[texName] = t;
            return t;
        }

        /// <summary>
        /// Shared material for kit props: KitProp shader (toon lighting +
        /// alpha clip) with the pack's diffuse texture and an optional tint.
        /// </summary>
        public static Material PropMat(string texName, string tintHex = null)
        {
            var key = (texName ?? "") + "|" + (tintHex ?? "");
            Material m;
            if (matCache.TryGetValue(key, out m) && m != null) return m;
            if (vertexShader == null)
            {
                vertexShader = Resources.Load<Shader>("Shaders/KitProp");
                if (vertexShader == null) vertexShader = Shader.Find("PokeProfessions/KitProp");
            }
            if (vertexShader == null) return null;
            m = new Material(vertexShader);
            var tex = Tex(texName);
            if (tex != null && m.HasProperty("_MainTex")) m.mainTexture = tex;
            if (m.HasProperty("_Tint"))
                m.SetColor("_Tint", string.IsNullOrEmpty(tintHex)
                    ? Color.white : WorldBuilder.ColorFromHex(tintHex));
            matCache[key] = m;
            return m;
        }

        /// <summary>
        /// Spawn a nature kit prop under a parent, scaled so its largest
        /// dimension (measured from the instantiated renderers) equals
        /// <paramref name="targetSize"/> world units. The kit ships with
        /// wildly inconsistent raw units (grass 130+, trees 700+), so
        /// bounds-based normalization keeps every prop at its intended
        /// size regardless of the FBX's unit scale.
        /// </summary>
        public static Transform Spawn(Transform parent, string modelName, Vector3 localPos,
                                      float targetSize, float yawDeg, string tintHex = null)
        {
            var prefab = LoadProp(modelName);
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab);
            go.name = "Nature_" + modelName;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            go.transform.localScale = Vector3.one;

            // bind per-mesh-part textures so bark/leaf parts read correctly;
            // parts with no table entry keep Unity's auto-imported material
            var renderers = go.GetComponentsInChildren<MeshRenderer>();
            string[] texs;
            ModelTextures.TryGetValue(modelName, out texs);
            var bounds = new Bounds(go.transform.position, Vector3.zero);
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (texs != null && texs.Length > 0)
                {
                    var mat = PropMat(PickTex(texs, renderers[i].name), tintHex);
                    if (mat != null) renderers[i].sharedMaterial = mat;
                }
                var cols = renderers[i].GetComponents<Collider>();
                for (int k = 0; k < cols.Length; k++) Object.Destroy(cols[k]);
                if (renderers[i].gameObject.activeInHierarchy)
                {
                    var b = renderers[i].bounds;
                    if (!hasBounds) { bounds = b; hasBounds = true; }
                    else bounds.Encapsulate(b);
                }
            }

            float maxDim = hasBounds ? Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) : 0f;
            float s = maxDim > 0.0001f ? targetSize / maxDim : targetSize;
            go.transform.localScale = Vector3.one * s;
            return go.transform;
        }

        /// <summary>Intended world size (largest dimension) for a kit prop.
        /// Trees ~7u to match the procedural trunks, small foliage under 1u.</summary>
        public static float TargetSize(string modelName)
        {
            if (string.IsNullOrEmpty(modelName)) return 1f;
            if (modelName.StartsWith("CommonTree") || modelName.StartsWith("DeadTree") ||
                modelName.StartsWith("TwistedTree") || modelName.StartsWith("Pine")) return 7f;
            if (modelName.StartsWith("Bush")) return 1.5f;
            if (modelName.StartsWith("Grass")) return 1.1f;
            if (modelName.StartsWith("Fern")) return 1.4f;
            if (modelName.StartsWith("Flower") || modelName.StartsWith("Petal") ||
                modelName.StartsWith("Clover")) return 0.7f;
            if (modelName.StartsWith("Mushroom")) return 0.8f;
            if (modelName.StartsWith("Plant")) return 1.1f;
            if (modelName.StartsWith("Rock")) return 1.9f;
            if (modelName.StartsWith("Pebble")) return 0.45f;
            if (modelName.StartsWith("RockPath")) return 1.6f;
            return 1f;
        }

        /// <summary>Choose the texture entry matching a renderer's mesh part
        /// (bark vs leaves), falling back to the first entry.</summary>
        static string PickTex(string[] texs, string rendererName)
        {
            if (texs == null || texs.Length == 0) return null;
            if (texs.Length == 1) return texs[0];
            string rn = rendererName.ToLower();
            for (int i = 0; i < texs.Length; i++)
            {
                string tl = texs[i].ToLower();
                if ((rn.Contains("leaf") || rn.Contains("flower")) && tl.Contains("leaf")) return texs[i];
                if ((rn.Contains("bark") || rn.Contains("trunk") || rn.Contains("wood")) && tl.Contains("bark")) return texs[i];
            }
            for (int i = 0; i < texs.Length; i++)
            {
                string tl = texs[i].ToLower();
                if (rn.Contains(tl) || tl.Contains(rn)) return texs[i];
            }
            return texs[0];
        }

        /// <summary>Convenience: pick a random model name from a list.</summary>
        public static string Pick(params string[] names)
        {
            return names != null && names.Length > 0 ? names[Random.Range(0, names.Length)] : null;
        }

        /// <summary>
        /// Scatter a few kit props around a gathering node so nodes read as
        /// part of the world. No-op when the kit is missing.
        /// </summary>
        public static void Decorate(Transform nodeRoot, string kind, float radius)
        {
            if (!Available) return;
            string[] pool;
            switch (kind)
            {
                case "rock":  pool = new[] { "Pebble_Round_1", "Pebble_Round_3", "Pebble_Square_2", "Rock_Medium_1" }; break;
                case "shore": pool = new[] { "Pebble_Round_2", "Pebble_Round_4", "Rock_Medium_2", "Rock_Medium_3" }; break;
                default:      pool = new[] { "Grass_Common_Tall", "Grass_Wispy_Tall", "Flower_3_Single", "Flower_4_Single", "Bush_Common" }; break; // bush
            }
            int count = Random.Range(3, 6);
            for (int i = 0; i < count; i++)
            {
                var name = Pick(pool);
                var a = Random.Range(0f, Mathf.PI * 2f);
                var r = radius + Random.Range(-0.15f, 0.45f);
                float size = TargetSize(name) * Random.Range(0.7f, 1.15f);
                Spawn(nodeRoot, name,
                      new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r),
                      size, Random.Range(0f, 360f));
            }
        }
    }
}
