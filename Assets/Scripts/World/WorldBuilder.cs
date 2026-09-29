using System.Collections.Generic;
using UnityEngine;
using PokeProfessions.Core;

namespace PokeProfessions.World
{
    /// <summary>
    /// Builds the Emberwild Vale procedurally at runtime. The island is
    /// 200x200 units; zones: meadow (center), ridge (east), lake (north),
    /// hollow (south), hollow_deep (southeast cave) — reachable through
    /// gates that open as the story progresses.
    /// </summary>
    public class WorldBuilder : MonoBehaviour
    {
        [Header("Tuning")]
        public float worldSize = 200f;

        public Transform Player;
        public Transform InteractablesRoot;
        public Transform ZoneVolumeRoot;

        // zone marker positions (y = ground height at that xz)
        public static readonly Dictionary<string, Vector3> ZoneCenters = new Dictionary<string, Vector3>
        {
            { "meadow", new Vector3(0f, 0f, 0f) },
            { "ridge", new Vector3(70f, 0f, 15f) },
            { "lake", new Vector3(-8f, 0f, -78f) },
            { "hollow", new Vector3(10f, 0f, 75f) },
            { "hollow_deep", new Vector3(55f, 0f, 82f) },
        };

        readonly List<GameObject> spawned = new List<GameObject>();

        public static float GroundHeight(float x, float z)
        {
            float h = Mathf.PerlinNoise(x * 0.02f, z * 0.02f) * 4f;
            h += Mathf.PerlinNoise(x * 0.08f, z * 0.08f) * 1.2f;

            // ridge plateau in the east
            float ridge = Mathf.Exp(-Mathf.Pow((x - 70f) / 28f, 2f)) * 9f * Mathf.PerlinNoise(x * 0.05f, z * 0.05f);
            h += ridge;

            // lake basin in the north
            float lake = Mathf.Exp(-Mathf.Pow((x + 8f) / 30f, 2f) - Mathf.Pow((z + 78f) / 26f, 2f)) * 7f;
            h -= lake;

            // hollow dip in the south
            float hollow = Mathf.Exp(-Mathf.Pow((x - 10f) / 26f, 2f) - Mathf.Pow((z - 75f) / 30f, 2f)) * 5f;
            h -= hollow * 0.5f;

            // island falloff
            float d = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) / (0.5f * 200f);
            h -= Mathf.Pow(Mathf.Clamp01(d), 6f) * 8f;

            return h;
        }

        // ---- bridge deck constants (must match BuildBridge) ----
        const float BridgeX0 = 38f, BridgeX1 = 52f, BridgeZ = 15f, BridgeHalfWidth = 2.45f, BridgeArch = 1.5f;

        /// <summary>
        /// Walkable surface height: terrain, plus the bridge deck when the
        /// point is on the bridge. PlayerController snaps to THIS, not to
        /// GroundHeight, so characters walk over the bridge instead of
        /// sinking through the planks.
        /// </summary>
        public static float SurfaceHeight(float x, float z)
        {
            float h = GroundHeight(x, z);
            if (x >= BridgeX0 - 0.1f && x <= BridgeX1 + 0.1f && Mathf.Abs(z - BridgeZ) <= BridgeHalfWidth)
            {
                float t = Mathf.Clamp01((x - BridgeX0) / (BridgeX1 - BridgeX0));
                float deck = GroundHeight(x, z) + 0.2f + Mathf.Sin(t * Mathf.PI) * BridgeArch;
                h = Mathf.Max(h, deck);
            }
            return h;
        }

        public void Build()
        {
            spawned.Clear();
            EnsureStyle();
            BuildTerrain();
            BuildWater();
            BuildBridge();
            BuildGrass();
            BuildZoneVolumes();
            BuildProps();
        }

        static WorldStyle style;
        void EnsureStyle()
        {
            if (style != null) return;
            var go = new GameObject("WorldStyle");
            style = go.AddComponent<WorldStyle>();
            spawned.Add(go);
        }

        readonly List<Transform> swayingCrowns = new List<Transform>();
        readonly List<Transform> forgeEmbers = new List<Transform>();

        void Update()
        {
            float t = Time.time;
            for (int i = 0; i < swayingCrowns.Count; i++)
            {
                var c = swayingCrowns[i];
                if (c == null) { swayingCrowns.RemoveAt(i); i--; continue; }
                float ph = t * 1.4f + i * 1.73f;
                c.localRotation = Quaternion.Euler(Mathf.Sin(ph) * 2.0f, Mathf.Sin(ph * 0.6f) * 4.0f, Mathf.Cos(ph * 0.8f) * 1.5f);
            }
            for (int i = 0; i < forgeEmbers.Count; i++)
            {
                var e = forgeEmbers[i];
                if (e == null) { forgeEmbers.RemoveAt(i); i--; continue; }
                float ph = t * 9f + i * 2.31f;
                float baseY = e.name == "flame" ? 0.6f : 0.26f;
                e.localScale = new Vector3(e.localScale.x, baseY * (1f + Mathf.Sin(ph) * 0.12f), e.localScale.z);
            }
        }

        public void Clear()
        {
            foreach (var go in spawned) if (go != null) Destroy(go);
            spawned.Clear();
        }

        // ---------------- terrain ----------------
        void BuildTerrain()
        {
            const int seg = 100; // 100x100 quads = 20k tris, fine
            var size = worldSize;
            var mesh = new Mesh { name = "ValeTerrain" };

            var verts = new Vector3[(seg + 1) * (seg + 1)];
            var uvs = new Vector2[verts.Length];
            var tris = new int[seg * seg * 6];
            var colors = new Color[verts.Length];

            var meadowC = ColorFromHex("#7EC850");
            var ridgeC = ColorFromHex("#C9A06A");
            var lakeC = ColorFromHex("#5FBF9A");
            var hollowC = ColorFromHex("#3E7D5A");
            var deepC = ColorFromHex("#2C5445");

            int i = 0;
            for (int zi = 0; zi <= seg; zi++)
            {
                for (int xi = 0; xi <= seg; xi++)
                {
                    float x = (xi / (float)seg - 0.5f) * size;
                    float z = (zi / (float)seg - 0.5f) * size;
                    float y = GroundHeight(x, z);
                    verts[i] = new Vector3(x, y, z);
                    uvs[i] = new Vector2(xi / (float)seg, zi / (float)seg);

                    // vertex color by zone — saturated modern palette
                    Color c = meadowC;
                    if (x > 40f) c = Color.Lerp(meadowC, ridgeC, Mathf.Clamp01((x - 40f) / 18f));
                    if (z < -50f) c = Color.Lerp(meadowC, lakeC, Mathf.Clamp01((-z - 50f) / 18f));
                    if (z > 52f) c = Color.Lerp(meadowC, hollowC, Mathf.Clamp01((z - 52f) / 18f));
                    if (x > 30f && z > 60f) c = deepC;
                    // sandy ring around the shore
                    if (y < 0.35f) c = Color.Lerp(c, ColorFromHex("#E8DCA8"), 0.75f);
                    // rocky grey above the treeline on the ridge
                    if (y > 7.5f && x > 40f) c = Color.Lerp(c, ColorFromHex("#9C9284"), Mathf.Clamp01((y - 7.5f) / 3f));
                    // painterly variation: subtle checker + noise mottle
                    float checker = ((xi + zi) % 2 == 0) ? 1.035f : 0.965f;
                    float mottle = 1f + (Mathf.PerlinNoise(x * 0.35f, z * 0.35f) - 0.5f) * 0.12f;
                    colors[i] = c * checker * mottle;
                    i++;
                }
            }

            int t = 0;
            for (int zi = 0; zi < seg; zi++)
            {
                for (int xi = 0; xi < seg; xi++)
                {
                    int a = zi * (seg + 1) + xi;
                    int b = a + 1;
                    int c2 = a + seg + 1;
                    int d = c2 + 1;
                    tris[t++] = a; tris[t++] = c2; tris[t++] = b;
                    tris[t++] = b; tris[t++] = c2; tris[t++] = d;
                }
            }

            mesh.vertices = verts; mesh.triangles = tris; mesh.uv = uvs; mesh.colors = colors;
            mesh.RecalculateNormals();

            var terrain = new GameObject("ValeTerrain");
            terrain.AddComponent<MeshFilter>().sharedMesh = mesh;
            terrain.AddComponent<MeshRenderer>().material = LitVertexColorMat();
            var col = terrain.AddComponent<MeshCollider>();
            col.sharedMesh = mesh;
            spawned.Add(terrain);
        }

        static Material s_vcolMat;
        public static Material LitVertexColorMat()
        {
            if (s_vcolMat == null)
            {
                // Resources first: in player builds Shader.Find only sees
                // shaders that survived stripping; Resources.Load always works.
                var sh = Resources.Load<Shader>("Shaders/VertexColorUnlit");
                if (sh == null) sh = Shader.Find("PokeProfessions/VertexColorUnlit");
                if (sh == null) sh = Resources.Load<Shader>("Shaders/UnlitColor");
                if (sh == null) sh = Shader.Find("PokeProfessions/UnlitColor");
                if (sh == null) sh = Shader.Find("Legacy Shaders/Diffuse");
                if (sh == null) sh = Shader.Find("Sprites/Default");
                if (sh == null)
                {
                    Debug.LogError("[World] No shader available for terrain!");
                    return null;
                }
                Debug.Log("[World] Terrain shader: '" + sh.name + "' supported=" + sh.isSupported);
                s_vcolMat = new Material(sh);
            }
            return s_vcolMat;
        }

        void BuildWater()
        {
            var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "Mirrorlake";
            water.transform.position = new Vector3(-8f, 0.45f, -78f);
            water.transform.localScale = new Vector3(6f, 1f, 5f);
            var mr = water.GetComponent<MeshRenderer>();
            var wsh = Resources.Load<Shader>("Shaders/ToonWater");
            if (wsh == null) wsh = Shader.Find("PokeProfessions/ToonWater");
            if (wsh != null) mr.material = new Material(wsh);
            else mr.material = ToonMat("#4A98C8");
            water.GetComponent<Collider>().isTrigger = false;
            spawned.Add(water);
        }

        void BuildBridge()
        {
            // arched plank bridge over the river gorge between meadow and ridge
            for (int i = 0; i < 14; i++)
            {
                var plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plank.name = "plank" + i;
                float t = i / 13f;
                float x = Mathf.Lerp(BridgeX0, BridgeX1, t);
                float z = BridgeZ + Mathf.Sin(t * Mathf.PI) * 0.5f;
                float y = GroundHeight(x, z) + 0.2f + Mathf.Sin(t * Mathf.PI) * BridgeArch;
                plank.transform.position = new Vector3(x, y, z);
                plank.transform.localScale = new Vector3(1.6f, 0.15f, 4.5f);
                plank.GetComponent<MeshRenderer>().material = ToonMat(i % 2 == 0 ? "#A4713D" : "#96653A");
                spawned.Add(plank);
            }
            // rope railings
            foreach (float side in new[] { -2.1f, 2.1f })
            {
                for (int i = 0; i < 7; i++)
                {
                    var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    post.name = "railpost" + side + i;
                    float t = i / 6f;
                    float x = Mathf.Lerp(BridgeX0 + 0.2f, BridgeX1 - 0.2f, t);
                    float z = BridgeZ + side;
                    float y = GroundHeight(x, z) + 0.4f + Mathf.Sin(t * Mathf.PI) * BridgeArch;
                    post.transform.position = new Vector3(x, y + 0.45f, z);
                    post.transform.localScale = new Vector3(0.14f, 0.45f, 0.14f);
                    post.GetComponent<MeshRenderer>().material = ToonMat("#7A5230");
                    var c = post.GetComponent<Collider>(); if (c != null) Destroy(c);
                    spawned.Add(post);
                }
            }
        }

        // ---------------- grass field (one merged mesh, stylized tufts) ----------------
        void BuildGrass()
        {
            const int count = 900;
            var verts = new List<Vector3>(count * 12);
            var norms = new List<Vector3>(count * 12);
            var tris = new List<int>(count * 24);
            var cols = new List<Color>(count * 12);
            var baseGrass = ColorFromHex("#5FB944");
            var baseDry = ColorFromHex("#B8C84E");

            for (int i = 0; i < count; i++)
            {
                // cluster around random hotspots for a natural clumped look
                float cx = Random.Range(-88f, 88f), cz = Random.Range(-88f, 88f);
                float gy = GroundHeight(cx, cz);
                if (gy < 0.25f) { i--; continue; }
                if (gy > 7.5f) continue;

                int blades = 3;
                for (int b = 0; b < blades; b++)
                {
                    float x = cx + Random.Range(-1.6f, 1.6f);
                    float z = cz + Random.Range(-1.6f, 1.6f);
                    float y = GroundHeight(x, z);
                    if (y < 0.25f) continue;

                    float h = Random.Range(0.35f, 0.75f);
                    float w = Random.Range(0.05f, 0.1f);
                    float ang = Random.Range(0f, Mathf.PI);
                    float dx = Mathf.Cos(ang) * w, dz = Mathf.Sin(ang) * w;
                    Color c = Color.Lerp(baseGrass, baseDry, Random.value * 0.5f);
                    c *= Random.Range(0.88f, 1.1f);

                    int vi = verts.Count;
                    verts.Add(new Vector3(x - dx, y, z - dz));
                    verts.Add(new Vector3(x + dx, y, z + dz));
                    verts.Add(new Vector3(x + dx * 0.4f, y + h, z + dz * 0.4f));
                    verts.Add(new Vector3(x - dx * 0.4f, y + h, z - dz * 0.4f));
                    norms.Add(Vector3.up); norms.Add(Vector3.up); norms.Add(Vector3.up); norms.Add(Vector3.up);
                    cols.Add(c * 0.85f); cols.Add(c * 0.85f); cols.Add(c); cols.Add(c);
                    tris.Add(vi); tris.Add(vi + 1); tris.Add(vi + 2);
                    tris.Add(vi); tris.Add(vi + 2); tris.Add(vi + 3);
                }
            }

            var mesh = new Mesh { name = "ValeGrass" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetTriangles(tris, 0);
            mesh.SetColors(cols);

            var grassGo = new GameObject("ValeGrass");
            grassGo.AddComponent<MeshFilter>().sharedMesh = mesh;
            var gmr = grassGo.AddComponent<MeshRenderer>();
            var sh = Resources.Load<Shader>("Shaders/VertexColorUnlit");
            if (sh == null) sh = Shader.Find("PokeProfessions/VertexColorUnlit");
            if (sh != null) gmr.material = new Material(sh);
            spawned.Add(grassGo);
        }

        void BuildZoneVolumes()
        {
            if (ZoneVolumeRoot == null) return;
            AddZone("meadow", new Vector3(0, 0, 0), new Vector3(56, 12, 56));
            AddZone("ridge", new Vector3(70, 5, 15), new Vector3(50, 14, 50));
            AddZone("lake", new Vector3(-8, 0, -78), new Vector3(48, 12, 40));
            AddZone("hollow", new Vector3(10, 0, 75), new Vector3(46, 12, 40));
            AddZone("hollow_deep", new Vector3(55, 0, 82), new Vector3(24, 12, 22));
        }

        void AddZone(string id, Vector3 center, Vector3 size)
        {
            var go = new GameObject("Zone_" + id);
            go.transform.SetParent(ZoneVolumeRoot, false);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(center.x, center.y + 4f, center.z);
            box.size = size;
            var z = go.AddComponent<ZoneVolume>();
            z.zoneId = id;
            spawned.Add(go);
        }

        // ---------------- props & interactables ----------------
        void BuildProps()
        {
            // trees, rocks scattered per zone
            ScatterTrees("meadow", 40, new Vector2(-30, 30), new Vector2(-30, 30));
            ScatterTrees("ridge", 26, new Vector2(48, 95), new Vector2(-8, 40), "#8A6A48");
            ScatterTrees("hollow", 34, new Vector2(-12, 32), new Vector2(58, 92), "#4A6A4A");

            // rocks for silhouette variety
            ScatterRocks("meadow", 12, new Vector2(-32, 32), new Vector2(-32, 32));
            ScatterRocks("ridge", 18, new Vector2(46, 95), new Vector2(-10, 42));

            // gathering nodes
            // Lv0 starter nodes around the ranch: gatherable with zero training
            SpawnNode("bush_meadowgrass", new Vector3(4, 0, -10));
            SpawnNode("bush_meadowgrass", new Vector3(11, 0, -9));
            SpawnNode("bush_meadowgrass", new Vector3(9, 0, -20));
            SpawnNode("rock_pebble", new Vector3(2, 0, -18));
            SpawnNode("rock_pebble", new Vector3(14, 0, -15));
            SpawnNode("spot_creek", new Vector3(-2, 0, -26));

            SpawnNode("bush_bitterleaf", new Vector3(8, 0, -12));
            SpawnNode("bush_bitterleaf", new Vector3(-14, 0, 6));
            SpawnNode("bush_bitterleaf", new Vector3(-6, 0, 22));
            SpawnNode("bush_fiber", new Vector3(16, 0, 4));
            SpawnNode("bush_fiber", new Vector3(-20, 0, -10));
            SpawnNode("bush_fiber", new Vector3(22, 0, -18));
            SpawnNode("bush_emberpepper", new Vector3(30, 0, 26));
            SpawnNode("stump_glowcap", new Vector3(12, 0, 66));
            SpawnNode("stump_glowcap", new Vector3(-8, 0, 80));

            SpawnNode("rock_stone", new Vector3(24, 0, 34));
            SpawnNode("rock_stone", new Vector3(-26, 0, 28));
            SpawnNode("rock_copper", new Vector3(62, 0, 4));
            SpawnNode("rock_copper", new Vector3(74, 0, 26));
            SpawnNode("rock_iron", new Vector3(84, 0, 18));
            SpawnNode("rock_moon", new Vector3(55, 0, 82));

            SpawnNode("spot_river", new Vector3(-30, 0, -40));
            SpawnNode("spot_river", new Vector3(-2, 0, -46));
            SpawnNode("spot_lake", new Vector3(-8, 0, -62));

            // crafting stations
            SpawnStation("forge", "Brann's Forge", new Vector3(-24, 0, -2), "#8A4A30");
            SpawnStation("campfire", "Meadow Campfire", new Vector3(6, 0, -4), "#D06A30");
            SpawnStation("workbench", "Post Workbench", new Vector3(-12, 0, -24), "#7A6A50");

            // shop
            SpawnShop("general", "Vale Post Traders", new Vector3(-16, 0, -20));

            // NPCs (the Sleeper spawns later via story flag)
            SpawnNpc("maren", new Vector3(10, 0, -20));
            SpawnNpc("pell", new Vector3(-26, 0, -34));
            SpawnNpc("juna", new Vector3(-10, 0, -8));
            SpawnNpc("brann", new Vector3(-23, 0, 1));
            SpawnNpc("odessa", new Vector3(-4, 0, 2));
            SpawnNpc("tobin", new Vector3(30, 0, -30));
            SpawnNpc("sela", new Vector3(58, 0, 30));
            SpawnNpc("korrin", new Vector3(70, 0, 10));
            SpawnNpc("vesna", new Vector3(-12, 0, -58));
            SpawnNpc("hollis", new Vector3(6, 0, -60));
            SpawnNpc("zephy", new Vector3(14, 0, 66));
        }

        public GameObject SpawnNpc(string npcId, Vector3 xz)
        {
            NpcDef def;
            if (!StoryData.Npcs.TryGetValue(npcId, out def)) return null;

            var go = new GameObject("NPC_" + npcId);
            var n = go.AddComponent<NpcInteractable>();
            n.npcId = npcId;
            go.transform.position = new Vector3(xz.x, GroundHeight(xz.x, xz.z), xz.z);

            // Prefer a KayKit model; fall back to a simple capsule body.
            var npcPath = ModelLoader.PathForNpc(npcId);
            var model = ModelLoader.Instantiate(npcPath, go.transform, 1f);
            if (model != null)
            {
                model.transform.localPosition = Vector3.zero;
                var col0 = go.AddComponent<CapsuleCollider>();
                col0.height = 2.4f; col0.radius = 0.8f; col0.center = new Vector3(0, 1.1f, 0);
                AddBlobShadow(go.transform, 0.55f);
                spawned.Add(go);
                return go;
            }

            // body: rounded capsule + tunic belt
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "body";
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = new Vector3(0, 0.85f, 0);
            body.transform.localScale = new Vector3(0.62f, 0.8f, 0.62f);
            body.GetComponent<MeshRenderer>().material = ToonMat(def.colorHex);

            var belt = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            belt.name = "belt";
            belt.transform.SetParent(go.transform, false);
            belt.transform.localPosition = new Vector3(0, 0.78f, 0);
            belt.transform.localScale = new Vector3(0.66f, 0.06f, 0.66f);
            belt.GetComponent<MeshRenderer>().material = ToonMat("#5A4632");
            var beltCol = belt.GetComponent<Collider>(); if (beltCol != null) Destroy(beltCol);

            // head + hair cap + eyes — the face sells the style
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "head";
            head.transform.SetParent(go.transform, false);
            head.transform.localPosition = new Vector3(0, 1.82f, 0);
            head.transform.localScale = new Vector3(0.55f, 0.52f, 0.5f);
            head.GetComponent<MeshRenderer>().material = ToonMat("#F2CFA0");

            var hair = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hair.name = "hair";
            hair.transform.SetParent(go.transform, false);
            hair.transform.localPosition = new Vector3(0, 1.92f, -0.03f);
            hair.transform.localScale = new Vector3(0.58f, 0.42f, 0.54f);
            hair.GetComponent<MeshRenderer>().material = ToonMat("#4A3626");

            AddEyes(go.transform, 1.84f, 0.24f, 0.215f, "#2A3438");

            // blob shadow
            AddBlobShadow(go.transform, 0.55f);

            var col = go.AddComponent<CapsuleCollider>();
            col.height = 2.4f; col.radius = 0.8f; col.center = new Vector3(0, 1.1f, 0);

            spawned.Add(go);
            return go;
        }

        /// <summary>Big stylized eyes: white sclera + dark pupil, on XZ ±offset at given height.</summary>
        public static void AddEyes(Transform parent, float y, float xOff, float zOff, string pupilHex)
        {
            foreach (float sx in new[] { 1f, -1f })
            {
                var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                eye.name = "eye" + (sx > 0 ? "L" : "R");
                eye.transform.SetParent(parent, false);
                eye.transform.localPosition = new Vector3(sx * xOff, y, zOff);
                eye.transform.localScale = new Vector3(0.09f, 0.13f, 0.06f);
                eye.GetComponent<MeshRenderer>().material = ToonMat(pupilHex);
                var ec = eye.GetComponent<Collider>(); if (ec != null) Destroy(ec);
            }
        }

        /// <summary>Soft round blob shadow under characters/creatures (fake contact shadow).</summary>
        public static void AddBlobShadow(Transform parent, float radius)
        {
            var sh = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sh.name = "blobShadow";
            sh.transform.SetParent(parent, false);
            sh.transform.localPosition = new Vector3(0, 0.03f, 0);
            sh.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 1.6f);
            var mr = sh.GetComponent<MeshRenderer>();
            mr.material = ToonMat("#1A2428");
            var col = sh.GetComponent<Collider>(); if (col != null) Destroy(col);
        }

        /// <summary>Scattered decorative rocks.</summary>
        void ScatterRocks(string zone, int count, Vector2 x, Vector2 z)
        {
            for (int i = 0; i < count; i++)
            {
                float px = Random.Range(x.x, x.y);
                float pz = Random.Range(z.x, z.y);
                float gy = GroundHeight(px, pz);
                if (gy < 0.2f) continue;
                var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rock.name = "Rock_" + zone;
                rock.transform.position = new Vector3(px, gy + 0.1f, pz);
                rock.transform.rotation = Quaternion.Euler(Random.Range(0f, 40f), Random.Range(0f, 90f), Random.Range(0f, 40f));
                float rs = Random.Range(0.3f, 0.9f);
                rock.transform.localScale = new Vector3(rs, rs * 0.7f, rs * Random.Range(0.7f, 1.2f));
                rock.GetComponent<MeshRenderer>().material = ToonMat("#8E8A80");
                spawned.Add(rock);
            }
        }

        public GameObject SpawnNode(string nodeId, Vector3 xz)
        {
            if (NodeDB.Get(nodeId) == null) return null;

            var go = new GameObject("Node_" + nodeId + "_" + xz.x + "_" + xz.z);
            var ni = go.AddComponent<GatherNode>();
            ni.nodeId = nodeId;
            go.transform.position = new Vector3(xz.x, GroundHeight(xz.x, xz.z), xz.z);
            ni.BuildVisual(); // node builds its own mesh + collider
            spawned.Add(go);
            return go;
        }

        public GameObject SpawnStation(string stationId, string label, Vector3 xz, string colorHex)
        {
            var go = new GameObject("Station_" + stationId + "_" + xz.x);
            var s = go.AddComponent<CraftingStation>();
            s.stationId = stationId;
            go.transform.position = new Vector3(xz.x, GroundHeight(xz.x, xz.z), xz.z);

            if (stationId == "campfire") BuildCampfire(go.transform);
            else if (stationId == "forge") BuildForge(go.transform);
            else BuildWorkbench(go.transform);

            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(1.6f, 1.4f, 1.6f);
            col.center = new Vector3(0, 0.7f, 0);
            spawned.Add(go);
            return go;
        }

        void BuildCampfire(Transform parent)
        {
            // stone ring
            for (int i = 0; i < 7; i++)
            {
                float a = i / 7f * Mathf.PI * 2f;
                var st = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                st.name = "stone" + i;
                st.transform.SetParent(parent, false);
                st.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.75f, 0.12f, Mathf.Sin(a) * 0.75f);
                st.transform.localScale = new Vector3(0.3f, 0.22f, 0.3f);
                st.GetComponent<MeshRenderer>().material = ToonMat("#8E8A80");
                var c = st.GetComponent<Collider>(); if (c != null) Destroy(c);
            }
            // logs
            for (int i = 0; i < 3; i++)
            {
                float a = i / 3f * Mathf.PI * 2f + 0.4f;
                var log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                log.name = "log" + i;
                log.transform.SetParent(parent, false);
                log.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.18f, 0.16f, Mathf.Sin(a) * 0.18f);
                log.transform.localScale = new Vector3(0.1f, 0.42f, 0.1f);
                log.transform.rotation = Quaternion.Euler(90f, a * Mathf.Rad2Deg, 0f);
                log.GetComponent<MeshRenderer>().material = ToonMat("#7A5230");
                var c = log.GetComponent<Collider>(); if (c != null) Destroy(c);
            }
            // flame: two stacked cones made from scaled cylinders
            var flame = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            flame.name = "flame";
            flame.transform.SetParent(parent, false);
            flame.transform.localPosition = new Vector3(0, 0.45f, 0);
            flame.transform.localScale = new Vector3(0.34f, 0.6f, 0.34f);
            flame.GetComponent<MeshRenderer>().material = ToonMat("#FF9838");
            var fc = flame.GetComponent<Collider>(); if (fc != null) Destroy(fc);
            forgeEmbers.Add(flame.transform);

            var flame2 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            flame2.name = "flame2";
            flame2.transform.SetParent(parent, false);
            flame2.transform.localPosition = new Vector3(0, 0.35f, 0);
            flame2.transform.localScale = new Vector3(0.22f, 0.26f, 0.22f);
            flame2.GetComponent<MeshRenderer>().material = ToonMat("#FFD75E");
            var fc2 = flame2.GetComponent<Collider>(); if (fc2 != null) Destroy(fc2);
            forgeEmbers.Add(flame2.transform);
        }

        void BuildForge(Transform parent)
        {
            // anvil body: stone base + dark iron top
            var baseCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseCube.name = "base";
            baseCube.transform.SetParent(parent, false);
            baseCube.transform.localPosition = new Vector3(0, 0.3f, 0);
            baseCube.transform.localScale = new Vector3(1.3f, 0.6f, 1.3f);
            baseCube.GetComponent<MeshRenderer>().material = ToonMat("#6E6A64");
            var bc = baseCube.GetComponent<Collider>(); if (bc != null) Destroy(bc);

            var top = GameObject.CreatePrimitive(PrimitiveType.Cube);
            top.name = "top";
            top.transform.SetParent(parent, false);
            top.transform.localPosition = new Vector3(0, 0.75f, 0);
            top.transform.localScale = new Vector3(1.6f, 0.3f, 1.1f);
            top.GetComponent<MeshRenderer>().material = ToonMat("#3E4046");
            var tc = top.GetComponent<Collider>(); if (tc != null) Destroy(tc);

            // glowing coals
            var coals = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            coals.name = "coals";
            coals.transform.SetParent(parent, false);
            coals.transform.localPosition = new Vector3(0.5f, 0.95f, 0);
            coals.transform.localScale = new Vector3(0.5f, 0.2f, 0.4f);
            coals.GetComponent<MeshRenderer>().material = ToonMat("#FF6A2A");
            var cc = coals.GetComponent<Collider>(); if (cc != null) Destroy(cc);
            forgeEmbers.Add(coals.transform);
        }

        void BuildWorkbench(Transform parent)
        {
            // table top + 4 legs
            var top = GameObject.CreatePrimitive(PrimitiveType.Cube);
            top.name = "top";
            top.transform.SetParent(parent, false);
            top.transform.localPosition = new Vector3(0, 0.72f, 0);
            top.transform.localScale = new Vector3(1.7f, 0.14f, 1.1f);
            top.GetComponent<MeshRenderer>().material = ToonMat("#A4713D");
            var tc = top.GetComponent<Collider>(); if (tc != null) Destroy(tc);

            foreach (float sx in new[] { -0.72f, 0.72f })
            {
                foreach (float sz in new[] { -0.42f, 0.42f })
                {
                    var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    leg.name = "leg";
                    leg.transform.SetParent(parent, false);
                    leg.transform.localPosition = new Vector3(sx, 0.32f, sz);
                    leg.transform.localScale = new Vector3(0.12f, 0.64f, 0.12f);
                    leg.GetComponent<MeshRenderer>().material = ToonMat("#8A5E33");
                    var lc = leg.GetComponent<Collider>(); if (lc != null) Destroy(lc);
                }
            }
            // little anvil silhouette on top
            var tool = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tool.name = "tool";
            tool.transform.SetParent(parent, false);
            tool.transform.localPosition = new Vector3(0.3f, 0.87f, 0);
            tool.transform.localScale = new Vector3(0.35f, 0.16f, 0.2f);
            tool.GetComponent<MeshRenderer>().material = ToonMat("#5A5C62");
            var tl = tool.GetComponent<Collider>(); if (tl != null) Destroy(tl);
        }

        public GameObject SpawnShop(string shopId, string label, Vector3 xz)
        {
            var go = new GameObject("Shop_" + shopId);
            var s = go.AddComponent<ShopInteractable>();
            s.shopId = shopId;
            go.transform.position = new Vector3(xz.x, GroundHeight(xz.x, xz.z), xz.z);

            // market stall: posts + striped awning + counter
            foreach (float sx in new[] { -0.9f, 0.9f })
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = "post";
                post.transform.SetParent(go.transform, false);
                post.transform.localPosition = new Vector3(sx, 1.0f, -0.6f);
                post.transform.localScale = new Vector3(0.09f, 1.0f, 0.09f);
                post.GetComponent<MeshRenderer>().material = ToonMat("#7A5230");
                var pc2 = post.GetComponent<Collider>(); if (pc2 != null) Destroy(pc2);
            }
            var awning = GameObject.CreatePrimitive(PrimitiveType.Cube);
            awning.name = "awning";
            awning.transform.SetParent(go.transform, false);
            awning.transform.localPosition = new Vector3(0, 2.0f, 0);
            awning.transform.localScale = new Vector3(2.3f, 0.08f, 1.5f);
            awning.transform.rotation = Quaternion.Euler(-12f, 0f, 0f);
            awning.GetComponent<MeshRenderer>().material = ToonMat("#D8604A");
            var ac = awning.GetComponent<Collider>(); if (ac != null) Destroy(ac);
            for (int i = 0; i < 4; i++)
            {
                var stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stripe.name = "stripe" + i;
                stripe.transform.SetParent(awning.transform, false);
                stripe.transform.localPosition = new Vector3(-0.86f + i * 0.58f, 0.01f, 0);
                stripe.transform.localScale = new Vector3(0.29f, 0.02f, 1.5f);
                stripe.GetComponent<MeshRenderer>().material = ToonMat("#F2EBDD");
                var sc = stripe.GetComponent<Collider>(); if (sc != null) Destroy(sc);
            }
            var counter = GameObject.CreatePrimitive(PrimitiveType.Cube);
            counter.name = "counter";
            counter.transform.SetParent(go.transform, false);
            counter.transform.localPosition = new Vector3(0, 0.55f, 0.35f);
            counter.transform.localScale = new Vector3(1.9f, 0.7f, 0.6f);
            counter.GetComponent<MeshRenderer>().material = ToonMat("#A4713D");

            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(2.2f, 2f, 2.2f);
            col.center = new Vector3(0, 1f, 0);
            spawned.Add(go);
            return go;
        }

        // ---------------- helpers ----------------
        void ScatterTrees(string zone, int count, Vector2 x, Vector2 z, string colorHex = "#5F8F4F")
        {
            for (int i = 0; i < count; i++)
            {
                float px = Random.Range(x.x, x.y);
                float pz = Random.Range(z.x, z.y);
                if (GroundHeight(px, pz) < 0.2f) continue; // skip water
                var tree = new GameObject("Tree_" + zone);
                tree.transform.position = new Vector3(px, GroundHeight(px, pz), pz);

                // trunk: tapered two-tier — top tier must reach the crown (y=2.2)
                var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                trunk.name = "trunk";
                trunk.transform.SetParent(tree.transform, false);
                trunk.transform.localPosition = new Vector3(0, 0.7f, 0);
                trunk.transform.localScale = new Vector3(0.46f, 0.7f, 0.46f);
                trunk.GetComponent<MeshRenderer>().material = ToonMat("#7A5230");
                var tc = trunk.GetComponent<Collider>(); if (tc != null) Destroy(tc);
                var trunkTop = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                trunkTop.name = "trunkTop";
                trunkTop.transform.SetParent(tree.transform, false);
                trunkTop.transform.localPosition = new Vector3(0, 1.6f, 0);
                trunkTop.transform.localScale = new Vector3(0.32f, 0.6f, 0.32f);
                trunkTop.GetComponent<MeshRenderer>().material = ToonMat("#7A5230");
                var tc2 = trunkTop.GetComponent<Collider>(); if (tc2 != null) Destroy(tc2);

                // foliage: layered spheres, slightly offset — classic stylized look.
                // pivot at 2.05 keeps the low junction puff overlapping the trunk top.
                var crownPivot = new GameObject("crownPivot").transform;
                crownPivot.SetParent(tree.transform, false);
                crownPivot.localPosition = new Vector3(0, 2.05f, 0);
                string[] tones = { colorHex, Lighten(colorHex, 0.12f), Darken(colorHex, 0.1f), colorHex, Darken(colorHex, 0.18f) };
                Vector3[] offs = {
                    new Vector3(0f, 0.95f, 0f),
                    new Vector3(0.55f, 0.6f, 0.18f),
                    new Vector3(-0.48f, 0.55f, -0.22f),
                    new Vector3(0.12f, 0.62f, -0.5f),
                    new Vector3(0f, 0.28f, 0f)
                };
                float[] rads = { 1.05f, 0.72f, 0.68f, 0.62f, 0.78f };
                for (int k = 0; k < offs.Length; k++)
                {
                    var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    puff.name = "puff" + k;
                    puff.transform.SetParent(crownPivot, false);
                    puff.transform.localPosition = offs[k];
                    float cs = rads[k] * Random.Range(0.9f, 1.15f);
                    puff.transform.localScale = new Vector3(cs, cs * 0.88f, cs);
                    puff.GetComponent<MeshRenderer>().material = ToonMat(tones[k]);
                    var pc2 = puff.GetComponent<Collider>(); if (pc2 != null) Destroy(pc2);
                }
                swayingCrowns.Add(crownPivot);

                var col = tree.AddComponent<CapsuleCollider>();
                col.radius = 0.5f; col.height = 3.5f; col.center = new Vector3(0, 1.75f, 0);
                spawned.Add(tree);
            }
        }

        public static string Lighten(string hex, float amt)
        {
            var c = ColorFromHex(hex);
            return ToHex(Color.Lerp(c, Color.white, amt));
        }

        public static string Darken(string hex, float amt)
        {
            var c = ColorFromHex(hex);
            return ToHex(Color.Lerp(c, Color.black, amt));
        }

        public static string ToHex(Color c)
        {
            return "#" + ColorUtility.ToHtmlStringRGB(c);
        }

        public static Color ColorFromHex(string hex)
        {
            Color c;
            return ColorUtility.TryParseHtmlString(hex, out c) ? c : Color.magenta;
        }

        /// <summary>Shared toon material — delegates to WorldStyle's factory.</summary>
        public static Material ToonMat(string hex)
        {
            return WorldStyle.ToonMat(hex);
        }
    }
}
