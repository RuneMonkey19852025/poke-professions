using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using PokeProfessions.Core;
using PokeProfessions.UI;
using PokeProfessions.World;
using Object = UnityEngine.Object;

namespace PokeProfessions.EditorTools
{
    /// <summary>
    /// One-shot automated playtest. Drop an <c>Assets/Playtest.request</c> marker
    /// (and optionally <c>Assets/Playtest.exit</c>) then launch the editor: this
    /// opens GameScene, enters Play mode, drives the systems the way the player
    /// would (equip every weapon, swing at a wild target until it drops, open the
    /// weapon panel, inspect the nature props) and writes a PASS/FAIL report to
    /// <c>Logs/playtest.txt</c>.
    ///
    /// Compared with launch.sh's AutoPlay, this both presses the buttons and
    /// reads the resulting state, so it catches "it compiles but plays wrong".
    ///
    ///   touch Assets/Playtest.request Assets/Playtest.exit
    ///   ./launch.sh
    /// </summary>
    [InitializeOnLoad]
    public static class EditorPlaytest
    {
        const string MarkerPath = "Assets/Playtest.request";
        const string ExitPath = "Assets/Playtest.exit";
        const string ScenePath = "Assets/Scenes/GameScene.unity";
        const string ReportPath = "Logs/playtest.txt";

        const string StartedKey = "pp_playtest_started";
        const string DoneKey = "pp_playtest_done";

        const float ReadyTimeout = 120f;

        static readonly List<string> report = new List<string>();
        static readonly List<string> runtimeErrors = new List<string>();
        static int failures;

        static readonly Stack<IEnumerator> coroutines = new Stack<IEnumerator>();
        static bool armed;
        static float readySince;

        static EditorPlaytest()
        {
            if (File.Exists(MarkerPath)) Arm();
        }

        /// <summary>Marker imported while the editor is already open.</summary>
        internal static void MarkerImported()
        {
            if (File.Exists(MarkerPath)) Arm();
        }

        static void Arm()
        {
            if (armed) return;
            armed = true;
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (SessionState.GetBool(DoneKey, false)) { Disarm(); return; }

            // ---- phase 1: open the scene and request Play mode ----
            if (!SessionState.GetBool(StartedKey, false))
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;

                Line("== Emberwild Vale playtest ==");
                try
                {
                    if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                    else Line("note: " + ScenePath + " not found, playing whatever is open");
                }
                catch (Exception e) { Line("FAIL scene: " + e.Message); failures++; }

                SessionState.SetBool(StartedKey, true);
                readySince = (float)EditorApplication.timeSinceStartup;
                EditorApplication.isPlaying = true;
                return;
            }

            // ---- phase 2: wait for the world, then drive the tests ----
            if (EditorApplication.isPlaying)
            {
                if (coroutines.Count == 0)
                {
                    if (!WorldReady())
                    {
                        if (EditorApplication.timeSinceStartup - readySince > ReadyTimeout)
                        {
                            Line("FAIL ready: world did not build within " + ReadyTimeout + "s");
                            failures++;
                            Finish();
                        }
                        return;
                    }
                    Application.logMessageReceived += OnLog;
                    coroutines.Push(Run());
                }

                try
                {
                    if (!Advance())
                    {
                        Application.logMessageReceived -= OnLog;
                        Finish();
                    }
                }
                catch (Exception e)
                {
                    Application.logMessageReceived -= OnLog;
                    coroutines.Clear();
                    Line("FAIL exception: " + e);
                    failures++;
                    Finish();
                }
                return;
            }

            // ---- phase 3: play mode is over; write the report ----
            if (SessionState.GetBool(StartedKey, false) && !EditorApplication.isPlayingOrWillChangePlaymode)
                Finish();
        }

        /// <summary>
        /// Drive the coroutine stack by one step, pushing yielded inner enumerators
        /// and popping them when they finish — i.e. real Unity coroutine semantics.
        /// Without the stack, <c>yield return Frames(n)</c> would advance the outer
        /// enumerator past the inner one and the inner body would never finish.
        /// Returns false when every coroutine has completed.
        /// </summary>
        static bool Advance()
        {
            while (coroutines.Count > 0)
            {
                var e = coroutines.Peek();
                if (!e.MoveNext()) { coroutines.Pop(); continue; }
                var inner = e.Current as IEnumerator;
                if (inner != null) { coroutines.Push(inner); continue; }
                return true; // one tick consumed
            }
            return false;
        }

        static bool WorldReady()
        {
            return Game.I != null && Game.I.State != null && Game.I.World != null &&
                   UiRouter.I != null && Object.FindObjectOfType<PlayerController>() != null;
        }

        // ===================================================================
        //  the tests
        // ===================================================================

        static IEnumerator Run()
        {
            var s = Game.I.State;
            var pc = PlayerController.I;

            // Let a couple of real game frames settle so every Start() has run.
            // (Editor update ticks outpace frames, so count frames, not yields.)
            yield return Frames(3);

            Line("-- session: party=" + s.party.Count + " coins=" + s.coins +
                 " save=" + (GameState.SaveExists() ? "yes" : "no"));

            // ---------------- 1. content DBs ----------------
            int weaponCount = 0;
            foreach (var id in ItemDB.PlayerWeapons)
            {
                var def = ItemDB.Get(id);
                bool ok = def != null && def.playerWeaponKind != PlayerWeaponKind.None;
                Check("item " + id + " defined as trainer weapon", ok,
                      def == null ? "no ItemDef" : "kind=" + def.playerWeaponKind);
                if (ok) weaponCount++;
            }
            Check("all " + ItemDB.PlayerWeapons.Length + " trainer weapons defined",
                  weaponCount == ItemDB.PlayerWeapons.Length, weaponCount + " ok");

            Check("thornmare -> Dragon model path",
                  ModelLoader.PathForSpecies("thornmare") == "Quaternius/Monsters/Dragon",
                  ModelLoader.PathForSpecies("thornmare"));

            // ---------------- 1b. the Normal → Legendary gear ladder ----------------
            // Weapons and armour are ladders rather than hand-numbered items, so
            // test the SYSTEM: every kind reaches every tier, damage climbs per
            // kind, armour's soak and health climb per tier, and every rung is
            // craftable at the bench that owns it. A missing rung, or a rung that
            // goes backwards, is what a player would actually break on.
            int rungs = 0;
            var ladderGaps = new List<string>();
            foreach (var kind in PlayerGear.Kinds)
            {
                int prevDmg = 0;
                foreach (var tier in PlayerGear.Tiers)
                {
                    string id = PlayerGear.WeaponFor(kind, tier);
                    if (string.IsNullOrEmpty(id)) { ladderGaps.Add(kind + "/" + ItemRarity.Name(tier) + " unauthored"); continue; }
                    rungs++;
                    int dmg = PlayerCombat.BaseDamage(id);
                    if (dmg <= prevDmg) ladderGaps.Add(kind + " " + ItemRarity.Name(tier) + " dmg " + dmg + " <= " + prevDmg);
                    prevDmg = dmg;
                }
            }
            int wantRungs = PlayerGear.Kinds.Length * PlayerGear.Tiers.Length;
            Check("weapon ladder covers all " + wantRungs + " kind-by-tier rungs (" + rungs + ")",
                  ladderGaps.Count == 0,
                  ladderGaps.Count > 0 ? string.Join("; ", ladderGaps.ToArray()) : "4 kinds x 5 tiers, damage climbing");
            Check("the weapon pool is exactly the ladder (" + ItemDB.PlayerWeapons.Length + ")",
                  ItemDB.PlayerWeapons.Length == wantRungs, "want " + wantRungs);

            int prevSoak = 0, prevHp = 0;
            var armorGaps = new List<string>();
            for (int i = 0; i < PlayerGear.Tiers.Length && i < ItemDB.PlayerArmors.Length; i++)
            {
                string id = ItemDB.PlayerArmors[i];
                var def = ItemDB.Get(id);
                if (def == null) { armorGaps.Add("missing " + id); continue; }
                if (def.rarity != PlayerGear.Tiers[i]) armorGaps.Add(id + " is " + def.rarity + ", rung wants " + PlayerGear.Tiers[i]);
                s.playerArmor = id;
                int tierSoak = PlayerCombat.Mitigation(s), tierHp = PlayerCombat.MaxHP(s);
                if (tierSoak <= prevSoak) armorGaps.Add(id + " soak " + tierSoak + " <= " + prevSoak);
                if (tierHp <= prevHp) armorGaps.Add(id + " hp " + tierHp + " <= " + prevHp);
                Line("     armour " + ItemRarity.Name(def.rarity) + ": " + def.name +
                     "  soak " + tierSoak + "  maxHP " + tierHp + "  (" + def.equipBonus + ")");
                prevSoak = tierSoak; prevHp = tierHp;
            }
            s.playerArmor = "";
            Check("armour ladder climbs in soak and health over " + ItemDB.PlayerArmors.Length + " tiers",
                  armorGaps.Count == 0,
                  armorGaps.Count > 0 ? string.Join("; ", armorGaps.ToArray()) : "one piece per tier");

            var missingRecipes = new List<string>();
            foreach (var id in ItemDB.PlayerWeapons)
            {
                var r = RecipeFor(id);
                if (r == null || r.station != "weaponsmith" || r.inputs.Count == 0 || r.inputs.Count < 2)
                    missingRecipes.Add(id + " (" + (r == null ? "no recipe" : r.station + ", " + r.inputs.Count + " inputs") + ")");
            }
            foreach (var id in ItemDB.PlayerArmors)
            {
                var r = RecipeFor(id);
                if (r == null || r.station != "armorer" || r.inputs.Count < 2)
                    missingRecipes.Add(id + " (" + (r == null ? "no recipe" : r.station + ", " + r.inputs.Count + " inputs") + ")");
            }
            Check("every rung is crafted at its own bench (" + (ItemDB.PlayerWeapons.Length + ItemDB.PlayerArmors.Length) + " recipes)",
                  missingRecipes.Count == 0,
                  missingRecipes.Count > 0 ? missingRecipes.Count + " bad, first: " + missingRecipes[0]
                                           : "weaponsmith for weapons, armorer for armour, none free");

            var levelGaps = new List<string>();
            foreach (var kind in PlayerGear.Kinds)
            {
                int prevLevel = 0;
                foreach (var tier in PlayerGear.Tiers)
                {
                    var r = RecipeFor(PlayerGear.WeaponFor(kind, tier));
                    if (r == null) continue;
                    if (r.requiredLevel != prevLevel + 2)
                        levelGaps.Add(kind + " " + ItemRarity.Name(tier) + " needs Lv" + r.requiredLevel + ", want " + (prevLevel + 2));
                    prevLevel = r.requiredLevel;
                }
            }
            Check("each tier costs two more profession levels", levelGaps.Count == 0,
                  levelGaps.Count > 0 ? string.Join("; ", levelGaps.ToArray()) : "Normal Lv2 → Legendary Lv10");

            // ---------------- 2. nature kit props ----------------
            var objects = Object.FindObjectsOfType<Transform>(true);
            int undergrowth = 0, kitProps = 0, kitTrees = 0, textured = 0, kitShaderProps = 0;
            string sampleShader = "none";
            foreach (var t in objects)
            {
                if (t == null) continue;
                string n = t.name;
                if (n.StartsWith("Undergrowth_")) undergrowth++;
                else if (n.StartsWith("Nature_"))
                {
                    kitProps++;
                    var mr = t.GetComponent<MeshRenderer>();
                    if (mr != null && mr.sharedMaterial != null && mr.sharedMaterial.shader != null)
                    {
                        if (mr.sharedMaterial.shader.name.Contains("KitProp")) kitShaderProps++;
                        if (kitShaderProps == 1) sampleShader = mr.sharedMaterial.shader.name +
                            (mr.sharedMaterial.mainTexture != null ? " +tex" : " (no texture)");
                        if (mr.sharedMaterial.mainTexture != null) textured++;
                    }
                }
                else if (n.StartsWith("Tree_"))
                {
                    foreach (Transform child in t)
                        if (child != null && child.name.StartsWith("Nature_")) { kitTrees++; break; }
                }
            }

            Check("nature kit available", NatureKit.Available);
            Check("KitProp shader loads", Resources.Load<Shader>("Shaders/KitProp") != null);
            Check("undergrowth spawned (" + undergrowth + ")", undergrowth > 20, "want >20");
            Check("kit props in scene (" + kitProps + ")", kitProps > 20, "want >20");
            Check("kit trees replacing procedural trunks (" + kitTrees + ")", kitTrees > 0);
            Check("kit props use the KitProp shader (" + kitShaderProps + "/" + kitProps + ")",
                  kitShaderProps > 0, "sample: " + sampleShader);
            Check("kit props carry a diffuse texture (" + textured + ")", textured > 0);

            // ---------------- 2b. start-area gathering nodes ----------------
            // Emberpost carries its own Lv2 copper/iron outcrops so a fresh
            // Miner works the town instead of walking out to the ridge. Verify
            // they exist, yield real ore, sit inside the meadow start zone and
            // are clear of the houses/NPCs/targets that also occupy the town —
            // a node buried in a wall is unreachable in practice.
            var live = new List<GatherNode>();
            foreach (var g in Object.FindObjectsOfType<GatherNode>()) if (g != null) live.Add(g);
            var liveXZ = new List<Vector2>();
            foreach (var g in live) liveXZ.Add(new Vector2(g.transform.position.x, g.transform.position.z));

            var occupied = new List<Transform>();
            foreach (var t in objects)
            {
                if (t == null) continue;
                string n = t.name;
                if (n.StartsWith("House_") || n.StartsWith("Station_") || n.StartsWith("Shop_") ||
                    n.StartsWith("NPC_") || n.StartsWith("WildTarget_") || n.StartsWith("Lantern_") ||
                    n == "BellTower" || n == "TownWell" || n == "SlotsMachine")
                    occupied.Add(t);
            }

            // meadow zone volume, as built by WorldBuilder.BuildZoneVolumes
            const float ZoneHalfX = 36f, ZoneZMin = -48f, ZoneZMax = 24f;
            var spawnFlat = new Vector2(s.playerX, s.playerZ);
            int pebbleM = 0, stoneM = 0, ironM = 0, copperM = 0, offZone = 0;
            float minClearance = float.MaxValue, minNodeGap = float.MaxValue, maxFromSpawn = 0f;

            for (int i = 0; i < live.Count; i++)
            {
                var g = live[i];
                Vector3 p = g.transform.position;
                bool inMeadow = Mathf.Abs(p.x) <= ZoneHalfX && p.z >= ZoneZMin && p.z <= ZoneZMax;
                if (inMeadow)
                {
                    if (g.nodeId == "rock_pebble") pebbleM++;
                    else if (g.nodeId == "rock_stone") stoneM++;
                    else if (g.nodeId == "rock_iron_shallow") ironM++;
                    else if (g.nodeId == "rock_copper_shallow") copperM++;
                }

                bool outcrop = g.nodeId == "rock_iron_shallow" || g.nodeId == "rock_copper_shallow";
                if (!outcrop) continue;
                if (!inMeadow) { offZone++; continue; }

                foreach (var o in occupied)
                {
                    if (o == null) continue;
                    var q = new Vector2(o.position.x, o.position.z);
                    minClearance = Mathf.Min(minClearance, Vector2.Distance(liveXZ[i], q));
                }
                for (int j = 0; j < liveXZ.Count; j++)
                    if (j != i) minNodeGap = Mathf.Min(minNodeGap, Vector2.Distance(liveXZ[i], liveXZ[j]));
                maxFromSpawn = Mathf.Max(maxFromSpawn, Vector2.Distance(liveXZ[i], spawnFlat));

                Line("     node " + g.nodeId + " at (" + p.x.ToString("0.0") + "," +
                     p.y.ToString("0.00") + "," + p.z.ToString("0.0") + ")");
            }

            var ironDef = NodeDB.Get("rock_iron_shallow");
            var copperDef = NodeDB.Get("rock_copper_shallow");
            Check("iron outcrop def is a Miner Lv2 node",
                  ironDef != null && ironDef.profession == ProfessionId.Miner && ironDef.requiredLevel == 2,
                  ironDef == null ? "missing" : ironDef.name + " " + ironDef.profession + " Lv" + ironDef.requiredLevel);
            Check("copper outcrop def is a Miner Lv2 node",
                  copperDef != null && copperDef.profession == ProfessionId.Miner && copperDef.requiredLevel == 2,
                  copperDef == null ? "missing" : copperDef.name + " " + copperDef.profession + " Lv" + copperDef.requiredLevel);
            Check("outcrops yield real ore and no sigil fragments",
                  ironDef != null && ironDef.itemPool.Contains("iron_ore") && !ironDef.itemPool.Contains("sigil_piece_c") &&
                  copperDef != null && copperDef.itemPool.Contains("copper_ore") && !copperDef.itemPool.Contains("sigil_piece_c"),
                  "the Ember fragment stays a ridge-vein drop");
            Check("start-area pebble scatter (" + pebbleM + ")", pebbleM >= 4, "want >=4");
            Check("start-area loose stone (" + stoneM + ")", stoneM >= 3, "want >=3");
            Check("start-area iron outcrops (" + ironM + ")", ironM >= 4, "want >=4");
            Check("start-area copper outcrops (" + copperM + ")", copperM >= 3, "want >=3");
            Check("every outcrop sits in the meadow start zone", offZone == 0, offZone + " outside the zone");
            Check("outcrops clear of town props/NPCs (" + minClearance.ToString("0.0") + "u)",
                  minClearance >= 3.5f, "want >=3.5u to the nearest house/lantern/NPC/target");
            Check("no gathering node stacked on another (" + minNodeGap.ToString("0.0") + "u)",
                  minNodeGap >= 2.5f, "want >=2.5u between nodes");
            Check("outcrops are a short walk from spawn (max " + maxFromSpawn.ToString("0.0") + "u)",
                  maxFromSpawn < 45f, "want <45u");

            // The two character-gear benches have to actually be standing in
            // town: the ladder recipes above are unreachable otherwise.
            var stationIds = new List<string>();
            foreach (var st in Object.FindObjectsOfType<CraftingStation>())
                if (st != null && !string.IsNullOrEmpty(st.stationId)) stationIds.Add(st.stationId);
            Check("Weaponsmith's Bench stands in Emberpost", stationIds.Contains("weaponsmith"),
                  string.Join(", ", stationIds.ToArray()));
            Check("Armourer's Stand stands in Emberpost", stationIds.Contains("armorer"),
                  string.Join(", ", stationIds.ToArray()));

            // The generalist gear bench: one table that works BOTH gear ladders, so a
            // trainer who just wants gear has one obvious place to craft it rather
            // than having to learn which of the two specialist benches is which.
            Check("Gearwright's Table stands in Emberpost", stationIds.Contains("gear_table"),
                  string.Join(", ", stationIds.ToArray()));
            Check("the Gearwright's Table works both gear ladders",
                  ProfessionSystem.StationAccepts("weaponsmith", "gear_table") &&
                  ProfessionSystem.StationAccepts("armorer", "gear_table") &&
                  !ProfessionSystem.StationAccepts("forge", "gear_table") &&
                  ProfessionSystem.StationAccepts("weaponsmith", "weaponsmith"),
                  "weapon=" + ProfessionSystem.StationAccepts("weaponsmith", "gear_table") +
                  " armour=" + ProfessionSystem.StationAccepts("armorer", "gear_table") +
                  " forge=" + ProfessionSystem.StationAccepts("forge", "gear_table"));

            // The trader: a person in the start town you can buy the trainer's own
            // weapons and armour from. Without him both ladders are craft-only,
            // which is a long walk for a fresh trainer with no professions yet.
            ShopDef armoury = null;
            if (StoryData.Shops != null) StoryData.Shops.TryGetValue("armory", out armoury);
            int gearStock = 0;
            if (armoury != null)
                foreach (var entry in StoryData.StockFor(armoury, s))
                {
                    string id = entry.Split(':')[0];
                    var d = ItemDB.Get(id);
                    if (d == null) continue;
                    if (d.playerWeaponKind != PlayerWeaponKind.None || id.StartsWith("arm_")) gearStock++;
                }
            Check("Hask's Armoury stocks player weapons and armour (" + gearStock + " pieces)",
                  armoury != null && gearStock >= 9,
                  armoury == null ? "no 'armory' shop" : armoury.name + ", " + gearStock + " gear pieces");

            // The rack grows with the story: a quartermaster only deals a rung he can
            // get hold of. Snapshot the flags first so the rest of the run sees the
            // save it expected.
            if (armoury != null)
            {
                var flagSnapshot = new List<string>(s.flags);
                int ArmouryGear()
                {
                    int n = 0;
                    foreach (var entry in StoryData.StockFor(armoury, s))
                    {
                        string id = entry.Split(':')[0];
                        var d = ItemDB.Get(id);
                        if (d != null && (d.playerWeaponKind != PlayerWeaponKind.None || id.StartsWith("arm_"))) n++;
                    }
                    return n;
                }
                s.flags.Remove("ridge_opened"); s.flags.Remove("has_sigil"); s.flags.Remove("sleeper_awake");
                int hintsFresh = StoryData.StockHints(armoury, s).Count;
                int stockAct1 = ArmouryGear();   // two rungs
                s.SetFlag("ridge_opened");
                int stockAct2 = ArmouryGear();   // + Rare
                s.SetFlag("has_sigil");
                int stockAct3 = ArmouryGear();   // + Epic
                s.SetFlag("sleeper_awake");
                int stockEnd = ArmouryGear();    // + Legendary
                int hintsEnd = StoryData.StockHints(armoury, s).Count;
                s.flags.Clear(); s.flags.AddRange(flagSnapshot);
                Check("the armoury stock grows with the story (" + stockAct1 + " -> " + stockAct2 +
                      " -> " + stockAct3 + " -> " + stockEnd + " pieces)",
                      stockAct1 == 10 && stockAct2 == 15 && stockAct3 == 20 && stockEnd == 25,
                      "want 10 (2 tiers) -> 15 (Rare) -> 20 (Epic) -> 25 (Legendary)");

                // A rack that quietly stops at Uncommon reads as a short ladder, so
                // the shop names the locked tiers and what opens each — and stops
                // once it deals the whole thing.
                Check("the armoury names its locked tiers (" + hintsFresh + " -> " + hintsEnd +
                      " hints)", hintsFresh == 3 && hintsEnd == 0,
                      "want 3 locked hints at the start, 0 once the ladder is open");
            }

            bool haskNpc = StoryData.Npcs != null && StoryData.Npcs.ContainsKey("hask");
            bool haskShopLine = false;
            if (haskNpc)
            {
                var def = StoryData.Npcs["hask"];
                if (def.dialogueNodes != null)
                    foreach (var nodeId in def.dialogueNodes)
                    {
                        var node = StoryData.GetNode(nodeId);
                        if (node == null) continue;
                        foreach (var line in node.lines)
                            if (line != null && line.openShop == "armory") haskShopLine = true;
                    }
            }
            int haskInWorld = 0;
            foreach (var n in Object.FindObjectsOfType<NpcInteractable>())
                if (n != null && n.npcId == "hask") haskInWorld++;
            Check("Hask the trader stands in the start town and opens his shop",
                  haskNpc && haskShopLine && haskInWorld > 0,
                  "npc=" + haskNpc + " shopLine=" + haskShopLine + " inWorld=" + haskInWorld);

            // ---------------- 3. creature model scaling + ground alignment ----------------
            // The size/ground pass runs on the model's first LateUpdate, so build
            // the ruler creature, let frames pass, then measure.
            BeginMeasure("thornmare");
            yield return Frames(10); // > ModelFooting.FirstSample: the ground pass has run
            float thornH = FinishMeasure();
            string thornDiag = measureDiag;
            float thornMinY = measureMinY;
            float thornBoundsRatio = measureBoundsRatio;

            BeginMeasure("sprigling");
            yield return Frames(10); // > ModelFooting.FirstSample: the ground pass has run
            float sprigH = FinishMeasure();
            string sprigDiag = measureDiag;

            Check("thornmare renders at creature scale (" + thornH.ToString("0.00") + "u)",
                  thornH > 0.6f && thornH < 2.6f, thornDiag + "  want 0.6-2.6");
            Check("sprigling renders at creature scale (" + sprigH.ToString("0.00") + "u)",
                  sprigH > 0.6f && sprigH < 2.6f, sprigDiag + "  want 0.6-2.6");
            Check("culling bounds track the animated size (" + thornBoundsRatio.ToString("0.00") + "x target)",
                  thornBoundsRatio > 0.5f && thornBoundsRatio < 2f,
                  "Renderer.bounds/target for thornmare, want 0.5-2x (the bind pose reads ~40x)");
            Check("pack creatures ground-align to their feet (minY " + thornMinY.ToString("0.00") + ")",
                  thornMinY > -0.2f && thornMinY < 0.4f, thornDiag);

            // ---------------- 3b. animation clips on every instantiated model ----------------
            ReportScaleInternals("Quaternius/Animals/Deer");
            ReportScaleInternals("Quaternius/Monsters/Dragon");

            // ---------------- 3d. what unit scale did the import actually bake? ----------------
            // Raw (t=0, before any skin update), settled (t=3/t=15) and the
            // GPU-skinning ground truth for one model per import mode. Generic
            // (KayKit, unchanged import) is the control: it renders at human
            // height in game, so whichever metric agrees with ~1.7u there is the
            // metric that reflects the pixels.
            yield return DiagModel("KayKitAdventurers/Characters/fbx/Ranger");
            yield return DiagModel("Quaternius/Animals/Deer");
            yield return DiagModel("Quaternius/Monsters/Dragon");
            yield return DiagModel("CuteMonsters/Alien");

            var animOffenders = new List<string>();
            int probed = 0;
            foreach (var kv in CreatureDB.All)
                ProbeAnim(kv.Key, ModelLoader.PathForSpecies(kv.Key), animOffenders, ref probed);

            var npcField = typeof(ModelLoader).GetField("NpcModel",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (npcField != null)
            {
                var npcModels = npcField.GetValue(null) as Dictionary<string, string>;
                if (npcModels != null)
                {
                    // The KayKit characters ship with no clips of their own, so
                    // this is the check that the borrowed library reached them.
                    var npcMissing = new List<string>();
                    foreach (var kv in npcModels)
                    {
                        if (kv.Value == null) continue;
                        if (ProbeAnim(kv.Key, kv.Value, animOffenders, ref probed)) continue;
                        npcMissing.Add(kv.Key + " -> " + kv.Value);
                    }
                    Check("all " + npcModels.Count + " NPC models resolve a legacy idle clip",
                          npcMissing.Count == 0 || !HasClipLibrary(),
                          npcMissing.Count == 0
                              ? "clips borrowed from Models/KayKitAnimations"
                              : !HasClipLibrary()
                                  ? "no clip library imported — see Assets/Resources/Models/README.md"
                                  : npcMissing.Count + " missing, first: " + npcMissing[0]);
                }
            }

            // Informational. The CC0 creature packs are imported Legacy
            // (animationType: 1), which is what makes their embedded clips
            // legacy-playable, so nearly every creature reports an idle state.
            // The KayKit human/skeleton FBX were left alone: their animation
            // library is a separate download that is not in the project, so they
            // have no clips to expose at all. Reported rather than asserted — the
            // contract ModelLoader actually owns is "degrade silently" (the
            // runtime-error check below covers that).
            Line("animation: " + (probed - animOffenders.Count) + "/" + probed +
                 " models expose a legacy-playable idle state" +
                 (animOffenders.Count > 0
                     ? "; first: " + animOffenders[0]
                     : ""));

            // Which states each mapped creature pack actually carries. The overworld
            // wiring (roam -> walk, lunge -> attack, blow -> hit, KO -> death) has
            // footage only where the clip exists, and the packs differ; this is the
            // line that says how much of it is real. Informational by design — the
            // contract the runtime owns is "degrade to the pose you had".
            var stateNames = new[] { "idle", "walk", "run", "attack", "hit", "death", "spawn" };
            var stateCounts = new Dictionary<string, int>();
            int mappedSpecies = 0;
            foreach (var kv in CreatureDB.All)
            {
                var speciesPath = ModelLoader.PathForSpecies(kv.Key);
                if (speciesPath == null) continue;
                mappedSpecies++;
                foreach (var st in stateNames)
                {
                    if (ModelLoader.GetClip(speciesPath, st) == null) continue;
                    int c;
                    stateCounts[st] = stateCounts.TryGetValue(st, out c) ? c + 1 : 1;
                }
            }
            var coverage = new List<string>();
            foreach (var st in stateNames)
            {
                int c;
                coverage.Add(st + "=" + (stateCounts.TryGetValue(st, out c) ? c : 0));
            }
            Line("creature state coverage over " + mappedSpecies + " mapped species: " +
                 string.Join(" ", coverage.ToArray()));

            // The species the packs left without an arrival clip. These are exactly
            // the ones the code-driven send-out exists for (CreatureVisual.EnterField),
            // so every mapped creature can enter the field rather than just appear.
            var noArrival = new List<string>();
            foreach (var kv in CreatureDB.All)
            {
                var p = ModelLoader.PathForSpecies(kv.Key);
                if (p != null && ModelLoader.GetClip(p, "spawn") == null) noArrival.Add(kv.Key);
            }
            Line("creatures that take the field on the synthetic send-out (" + noArrival.Count + "): " +
                 (noArrival.Count > 0 ? string.Join(" ", noArrival.ToArray()) : "none"));

            // CreatureVisual.PlayState is asked for the same pose every frame by the
            // roaming wilds, and Animation.Play restarts a clip from zero — so a
            // repeat ask must be a no-op or a walking creature stands frozen on
            // frame one. Tested on a visual we own, so nothing else drives its clock.
            var poseHost = new GameObject("Playtest_Pose");
            var poseVisual = CreatureVisual.Build(CreatureDB.Get("sprigling"), false, poseHost.transform);
            var poseAnim = poseHost.GetComponentInChildren<Animation>();
            if (poseAnim != null && poseAnim["idle"] != null)
            {
                poseVisual.PlayState("idle");
                yield return Frames(3);
                float idleT0 = poseAnim["idle"].time;
                poseVisual.PlayState("idle");   // the ask MeleeTarget makes every frame
                yield return Frames(3);
                Check("re-asking for the pose already playing does not restart it",
                      poseAnim["idle"].time > idleT0 + 0.005f,
                      "idle time " + idleT0.ToString("0.000") + " -> " +
                      poseAnim["idle"].time.ToString("0.000") +
                      " (a per-frame Play would pin it near zero)" + AnimDiag(poseAnim));
            }
            else
            {
                Line("pose guard: sprigling exposes no legacy idle clip, check skipped");
            }
            Object.DestroyImmediate(poseHost);

            // ---------------- 3c. animations actually move the skeleton ----------------
            yield return AnimPlays("sprigling");
            yield return AnimPlays("thornpaw");
            // One rig per KayKit family: the human characters and the skeletons.
            // Only meaningful once the shared clip library is imported — until
            // then the NPCs have nothing to play, so this reports instead of
            // failing (see HasClipLibrary).
            if (HasClipLibrary())
            {
                // One rig per KayKit family. Both are Rig_Medium now, which is what
                // the imported library is authored on: the adventurers take the
                // human sets and the skeletons the Skeletons_* ones.
                yield return NpcAnimPlays("maren", "KayKitAdventurers/Characters/fbx/Ranger");
                yield return NpcAnimPlays("korrin", "KayKitSkeletons/Skeleton_Warrior");
            }
            else
            {
                Line("npc animation: Models/KayKitAnimations is empty, so NPC characters " +
                     "stay static — see Assets/Resources/Models/README.md");
            }

            // ---------------- 4. enter the game (click Continue) ----------------
            Line("enter game: " + StartGameFromTitle());

            // ---------------- 4b. player locomotion: idle / walk / run ----------------
            yield return PlayerStates();

            // ---------------- 4c. battle poses: the swing replays, then releases ----------------
            yield return BattlePoses();

            // ---------------- 4c-bis. every creature can take the field ----------------
            yield return BattleArrivals();

            // ---------------- 4d. the turn the battle choreography replays ----------------
            yield return BattleBeats();

            // ---------------- 5. weapons: equip, prop, panel ----------------
            foreach (var id in ItemDB.PlayerWeapons)
            {
                if (ItemDB.Get(id) == null) continue;
                s.AddItem(id, 1);
                s.playerWeapon = id;
                pc.RefreshWeaponProp();

                var prop = pc.transform.Find("weapon_" + id);
                var rot = prop != null ? prop.localRotation : Quaternion.identity;
                yield return Frames(2);
                if (prop == null) prop = pc.transform.Find("weapon_" + id);

                Check("equipped " + id + " builds a visible prop", prop != null);
                if (prop == null) continue;
                Check("prop " + id + " rests in the hand (not reset to identity)",
                      Quaternion.Angle(prop.localRotation, rot) < 0.5f &&
                      Quaternion.Angle(rot, Quaternion.identity) > 1f,
                      "rest=" + rot.eulerAngles.ToString("0") +
                      " now=" + prop.localRotation.eulerAngles.ToString("0"));
            }

            var panel = Object.FindObjectOfType<WeaponPanel>();
            Check("weapon panel exists", panel != null);
            if (panel != null)
            {
                UiRouter.I.ToggleWeapons();
                yield return Frames(1);
                var cg = panel.GetComponent<CanvasGroup>();
                Check("weapon panel opens on Q", cg != null && cg.alpha > 0.99f &&
                      panel.gameObject.activeInHierarchy);
                UiRouter.I.ToggleWeapons();
                yield return Frames(1);
                Check("weapon panel closes again", cg != null && cg.alpha < 0.01f);
            }

            // The Q panel now shows the whole ladder, not just the bag: the rungs you
            // have are the clickable ones, and the ones still ahead stay on screen,
            // greyed, each saying whether to buy or forge it — the same progression
            // the shop tells, kept beside the gear it describes. Checked with the gear
            // temporarily out of the bag so every rung is locked, then put back.
            if (panel != null)
            {
                var invBackup = new List<ItemStack>(s.inventory);
                string wpnBackup = s.playerWeapon, armBackup = s.playerArmor;
                var flagBackup2 = new List<string>(s.flags);
                s.inventory.RemoveAll(st => st != null && IsPlayerGear(st.itemId));
                s.playerWeapon = ""; s.playerArmor = "";
                s.flags.Remove("ridge_opened"); s.flags.Remove("has_sigil"); s.flags.Remove("sleeper_awake");

                UiRouter.I.ToggleWeapons();
                yield return Frames(1);

                var lockedLabels = new List<Text>();
                foreach (var t in panel.GetComponentsInChildren<Text>())
                    if (t != null && t.transform.parent != null &&
                        t.transform.parent.name.StartsWith("locked_")) lockedLabels.Add(t);

                Check("the Q panel shows every rung you have not reached (" + lockedLabels.Count + ")",
                      lockedLabels.Count == 25, "want 25 = 20 weapons + 5 armour");

                string rareHint = "", legendaryHint = "";
                foreach (var t in lockedLabels)
                {
                    if (t.transform.parent.name == "locked_wpn_blade_rare") rareHint = t.text;
                    if (t.transform.parent.name == "locked_wpn_blade_legendary") legendaryHint = t.text;
                }
                Check("a locked rung says what it takes to forge it",
                      rareHint.Contains("Lv6") && rareHint.Contains("Smith"),
                      "rare blade: " + rareHint);
                Check("the top rung names its bench and level",
                      legendaryHint.Contains("Smith") && legendaryHint.Contains("Lv10") &&
                      legendaryHint.Contains("Weaponsmith"),
                      "legendary blade: " + legendaryHint);

                UiRouter.I.ToggleWeapons();
                yield return Frames(1);

                s.inventory.Clear(); s.inventory.AddRange(invBackup);
                s.playerWeapon = wpnBackup; s.playerArmor = armBackup;
                s.flags.Clear(); s.flags.AddRange(flagBackup2);
            }

            // The armoury tells the player its rack grows. A rack that quietly stops
            // at Uncommon reads as a short ladder, so the shop names the locked tiers
            // and what opens each — asserted through the real panel, since its blurb
            // is the only place a player would ever see it. Flags are parked at the
            // start of the story so all three tiers are locked, then restored.
            var shop = Object.FindObjectOfType<ShopPanel>();
            Check("shop panel exists", shop != null);
            if (shop != null)
            {
                var flagBackup = new List<string>(s.flags);
                s.flags.Remove("ridge_opened"); s.flags.Remove("has_sigil"); s.flags.Remove("sleeper_awake");
                UiRouter.I.OpenShop("armory");
                yield return Frames(1);
                Text shopDetail = null;
                foreach (var t in shop.GetComponentsInChildren<Text>())
                    if (t != null && t.name == "detail") { shopDetail = t; break; }
                string hint = shopDetail != null ? shopDetail.text : "";
                bool namesAll = hint.Contains("Rare") && hint.Contains("Epic") &&
                                hint.Contains("Legendary") && hint.Contains("unlocks");
                Check("the armoury panel names the tiers it cannot deal yet", namesAll,
                      hint.Replace("\n", " | "));
                shop.Show(false);
                UiRouter.I.ClosePanel(shop.gameObject);
                s.flags.Clear(); s.flags.AddRange(flagBackup);
                yield return Frames(1);
            }

            // ---------------- 6. melee combat ----------------
            var target = Object.FindObjectOfType<MeleeTarget>();
            Check("wild melee targets roam the world", target != null);
            if (target != null)
            {
                s.playerWeapon = "wpn_club";
                pc.RefreshWeaponProp();
                for (int i = 0; i < 4; i++) { s.playerWeaponLevels[i] = 0; s.playerWeaponXps[i] = 0; }
                s.playerArmorLevel = 0;
                s.playerArmorXp = 0;
                s.playerArmor = "";

                yield return Approach(pc, target);
                float dist = Vector3.Distance(pc.transform.position, target.transform.position);
                Check("player aims at the nearest wild target", pc.AimedTarget == target,
                      "dist " + dist.ToString("0.0") + "u aimed=" +
                      (pc.AimedTarget == null ? "none" : pc.AimedTarget.wild.speciesId) +
                      " want=" + target.wild.speciesId);

                int hp0 = target.wild.curHP;
                int clubIdx = (int)PlayerWeaponKind.Club;
                int xp0 = s.playerWeaponXps[clubIdx] + s.playerWeaponLevels[clubIdx] * 1000;
                int coins0 = s.coins;

                var playerAnim = pc.GetComponentInChildren<Animation>();
                var playerLoco = pc.GetComponent<PlayerLocomotion>();

                Swing(pc);
                yield return Frames(1);
                Check("a swing deals damage (" + hp0 + " -> " + target.wild.curHP + " HP)",
                      target.wild.curHP < hp0);
                Check("a swing grants weapon skill xp",
                      s.playerWeaponXps[clubIdx] + s.playerWeaponLevels[clubIdx] * 1000 > xp0);
                Check("skills are per kind (blade xp untouched by club swings)",
                      s.playerWeaponXps[(int)PlayerWeaponKind.Blade] == 0);

                // The prop swinging while the body stands in its idle pose is the
                // exact look this wiring exists to remove.
                Check("the swing plays the body's attack clip",
                      playerAnim != null && playerAnim.IsPlaying("attack"), NowPlaying(playerAnim));

                // ...and it is time-scaled to the weapon's cooldown, so the arm is
                // never left mid-swing past the point the next swing is allowed.
                float wantRate =
                    playerAnim != null && playerAnim["attack"] != null &&
                    playerAnim["attack"].clip != null && PlayerCombat.CooldownOf(s) > 0.01f
                        ? Mathf.Clamp(playerAnim["attack"].clip.length / PlayerCombat.CooldownOf(s), 0.35f, 3f)
                        : 1f;
                Check("the swing clip is scaled to the weapon's cooldown (" +
                      (playerLoco != null ? playerLoco.OneShotRate : 1f).ToString("0.00") + "x)",
                      playerLoco != null && playerLoco.InOneShot && playerLoco.OneShot == "attack" &&
                      Mathf.Abs(playerLoco.OneShotRate - wantRate) < 0.05f,
                      "want " + wantRate.ToString("0.00") + "x for a " +
                      PlayerCombat.CooldownOf(s).ToString("0.00") + "s cooldown");

                int hits = 1, guard = 0;
                while (target != null && !target.IsKo && guard++ < 200)
                {
                    yield return Approach(pc, target);
                    Swing(pc);
                    hits++;
                    yield return Frames(1);
                }

                bool ko = target != null && target.IsKo;
                Check("repeated swings KO the target (" + hits + " hits)", ko);
                if (ko)
                {
                    int gained = s.coins - coins0;
                    Check("a KO pays coins (" + gained + ", expected " + (5 + target.wild.level) + ")",
                          gained == 5 + target.wild.level);
                    Check("a KO grants skill xp",
                          s.playerWeaponXps[clubIdx] + s.playerWeaponLevels[clubIdx] * 1000 > xp0 + 8);
                    Check("a bare-chested KO grants no armour skill xp",
                          s.playerArmorLevel == 0 && s.playerArmorXp == 0,
                          "armour xp " + s.playerArmorXp);
                    Check("a KO disables further striking",
                          target.GetComponent<Collider>() == null ||
                          !target.GetComponent<Collider>().enabled);
                }

                // The pose has to be released again: a body left mid-swing forever
                // is as broken as one that never swings. A live flinch also counts
                // — something else then owns the pose, which is the machine working.
                yield return Wait(1.4f);
                bool idleAgain = playerAnim != null && playerAnim.IsPlaying("idle");
                bool flinching = playerLoco != null && playerLoco.InOneShot;
                Check("the swing pose releases back to locomotion",
                      idleAgain || flinching, NowPlaying(playerAnim));
            }

            // ---------------- 6b. the trainer's own health and armour ----------------
            // Creatures hit back now, so armour has to demonstrably matter: the
            // same blow lands softer and the ceiling sits higher the further up the
            // ladder you are. Read through the real hit path (PlayerController.
            // TakeHit) rather than a formula restated here.
            s.playerWeapon = "";
            s.playerArmor = "";
            s.playerHP = PlayerCombat.MaxHP(s);
            pc.RefreshGearProps();
            Check("a bare trainer has the base health pool (" + PlayerCombat.MaxHP(s) + ")",
                  PlayerCombat.MaxHP(s) == PlayerCombat.BaseHP && PlayerCombat.Mitigation(s) == 0,
                  "base " + PlayerCombat.BaseHP + ", no armour worn");

            // 30 is a plausible late-game blow and clears the armour ladder's best
            // soak (16), so the comparison below is not swallowed by the 1-damage
            // floor that keeps mitigation from ever granting immunity.
            int youHp = s.playerHP;
            pc.TakeHit(30);
            int bareLoss = youHp - s.playerHP;
            Check("an unarmoured hit lands for full (" + youHp + " -> " + s.playerHP + ")",
                  bareLoss == 30, "want a loss of 30");

            var flinchAnim = pc.GetComponentInChildren<Animation>();
            Check("a wild hit plays the body's flinch clip",
                  flinchAnim == null || flinchAnim.IsPlaying("hit"), NowPlaying(flinchAnim));

            s.AddItem("arm_aegis", 1);
            s.playerArmor = "arm_aegis";
            s.playerHP = PlayerCombat.MaxHP(s);
            pc.RefreshGearProps();
            yield return Frames(2);
            Check("worn armour builds a visible prop", pc.transform.Find("armour_arm_aegis") != null);

            youHp = s.playerHP;
            int soakPerHit = PlayerCombat.Mitigation(s);
            pc.TakeHit(30);
            int armedLoss = youHp - s.playerHP;
            Check("legendary armour turns the same hit (" + armedLoss + " vs " + bareLoss + " bare, soak " + soakPerHit + ")",
                  armedLoss == 30 - soakPerHit && armedLoss < bareLoss, "want " + (30 - soakPerHit));
            Check("armour raises the health ceiling (" + PlayerCombat.MaxHP(s) + ")",
                  PlayerCombat.MaxHP(s) == PlayerCombat.BaseHP + 52, "base " + PlayerCombat.BaseHP + " + hp+52");

            youHp = s.playerHP;
            pc.TakeHit(1);
            Check("a trivial hit still costs 1 health (" + (youHp - s.playerHP) + ")",
                  youHp - s.playerHP == 1, "mitigation must not make the trainer immune");

            // ---------------- 6b+. trainer skills (PlayerMastery) ----------------
            // The trainer levels skills exactly the way creatures level mastery:
            // per weapon kind from landed hits and KOs, armour skill from KOs
            // landed while wearing armour. Formula checks run through the real
            // functions; the damage check goes through the real hit path.
            s.playerArmorLevel = 0;
            s.playerArmorXp = 0;
            PlayerMastery.AddArmorXpForKO(s);   // armour is worn here (arm_aegis)
            Check("a KO while wearing armour grows the armour skill",
                  s.playerArmorXp == PlayerMastery.ArmorXpPerKO,
                  "armour xp " + s.playerArmorXp + ", want " + PlayerMastery.ArmorXpPerKO);

            string wornArmor = s.playerArmor;
            s.playerArmor = "";
            PlayerMastery.AddArmorXpForKO(s);
            Check("a bare-chested KO teaches the armour skill nothing",
                  s.playerArmorXp == PlayerMastery.ArmorXpPerKO);
            s.playerArmor = wornArmor;

            s.playerWeaponLevels[(int)PlayerWeaponKind.Blade] = 0;
            s.playerWeaponXps[(int)PlayerWeaponKind.Blade] = 0;
            s.playerWeaponXps[(int)PlayerWeaponKind.Club] = 0;
            PlayerMastery.AddWeaponXp(s, PlayerWeaponKind.Blade, 30);
            Check("weapon skill is per kind (blade levels, club untouched)",
                  s.playerWeaponLevels[(int)PlayerWeaponKind.Blade] == 2 &&
                  s.playerWeaponXps[(int)PlayerWeaponKind.Club] == 0,
                  "blade Lv" + s.playerWeaponLevels[(int)PlayerWeaponKind.Blade] +
                  ", club xp " + s.playerWeaponXps[(int)PlayerWeaponKind.Club]);

            s.playerWeaponLevels[(int)PlayerWeaponKind.Club] = 50;
            Check("weapon skill adds +0.6% damage per level",
                  Mathf.Abs(PlayerMastery.AttackMultiplier(s, PlayerWeaponKind.Club) - 1.3f) < 0.001f,
                  "mult " + PlayerMastery.AttackMultiplier(s, PlayerWeaponKind.Club).ToString("0.000"));

            // Through the real hit path: armour skill 20 (−10%) trims the same
            // 30-damage blow below what plain legendary armour let through.
            s.playerArmorLevel = 20;
            youHp = s.playerHP;
            pc.TakeHit(30);
            int skilledLoss = youHp - s.playerHP;
            int wantSkilled = Mathf.RoundToInt((30 - soakPerHit) * 0.9f);
            Check("armour skill turns the same hit further (" + skilledLoss + " vs " + armedLoss + " plain)",
                  skilledLoss == wantSkilled && skilledLoss < armedLoss, "want " + wantSkilled);
            s.playerArmorLevel = 0;

            // Old saves carried one global weapon skill; it folds into the kind
            // they were wearing, doubled (old cap +50% → new cap +60%).
            var mig = new GameState();
            mig.playerWeapon = "wpn_blade";
            mig.playerWeaponLevel = 37;
            GameState.MigrateLegacyWeaponSkill(mig);
            Check("old saves fold their weapon skill into the kind worn (×2, cap 100)",
                  mig.playerWeaponLevels[(int)PlayerWeaponKind.Blade] == 74 && mig.playerWeaponLevel == 0,
                  "blade Lv" + mig.playerWeaponLevels[(int)PlayerWeaponKind.Blade]);

            // ---------------- 6c. wild creatures fight back ----------------
            s.playerArmor = "";
            s.playerHP = PlayerCombat.MaxHP(s);
            // FindLiveTarget, not FindObjectOfType: the melee test above may have
            // just KO'd a creature that is still mid-destroy for a frame or two,
            // and holding a reference to that one throws when it goes.
            var foe = FindLiveTarget();
            Check("someone is out there to fight", foe != null);
            if (foe != null)
            {
                yield return Approach(pc, foe);
                int hpAtStart = s.playerHP;
                float waitStart = Time.time;
                while (s.playerHP >= hpAtStart && Time.time < waitStart + 9f)
                {
                    if (foe == null || foe.IsKo)
                    {
                        foe = FindLiveTarget();
                        if (foe == null) break;
                    }
                    yield return Approach(pc, foe);  // stay inside its reach while it winds up
                    yield return Frames(2);
                }
                Check("a wild creature retaliates (" + (hpAtStart - s.playerHP) + " damage)",
                      s.playerHP < hpAtStart,
                      "never got hit in " + (Time.time - waitStart).ToString("0.0") +
                      "s — aggro, reach or the hit path is broken");
                Check("the creature that hit the trainer is aggro", foe.IsAggro);
            }

            // ---------------- 6d. going down returns you to Emberpost ----------------
            s.playerArmor = "";
            s.playerHP = 3;
            pc.transform.position = new Vector3(40f, 0f, 40f);   // away from home
            pc.TakeHit(50);
            yield return Frames(2);
            // distance to the plaza itself, not to the world origin — the plaza
            // sits at (0,-16), so measuring from (0,0) would read a flat 16u
            // whether or not the trainer ever moved.
            float fromPlaza = Vector2.Distance(
                new Vector2(pc.transform.position.x, pc.transform.position.z),
                new Vector2(0f, -16f));
            Check("a knockout wakes the trainer in Emberpost (" + fromPlaza.ToString("0.0") + "u from the plaza)",
                  fromPlaza < 2f, "woke at (" + pc.transform.position.x.ToString("0.0") + "," +
                  pc.transform.position.z.ToString("0.0") + "), want (0,-16)");
            Check("a knockout restores full health (" + PlayerCombat.HealthLabel(s) + ")",
                  s.playerHP == PlayerCombat.MaxHP(s));

            // ---------------- 6d. the stage follows the active creature ----------------
            yield return BattleStageSwap();

            // ---------------- 7. runtime errors ----------------
            yield return Frames(1);
            if (editorChrome > 0)
                Line("note: " + editorChrome + " editor-chrome errors ignored (no real " +
                     "Game View in this session — GUIView RenderTexture at 0x0)");
            Check("no runtime errors or exceptions (" + runtimeErrors.Count + ")",
                  runtimeErrors.Count == 0,
                  runtimeErrors.Count > 0 ? runtimeErrors[0].Replace("\n", " | ") : "");

            yield return null;
        }

        /// <summary>Wait for real game frames (editor update ticks outpace them).</summary>
        static IEnumerator Frames(int n)
        {
            int start = Time.frameCount;
            while (Time.frameCount - start < n) yield return null;
        }

        /// <summary>Wait on the clock rather than a frame count — the locomotion
        /// smoothing is time-based, and play mode keeps running unfocused.</summary>
        static IEnumerator Wait(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until) yield return null;
        }

        /// <summary>
        /// Move the player the way PlayerController does, for a duration, and note the
        /// fastest speed PlayerLocomotion measured while it happened. Sampling inside
        /// the loop keeps the reading clean: the smoothing only needs a fraction of a
        /// second to converge, and the check then compares a speed read from the moving
        /// world with the gear that was asked for.
        /// </summary>
        static IEnumerator Drag(PlayerController pc, float planarSpeed, float seconds)
        {
            var locomotion = pc.GetComponent<PlayerLocomotion>();
            dragMeasured = 0f;
            float until = Time.time + seconds;
            while (Time.time < until)
            {
                pc.transform.position += Vector3.forward * planarSpeed * Time.deltaTime;
                // Wait on a GAME frame, not the editor tick that drives this
                // coroutine. Both the injected step and Time.deltaTime are per
                // game frame, so yielding to the editor instead would inject
                // whatever the last frame's delta was several times per frame —
                // the player would really be moving faster than the gear, and
                // this check would read the harness, not the game.
                yield return Frames(1);
                if (locomotion != null)
                    dragMeasured = Mathf.Max(dragMeasured, locomotion.MeasuredSpeed);
            }
        }

        /// <summary>Fastest speed PlayerLocomotion measured during the last Drag.</summary>
        static float dragMeasured;

        /// <summary>Which state the player's Animation is playing right now.</summary>
        static string NowPlaying(Animation anim)
        {
            if (anim == null) return "no Animation component";
            foreach (AnimationState st in anim)
                if (st.clip != null && anim.IsPlaying(st.clip.name))
                    return "playing " + st.clip.name;
            return "playing nothing";
        }

        /// <summary>Put the player right next to a target so it stays in reach while it wanders.</summary>
        static IEnumerator Approach(PlayerController pc, MeleeTarget t)
        {
            if (t == null) yield break;   // a KO'd creature can go mid-sequence
            var p = t.transform.position;
            var dir = new Vector3(1f, 0f, 0.35f).normalized;
            float x = p.x + dir.x * 1.2f, z = p.z + dir.z * 1.2f;
            pc.transform.position = new Vector3(x, WorldBuilder.GroundHeight(x, z) + 0.6f, z);
            yield return Frames(2); // let PlayerController.Update pick the aim target
        }

        static readonly MethodInfo attack = typeof(PlayerController)
            .GetMethod("TryAttack", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>A wild target that is still standing (KO'd ones linger a frame).</summary>
        static MeleeTarget FindLiveTarget()
        {
            foreach (var t in Object.FindObjectsOfType<MeleeTarget>())
                if (t != null && !t.IsKo) return t;
            return null;
        }

        /// <summary>The recipe that produces an item, or null when nothing does.</summary>
        static Recipe RecipeFor(string resultItemId)
        {
            if (string.IsNullOrEmpty(resultItemId) || ItemDB.Recipes == null) return null;
            foreach (var r in ItemDB.Recipes)
                if (r != null && r.resultItemId == resultItemId) return r;
            return null;
        }

        static void Swing(PlayerController pc)
        {
            if (attack != null) attack.Invoke(pc, null);
        }

        static string measureDiag = "";
        static float measureMinY;
        static float measureBoundsRatio;

        /// <summary>
        /// True rendered height of a rigged model: bake the skinned geometry and
        /// transform it, instead of trusting Renderer.bounds (which for a
        /// SkinnedMeshRenderer is approximated from the root bone and can be off
        /// by orders of magnitude when the skeleton carries an FBX unit scale).
        /// </summary>
        static Bounds? BakedBounds(GameObject root)
        {
            bool any = false;
            var b = new Bounds();
            var mesh = new Mesh();
            try
            {
                foreach (var s in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (s == null || s.sharedMesh == null) continue;
                    s.BakeMesh(mesh);
                    var verts = mesh.vertices;
                    var l2w = s.transform.localToWorldMatrix;
                    for (int i = 0; i < verts.Length; i++)
                    {
                        var w = l2w.MultiplyPoint3x4(verts[i]);
                        if (!any) { b = new Bounds(w, Vector3.zero); any = true; }
                        else b.Encapsulate(w);
                    }
                }
            }
            finally { Object.DestroyImmediate(mesh); }
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r == null) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return any ? b : (Bounds?)null;
        }

        static GameObject rulerHost;
        static GameObject rulerModel;
        static float measureWant;

        /// <summary>
        /// Build an isolated copy of a creature to measure a few frames later, once
        /// the deferred ground pass has run. The measurement is taken on the pack
        /// model itself (not the creature root) so that the blob shadow cannot
        /// stand in for the creature's height.
        /// </summary>
        static void BeginMeasure(string speciesId)
        {
            var def = CreatureDB.Get(speciesId);
            rulerHost = new GameObject("Playtest_Ruler");
            rulerModel = null;
            measureWant = 0f;
            if (def == null) { measureDiag = "no species"; return; }
            CreatureVisual.Build(def, false, rulerHost.transform);
            // the height the creature is supposed to end up at: the scale
            // CreatureVisual passes (Species.scale * 0.62) times ModelLoader's
            // ReferenceModelHeight (1.45).
            measureWant = def.scale * 0.62f * 1.45f;
            foreach (var t in rulerHost.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("Model_") &&
                    t.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
                { rulerModel = t.gameObject; break; }
            }
        }

        /// <summary>
        /// Measure the ruler creature built by BeginMeasure, then destroy it.
        ///
        /// The height returned is the union of the renderers' bounds, which is the
        /// mesh the player sees: ModelLoader sets updateWhenOffscreen on the skinned
        /// renderers, so their bounds follow the animated pose. BakeMesh is printed
        /// alongside it purely as the contrast case — for an off-screen rig it still
        /// reports the bind pose, ~100x the animated size on these Legacy FBX.
        /// </summary>
        static float FinishMeasure()
        {
            var host = rulerHost;
            rulerHost = null;
            if (host == null) return -1f;
            try
            {
                if (host.transform.childCount == 0) return -1f;
                var target = rulerModel != null ? rulerModel : host;
                var rs = target.GetComponentsInChildren<Renderer>(true);
                if (rs.Length == 0) { measureDiag = "no renderers"; return -1f; }
                var b = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++)
                    b.Encapsulate(rs[i].bounds);   // same union ModelFooting grounds on
                int mesh = 0, skinned = 0;
                var detail = new StringBuilder();
                foreach (var r in rs)
                {
                    if (r is SkinnedMeshRenderer s)
                    {
                        skinned++;
                        detail.Append("skin[").Append(r.bounds.size.y.ToString("0.###"));
                        if (!r.enabled) detail.Append(" off");
                        if (s.sharedMesh == null) detail.Append(" nomesh");
                        detail.Append("] ");
                    }
                    else { mesh++; detail.Append("mesh[").Append(r.bounds.size.y.ToString("0.###")).Append("] "); }
                }

                float bakedH = BakedHeight(target);   // bind pose: contrast only
                float h = b.size.y;                   // animated: what is rendered
                float L = target.transform.localScale.x;
                // height the model would have at localScale 1, i.e. the raw FBX
                // height times the Legacy import factor (~100x)
                float unit = L > 0.0000001f ? h / L : -1f;
                measureMinY = b.min.y - host.transform.position.y;
                measureBoundsRatio = measureWant > 0.0001f ? h / measureWant : -1f;
                measureDiag = "rendered=" + h.ToString("0.00") + "u bindpose=" + bakedH.ToString("0.00") +
                              "u L=" + L.ToString("0.#####") + " unit=" + unit.ToString("0.##") +
                              " target=" + measureWant.ToString("0.00") + "u" +
                              " mesh=" + mesh + " skinned=" + skinned +
                              " minY=" + measureMinY.ToString("0.00") + " " + detail;
                return h;
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// Instantiate a model and check the legacy Animation actually got an
        /// "idle" state — the condition that triggers the recurring
        /// "animation state idle could not be played" error at world build.
        /// Returns whether the model can play idle at all.
        /// </summary>
        static bool ProbeAnim(string id, string path, List<string> offenders, ref int probed)
        {
            if (path == null) return false;
            probed++;
            var host = new GameObject("Playtest_AnimProbe");
            try
            {
                var go = ModelLoader.Instantiate(path, host.transform, 1f);
                if (go == null) { offenders.Add(id + " -> " + path + " (model missing)"); return false; }
                var anim = go.GetComponent<Animation>();
                if (anim == null) { offenders.Add(id + " -> " + path + " (no Animation)"); return false; }
                if (anim["idle"] != null) return true;
                int states = 0;
                foreach (AnimationState st in anim) states++;

                var clips = ModelLoader.ClipsFor(path);
                var names = new List<string>();
                string idleClip = "none";
                if (clips != null)
                {
                    foreach (var c in clips)
                    {
                        if (names.Count < 4) names.Add(c.Key + "=" + (c.Value != null ? c.Value.name : "NULL"));
                        if (c.Key == "idle" && c.Value != null) idleClip = c.Value.name;
                    }
                }
                offenders.Add(id + " -> " + path + " states=" + states +
                              " idleClip=" + idleClip + " map: " + string.Join(",", names.ToArray()));
                return false;
            }
            finally { Object.DestroyImmediate(host); }
        }

        /// <summary>
        /// The acceptance test for pack animation: does the model expose a legacy
        /// idle state, and does playing it actually rotate the skeleton over time?
        /// (No try/finally: iterators cannot yield inside one.)
        /// </summary>
        static IEnumerator AnimPlays(string speciesId)
        {
            var def = CreatureDB.Get(speciesId);
            if (def == null) { Check("anim " + speciesId + ": species defined", false); yield break; }

            var host = new GameObject("Playtest_AnimPlay");
            host.transform.position = new Vector3(0f, -60f, 0f); // out of sight
            CreatureVisual.Build(def, false, host.transform);
            yield return Frames(2);

            var anim = host.GetComponentInChildren<Animation>(true);
            Check("anim " + speciesId + ": legacy idle state available",
                  anim != null && anim["idle"] != null,
                  anim == null ? "no Animation component" :
                      (anim["idle"] == null ? "state missing" : "ok"));

            var skinned = host.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var bones = skinned != null ? skinned.bones : null;
            Quaternion[] before = null;
            if (bones != null)
            {
                before = new Quaternion[bones.Length];
                for (int i = 0; i < bones.Length; i++)
                    before[i] = bones[i] != null ? bones[i].localRotation : Quaternion.identity;
            }

            yield return Frames(30);

            float maxDelta = 0f;
            if (bones != null && before != null)
                for (int i = 0; i < bones.Length && i < before.Length; i++)
                    if (bones[i] != null)
                        maxDelta = Mathf.Max(maxDelta, Quaternion.Angle(before[i], bones[i].localRotation));

            // Diagnostic detail: a state that reports "playing" while its clip time
            // never leaves zero is culled or never evaluated, which is a different
            // fault from a clip that is missing or empty — so report the clip's
            // length, the culling mode and what the viewport looked like.
            Check("anim " + speciesId + ": idle animates the skeleton", maxDelta > 0.05f,
                  bones == null || bones.Length == 0
                      ? "no skinned bones"
                      : "max bone delta " + maxDelta.ToString("0.00") + " deg over 30 frames" +
                        AnimDiag(anim));

            Object.DestroyImmediate(host);
        }

        /// <summary>
        /// The facts that separate "the clip is missing/empty" from "the clip is
        /// present and playable but the animation system is not ticking it".
        /// </summary>
        static string AnimDiag(Animation anim)
        {
            if (anim == null) return "";
            var st = anim["idle"];
            if (st == null) return "  [no idle state]";
            float len = st.clip != null ? st.clip.length : -1f;
            return "  [clip len=" + len.ToString("0.000") + "s time=" + st.time.ToString("0.000") +
                   "s speed=" + st.speed.ToString("0.0") + " weight=" + st.weight.ToString("0.0") +
                   " playing=" + anim.IsPlaying("idle") + " culling=" + anim.cullingType +
                   " enabled=" + anim.enabled + " dt=" + Time.deltaTime.ToString("0.0000") +
                   " viewport=" + Screen.width + "x" + Screen.height +
                   " cam=" + (Camera.main != null ? "yes" : "none") + "]";
        }

        /// <summary>
        /// Whether the shared KayKit animation library has been imported. The
        /// characters carry no clips of their own, so their animation is
        /// conditional on this asset pack existing; the checks assert once it
        /// does rather than failing while it is absent.
        /// </summary>
        static bool HasClipLibrary() =>
            Resources.LoadAll<AnimationClip>("Models/KayKitAnimations").Length > 0;

        /// <summary>
        /// The player's idle/walk/run states follow how fast it is really moving,
        /// and the two gears are matched to the clips' stride so the feet do not
        /// slide. PlayerController reads the keyboard, which a playtest cannot
        /// press, so this drives the transform the way the controller does and lets
        /// PlayerLocomotion read the resulting speed.
        /// </summary>
        static IEnumerator PlayerStates()
        {
            var pc = PlayerController.I;
            if (pc == null)
            {
                Check("player controller available", false, "PlayerController.I is null");
                yield break;
            }

            var anim = pc.GetComponentInChildren<Animation>();
            Check("player model exposes idle/walk/run clips",
                  anim != null && anim["idle"] != null && anim["walk"] != null && anim["run"] != null,
                  anim == null
                      ? "no Animation component on the player"
                      : "idle=" + (anim["idle"] != null) + " walk=" + (anim["walk"] != null) +
                        " run=" + (anim["run"] != null));
            // ...and the event poses PlayerLocomotion.PlayOnce drives, which are the
            // difference between a swing the character makes and a swing its weapon
            // makes on its own.
            Check("player model exposes the swing and flinch clips",
                  anim != null && anim["attack"] != null && anim["hit"] != null,
                  anim == null
                      ? "no Animation component on the player"
                      : "attack=" + (anim["attack"] != null) + " hit=" + (anim["hit"] != null));
            if (anim == null) yield break;

            var locomotion = pc.GetComponent<PlayerLocomotion>();
            float gate = locomotion != null ? locomotion.RunGate : 0f;
            Check("locomotion run gate sits between the two gears (" + gate.ToString("0.00") + "u/s)",
                  locomotion != null && gate > pc.walkSpeed && gate < pc.runSpeed,
                  "walk " + pc.walkSpeed.ToString("0.00") + "u/s, run " + pc.runSpeed.ToString("0.00") + "u/s");

            Check("locomotion maps the two gears to idle/walk/run",
                  PlayerLocomotion.StateForSpeed(0f, gate) == "idle" &&
                  PlayerLocomotion.StateForSpeed(pc.walkSpeed, gate) == "walk" &&
                  PlayerLocomotion.StateForSpeed(pc.runSpeed, gate) == "run",
                  "0->" + PlayerLocomotion.StateForSpeed(0f, gate) +
                  " " + pc.walkSpeed.ToString("0.00") + "->" +
                  PlayerLocomotion.StateForSpeed(pc.walkSpeed, gate) +
                  " " + pc.runSpeed.ToString("0.00") + "->" +
                  PlayerLocomotion.StateForSpeed(pc.runSpeed, gate));

            // ---- stride: the speed at which each clip's planted foot stays put ----
            var host = new GameObject("Playtest_Stride");
            var model = ModelLoader.Instantiate("KayKitAdventurers/Characters/fbx/Ranger", host.transform, 1f);
            var strideAnim = model != null ? model.GetComponentInChildren<Animation>() : null;
            float idealWalk = IdealSpeed(strideAnim, "walk");
            float idealRun = IdealSpeed(strideAnim, "run");
            Object.DestroyImmediate(host);

            Check("walk gear matches the walk clip stride (" + pc.walkSpeed.ToString("0.00") + "u/s)",
                  idealWalk > 0.01f && Mathf.Abs(pc.walkSpeed - idealWalk) / idealWalk < 0.15f,
                  "the stride wants " + idealWalk.ToString("0.00") + "u/s, want within 15%");
            Check("run gear matches the run clip stride (" + pc.runSpeed.ToString("0.00") + "u/s)",
                  idealRun > 0.01f && Mathf.Abs(pc.runSpeed - idealRun) / idealRun < 0.15f,
                  "the stride wants " + idealRun.ToString("0.00") + "u/s, want within 15%");

            var home = pc.transform.position;   // put it back before the melee checks

            yield return Wait(0.6f);
            Check("player is idle when standing still", anim.IsPlaying("idle"), NowPlaying(anim));

            yield return Drag(pc, pc.walkSpeed, 0.9f);
            Check("player walks on the walk gear", anim.IsPlaying("walk"), NowPlaying(anim));
            Check("locomotion measures the walk gear it is moved at (" + dragMeasured.ToString("0.00") + "u/s)",
                  Mathf.Abs(dragMeasured - pc.walkSpeed) < pc.walkSpeed * 0.15f,
                  "read " + dragMeasured.ToString("0.00") + "u/s off a " +
                  pc.walkSpeed.ToString("0.00") + "u/s gear; a bias here silently mis-tunes both gears");

            yield return Drag(pc, pc.runSpeed, 0.9f);
            Check("player runs on the run gear", anim.IsPlaying("run"), NowPlaying(anim));
            Check("locomotion measures the run gear it is moved at (" + dragMeasured.ToString("0.00") + "u/s)",
                  Mathf.Abs(dragMeasured - pc.runSpeed) < pc.runSpeed * 0.15f,
                  "read " + dragMeasured.ToString("0.00") + "u/s off a " +
                  pc.runSpeed.ToString("0.00") + "u/s gear");

            yield return Wait(1.3f);
            Check("player returns to idle when it stops", anim.IsPlaying("idle"), NowPlaying(anim));

            pc.transform.position = home;
        }

        static readonly FieldInfo battleEnded = typeof(BattlePanel)
            .GetField("ended", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly MethodInfo battleFinish = typeof(BattlePanel)
            .GetMethod("Finish", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>The species id of the body staged under a battle model, or null.</summary>
        static string StagedSpecies(GameObject model)
        {
            var cv = model != null ? model.GetComponentInChildren<CreatureVisual>() : null;
            return cv != null && cv.Species != null ? cv.Species.id : null;
        }

        /// <summary>
        /// The battle stage follows the active creature. A SWITCH sets the engine's
        /// active index outright and a faint advances it
        /// (BattleEngine.HandleFaint), so without a rebuild the fight keeps showing
        /// the model that left: the incoming creature attacks, flinches and faints as
        /// a body it no longer is, and a trainer's next send-out never appears at all.
        ///
        /// Real battle, real stage — opened through UiRouter and closed at the end, so
        /// none of the later checks run behind an open panel.
        /// </summary>
        static IEnumerator BattleStageSwap()
        {
            var s = Game.I != null ? Game.I.State : null;
            var panel = Object.FindObjectOfType<BattlePanel>();
            if (s == null || panel == null)
            {
                Check("battle stage: a battle can be opened", false, "no state or battle panel");
                yield break;
            }

            // The incoming creature is one whose pack carries a send-out clip, so the
            // arrival pose can be asserted rather than assumed; the one leaving is a
            // different species, so the swap itself is visible in the model.
            var incomingSp = SpeciesWithStates("idle", "spawn");
            string incomingId = incomingSp != null ? incomingSp.id : "thornpaw";
            string leavingId = incomingId == "sprigling" ? "thornpaw" : "sprigling";

            var first = MonsterFactory.Create(leavingId, 10);
            var second = MonsterFactory.Create(incomingId, 8);
            var foe = MonsterFactory.Create("ashkit", 6);
            if (first == null || second == null || foe == null)
            {
                Check("battle stage: throwaway creatures built", false, "MonsterFactory returned null");
                yield break;
            }

            var engine = new BattleEngine(new List<Creature> { first, second },
                new List<Creature> { foe }).WithState(s);
            UiRouter.I.StartBattle(engine);
            yield return Frames(3);

            var playerModel0 = panel.PlayerModel;
            var enemyModel0 = panel.EnemyModel;
            // Read the species now: the body itself is destroyed by the swap below, so
            // asking it what it was after the fact would come back empty.
            string playerSpecies0 = StagedSpecies(playerModel0);
            Check("the stage stands both creatures up",
                  playerSpecies0 == first.speciesId && StagedSpecies(enemyModel0) == foe.speciesId,
                  "player=" + playerSpecies0 + " enemy=" + StagedSpecies(enemyModel0));

            // ---- a switch has to swap the body, not just the stats ----
            engine.ActivePlayerIdx = 1;
            panel.RefreshStage();
            yield return Frames(2);
            Check("a switch swaps the visible model to the incoming creature",
                  StagedSpecies(panel.PlayerModel) == second.speciesId,
                  "showing " + StagedSpecies(panel.PlayerModel) + ", want " + second.speciesId);
            Check("only the side that changed is rebuilt",
                  panel.EnemyModel == enemyModel0 && panel.PlayerModel != playerModel0,
                  "player " + playerSpecies0 + " -> " + StagedSpecies(panel.PlayerModel) +
                  ", enemy model " + (panel.EnemyModel == enemyModel0 ? "kept" : "rebuilt (the arena pops)"));

            // ---- the replacement takes the field rather than blinking into existence ----
            var incomingAnim = panel.PlayerModel != null
                ? panel.PlayerModel.GetComponentInChildren<Animation>() : null;
            if (incomingSp == null)
            {
                Line("send-out: no mapped species carries a spawn clip, pose check skipped");
            }
            else
            {
                Check("a replacement takes the field with the send-out pose",
                      incomingAnim != null && incomingAnim.IsPlaying("spawn"), NowPlaying(incomingAnim));

                // The hold comes from the clip, and these range from 1.3 s to 3.6 s — a
                // fixed hold would leave the creature frozen mid-landing.
                float sendOut = incomingAnim != null && incomingAnim["spawn"] != null &&
                                incomingAnim["spawn"].clip != null
                                    ? incomingAnim["spawn"].clip.length : 0f;
                yield return Wait(sendOut + 0.5f);
                Check("the send-out settles into idle when it is over (" +
                      sendOut.ToString("0.0") + "s)",
                      incomingAnim != null && incomingAnim.IsPlaying("idle"), NowPlaying(incomingAnim));
            }

            // ---- and asking with nothing changed is a no-op ----
            var playerModel1 = panel.PlayerModel;
            panel.RefreshStage();
            yield return Frames(1);
            Check("refreshing with the same creatures is a no-op",
                  panel.PlayerModel == playerModel1,
                  panel.PlayerModel == playerModel1
                      ? "body kept: " + StagedSpecies(playerModel1)
                      : "the model was rebuilt for nothing");

            // ---- close it again so the run does not continue behind a panel ----
            if (battleEnded != null) battleEnded.SetValue(panel, true);
            if (battleFinish != null) battleFinish.Invoke(panel, null);
            yield return Frames(2);
            Check("the battle closes cleanly after the swap test",
                  !UiRouter.I.UiOpen && panel.PlayerModel == null,
                  !UiRouter.I.UiOpen && panel.PlayerModel == null
                      ? "panel closed, stage cleared"
                      : "still open");
        }

        /// <summary>
        /// The turn record the battle choreography plays back: one Beat per executed
        /// action, in order.
        ///
        /// Built straight from two throwaway creatures, with no battle UI and no
        /// touch to the save, because this is the data a choreographed turn depends
        /// on: PlayerTurn resolves both sides in one call and decides who moves
        /// first by speed and priority, so the panel has no way to work the order out
        /// for itself — if the beats are wrong, the enemy's counterattack either
        /// never appears or appears before the swing it answered.
        /// </summary>
        static IEnumerator BattleBeats()
        {
            var ally = MonsterFactory.Create("sprigling", 12);
            var foe = MonsterFactory.Create("thornpaw", 8);
            if (ally == null || foe == null)
            {
                Check("battle beats: throwaway creatures built", false, "MonsterFactory returned null");
                yield break;
            }

            // A move that actually deals damage, so there is damage to attribute.
            string moveId = "tackle";
            foreach (var m in ally.moves)
            {
                var md = MoveDB.Get(m);
                if (md != null && md.power > 0) { moveId = m; break; }
            }

            var engine = new BattleEngine(new List<Creature> { ally }, new List<Creature> { foe });
            ally.curHP = ally.MaxHP;
            foe.curHP = foe.MaxHP;

            int allyBefore = ally.curHP, foeBefore = foe.curHP;
            engine.PlayerTurn(moveId);

            int dealt = 0, taken = 0;
            foreach (var b in engine.Beats)
            {
                if (b.byPlayer) dealt += b.damage; else taken += b.damage;
            }

            Check("the turn records a beat per side that acted (" + engine.Beats.Count + ")",
                  engine.Beats.Count >= 1 && engine.Beats.Count <= 2,
                  "want 1-2, got " + engine.Beats.Count);

            // Attribution: the damage in the beats is the health that actually moved,
            // in both directions. Stated as an equivalence so an enemy heal (which
            // raises HP without any beat damage) does not read as a mismatch.
            bool foeMoved = foe.curHP < foeBefore;
            bool allyMoved = ally.curHP < allyBefore;
            Check("a beat's damage is the health that moved",
                  (dealt > 0) == foeMoved && (taken > 0) == allyMoved,
                  "dealt " + dealt + " (foe " + foeBefore + "->" + foe.curHP + "), took " + taken +
                  " (ally " + allyBefore + "->" + ally.curHP + ")");

            Check("both sides are recorded, so the counterattack can be shown",
                  engine.Beats.Count == 2 && engine.Beats[0].byPlayer != engine.Beats[1].byPlayer,
                  engine.Beats.Count != 2
                      ? "only " + engine.Beats.Count + " beat(s) — one side's turn is invisible"
                      : (engine.Beats[0].byPlayer ? "order player -> enemy" : "order enemy -> player"));

            // The fainting flag is what picks the death pose over the flinch, so it has
            // to survive the engine's own clamp of curHP to 0.
            bool landed = false, flagged = false;
            for (int i = 0; i < 4 && !landed; i++)
            {
                foe.curHP = 1;
                engine.PlayerTurn(moveId);
                foreach (var b in engine.Beats)
                    if (b.byPlayer && b.damage > 0) { landed = true; flagged = b.targetFainted; }
            }
            Check("a fainting blow is flagged in the beat (it picks the death pose)",
                  landed && flagged,
                  landed ? "targetFainted=" + flagged + " on a blow into 1 HP"
                         : "never landed in 4 turns");

            // And the panel turns that flag into a pose. Pure, so it needs no battle.
            Check("a beat picks its reaction pose from damage and fainting",
                  BattlePanel.ReactionFor(0, false) == null &&
                  BattlePanel.ReactionFor(9, false) == "hit" &&
                  BattlePanel.ReactionFor(0, true) == "death" &&
                  BattlePanel.ReactionFor(9, true) == "death",
                  "none=" + (BattlePanel.ReactionFor(0, false) ?? "null") +
                  " hit=" + (BattlePanel.ReactionFor(9, false) ?? "null") +
                  " death=" + (BattlePanel.ReactionFor(0, true) ?? "null"));
        }

        /// <summary>
        /// Every creature has to be able to take the field. A pack with a send-out
        /// clip uses it; a pack without one gets the code-driven arrival, so no
        /// creature simply blinks into existence because its pack shipped no leap.
        ///
        /// The two paths share one contract — EnterField returns the seconds to
        /// hold — so this pins both halves: the clip path plays the clip and holds
        /// for its real length, and the synthetic path moves the body off its spot
        /// (below the ground, stretched), lands it back exactly on that spot at full
        /// size, and hands back a positive hold. The settle back to idle that follows
        /// the hold lives in BattlePanel.PlaySendOut and is pinned in BattleStageSwap.
        /// </summary>
        static IEnumerator BattleArrivals()
        {
            // ---- the packs that carry a send-out clip play it ----
            var withClip = SpeciesWithStates("idle", "spawn");
            if (withClip != null)
            {
                var host = new GameObject("Playtest_Arrival_Clip");
                var cv = CreatureVisual.Build(withClip, false, host.transform);
                var anim = host.GetComponentInChildren<Animation>();
                float clipLen = cv.ClipLength("spawn");
                float hold = cv.EnterField();
                yield return Frames(1);
                Check("a creature whose pack has a send-out plays it (" + withClip.id + ")",
                      anim != null && anim.IsPlaying("spawn"), NowPlaying(anim));
                Check("the clip send-out holds for the clip's own length",
                      Mathf.Abs(hold - clipLen) < 0.01f,
                      "hold " + hold.ToString("0.00") + "s vs clip " + clipLen.ToString("0.00") + "s");
                // The settle back to idle is BattlePanel.PlaySendOut's job (it starts
                // ReturnToIdle for the hold EnterField hands back), so it is pinned
                // there, in BattleStageSwap.
                Object.DestroyImmediate(host);
            }
            else
            {
                Line("send-out clip: no mapped species carries one, clip path skipped");
            }

            // ---- the packs that carry none get the synthetic arrival ----
            var noClip = SpeciesWithStateNot("idle", "spawn");
            if (noClip == null)
            {
                Line("synthetic send-out: every mapped species carries a spawn clip, check skipped");
                yield break;
            }

            var host2 = new GameObject("Playtest_Arrival_Synth");
            var cv2 = CreatureVisual.Build(noClip, false, host2.transform);
            float hold2 = cv2.EnterField();
            Check("a creature with no send-out clip still gets an arrival (" + noClip.id + ")",
                  hold2 > 0.1f, "hold " + hold2.ToString("0.00") + "s");

            // One frame in it is already up out of its spot, stretched — which is
            // what makes it read as an entrance rather than a pop into place.
            yield return Frames(1);
            Vector3 midPos = cv2.transform.localPosition;
            Vector3 midScale = cv2.transform.localScale;
            Check("the synthetic arrival moves the body off its spot",
                  midPos.y < -0.01f || Mathf.Abs(midScale.y - 1f) > 0.01f,
                  "localPos.y " + midPos.y.ToString("0.00") + ", scale.y " + midScale.y.ToString("0.00"));

            yield return Wait(hold2 + 0.3f);
            Check("the synthetic arrival lands back on its spot at full size",
                  cv2.transform.localPosition.magnitude < 0.01f &&
                  (cv2.transform.localScale - Vector3.one).magnitude < 0.01f,
                  "localPos " + cv2.transform.localPosition.ToString("0.00") +
                  ", scale " + cv2.transform.localScale.ToString("0.00"));
            Object.DestroyImmediate(host2);
        }

        /// <summary>
        /// The first species whose pack carries every named pose, or null.
        /// </summary>
        static SpeciesDef SpeciesWithStates(params string[] states)
        {
            foreach (var kv in CreatureDB.All)
            {
                var path = ModelLoader.PathForSpecies(kv.Key);
                if (path == null) continue;
                bool all = true;
                foreach (var st in states)
                    if (ModelLoader.GetClip(path, st) == null) { all = false; break; }
                if (all) return kv.Value;
            }
            return null;
        }

        /// <summary>Is this item a player weapon or a body armour piece (see PlayerGear)?</summary>
        static bool IsPlayerGear(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            foreach (var w in ItemDB.PlayerWeapons) if (w == id) return true;
            foreach (var a in ItemDB.PlayerArmors) if (a == id) return true;
            return false;
        }

        /// <summary>
        /// The first species whose pack has <paramref name="have"/> but not
        /// <paramref name="missing"/> — used to find a creature with an idle pose
        /// but no send-out clip, so the synthetic arrival can be exercised.
        /// </summary>
        static SpeciesDef SpeciesWithStateNot(string have, string missing)
        {
            foreach (var kv in CreatureDB.All)
            {
                var path = ModelLoader.PathForSpecies(kv.Key);
                if (path == null) continue;
                if (ModelLoader.GetClip(path, have) != null &&
                    ModelLoader.GetClip(path, missing) == null) return kv.Value;
            }
            return null;
        }

        /// <summary>
        /// The battle beat: the attacker swings and the target reacts, and both come
        /// back to idle when it is over. Driven directly with visuals of its own, so
        /// the rules hold without playing a battle by hand.
        ///
        /// The bug these pin is a pose that never came back: the swing and the
        /// release to idle both used to be keyed off "did the target lose HP", so a
        /// miss, a KO, or a mastery technique that deals no damage at all (Aegis
        /// Guard is a pure buff) left the attacker clamped mid-swing and the target
        /// frozen in its hit pose for the rest of the fight.
        /// </summary>
        static IEnumerator BattlePoses()
        {
            var panel = Object.FindObjectOfType<BattlePanel>();
            Check("battle panel available for the beat test", panel != null);
            if (panel == null) yield break;

            var sp = SpeciesWithStates("idle", "attack", "hit");
            Check("a creature pack carries the battle pose set (idle+attack+hit)", sp != null,
                  sp == null ? "no mapped species exposes all three" : sp.id);
            if (sp == null) yield break;

            var atkHost = new GameObject("Playtest_Beat_Attacker");
            var defHost = new GameObject("Playtest_Beat_Target");
            var atk = CreatureVisual.Build(sp, false, atkHost.transform);
            var def = CreatureVisual.Build(sp, false, defHost.transform);
            var atkAnim = atkHost.GetComponentInChildren<Animation>();
            var defAnim = defHost.GetComponentInChildren<Animation>();

            // ---- a beat with no reaction still swings... ----
            panel.StrikeBeat(atk, def, null);
            yield return Frames(1);
            Check("a beat with no reaction still plays the swing",
                  atkAnim != null && atkAnim.IsPlaying("attack"), NowPlaying(atkAnim));

            // ---- ...and asking again is a new swing, not a no-op: this is the
            // multi-beat case, where the second swing must rewind the clip instead
            // of holding the first one's last frame ----
            yield return Wait(0.6f);
            float intoFirst = atkAnim["attack"].time;
            panel.StrikeBeat(atk, def, null);
            yield return Frames(1);
            Check("a second swing replays the attack pose instead of holding the last frame",
                  atkAnim["attack"].time < intoFirst - 0.1f,
                  "clip time " + intoFirst.ToString("0.00") + "s -> " +
                  atkAnim["attack"].time.ToString("0.00") + "s" + AnimDiag(atkAnim));

            // ---- and the beat releases on its own, with no reaction to trigger it ----
            yield return Wait(BattlePanel.BeatSeconds + 0.4f);
            Check("a no-damage beat releases the attacker back to idle",
                  atkAnim.IsPlaying("idle"), NowPlaying(atkAnim));

            // ---- a stale idle must not cut a newer swing short ----
            panel.StrikeBeat(atk, def, "hit");
            yield return Frames(1);
            Check("a reacting beat shows the target's hit pose",
                  defAnim != null && defAnim.IsPlaying("hit"), NowPlaying(defAnim));

            yield return Wait(0.9f);            // mid-way through that beat
            panel.StrikeBeat(atk, def, null);   // swing again before the first releases
            yield return Wait(0.5f);            // the first beat's idle lands in here
            Check("a scheduled idle does not cut a newer swing short",
                  atkAnim.IsPlaying("attack"), NowPlaying(atkAnim));

            Object.DestroyImmediate(atkHost);
            Object.DestroyImmediate(defHost);
        }

        /// <summary>
        /// The ground speed a locomotion clip wants, i.e. the speed at which its
        /// feet stop sliding: the average, across the rig's feet, of how fast each
        /// one moves through the part of the clip where it is planted (see
        /// PlantedSpeed). The clips are in place — the body does not travel inside
        /// them — so this is the number a controller gear has to equal. Returns -1
        /// when the clip or the rig is missing.
        /// </summary>
        static float IdealSpeed(Animation anim, string state)
        {
            if (anim == null || anim[state] == null) return -1f;
            var clip = anim[state].clip;
            if (clip == null || clip.length <= 0.001f) return -1f;

            var feet = FeetOf(anim.gameObject);
            if (feet.Count == 0) return -1f;

            const int Samples = 48;
            float dt = clip.length / Samples;
            var track = SampleFeet(anim, state, feet, Samples);

            float total = 0f;
            for (int f = 0; f < feet.Count; f++)
                total += PlantedSpeed(track, f, Samples, dt);
            return total / feet.Count;
        }

        /// <summary>
        /// Foot positions right through one clip, in the model's own frame, so the
        /// body's facing does not matter. The clips are in place — the body does not
        /// travel inside them — which is what makes "how fast does the ground have
        /// to move under this foot" the right question to ask of the track.
        /// </summary>
        static Vector3[,] SampleFeet(Animation anim, string state, List<Transform> feet, int samples)
        {
            var track = new Vector3[samples + 1, feet.Count];
            var st = anim[state];
            float length = st.clip.length;

            anim.Play(state);
            for (int i = 0; i <= samples; i++)
            {
                st.time = length * i / samples;
                st.weight = 1f;
                anim.Sample();
                for (int f = 0; f < feet.Count; f++)
                    track[i, f] = anim.transform.InverseTransformPoint(feet[f].position);
            }
            anim.Stop();
            return track;
        }

        /// <summary>
        /// The ground speed at which one foot stops sliding: while it is planted it
        /// has to travel backwards under the body at exactly the body's forward
        /// speed, so this measures how fast the foot moves through the part of the
        /// clip where it is on the floor (the bottom quarter of its lift).
        ///
        /// The obvious alternative — 2 x stride / clip length, since a cycle is two
        /// steps — is only equivalent when the foot is planted at the extremes of its
        /// swing, which holds for a walk but not for a run, where the flight phase
        /// makes it read ~35% low. Returns that fallback for a clip with no foot lift
        /// (they are then the same thing) or a foot that never plants.
        /// </summary>
        static float PlantedSpeed(Vector3[,] track, int foot, int samples, float dt)
        {
            float minY = float.MaxValue, maxY = float.MinValue;
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            for (int i = 0; i <= samples; i++)
            {
                var p = track[i, foot];
                minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z);
            }

            // whichever axis the rig travels along (these packs are z-forward)
            bool alongX = maxX - minX >= maxZ - minZ;
            float stride = Mathf.Max(maxX - minX, maxZ - minZ);
            float cycle = 2f * stride / (samples * dt);   // two steps per cycle

            float lift = maxY - minY;
            if (lift < 1e-4f) return cycle;               // never leaves the floor

            float floor = minY + lift * 0.25f;            // the planted quarter of the lift
            float travelled = 0f;
            int planted = 0;
            for (int i = 0; i < samples; i++)
            {
                if (track[i, foot].y > floor || track[i + 1, foot].y > floor) continue;
                float a = alongX ? track[i, foot].x : track[i, foot].z;
                float b = alongX ? track[i + 1, foot].x : track[i + 1, foot].z;
                travelled += Mathf.Abs(b - a);
                planted++;
            }
            return planted > 0 ? travelled / (planted * dt) : cycle;
        }

        /// <summary>The rig's feet: bones named for them, else the lowest two.</summary>
        static List<Transform> FeetOf(GameObject model)
        {
            var feet = new List<Transform>();
            var rig = model != null ? model.GetComponentInChildren<SkinnedMeshRenderer>() : null;
            if (rig == null || rig.bones == null) return feet;

            foreach (var b in rig.bones)
                if (b != null && b.name.ToLower().Contains("foot")) feet.Add(b);
            if (feet.Count > 0) return feet;

            var all = new List<Transform>();
            foreach (var b in rig.bones) if (b != null) all.Add(b);
            all.Sort((a, b) => a.position.y.CompareTo(b.position.y));
            for (int i = 0; i < 2 && i < all.Count; i++) feet.Add(all[i]);
            return feet;
        }

        /// <summary>
        /// The same acceptance test as AnimPlays, for an NPC whose clips are
        /// borrowed from the KayKit animation library. It also guards the failure
        /// mode a borrowed clip has that an embedded one cannot: a clip authored in
        /// a different unit space would blow the character up instead of posing it.
        /// </summary>
        static IEnumerator NpcAnimPlays(string npcId, string path)
        {
            var host = new GameObject("Playtest_NpcAnim");
            var go = ModelLoader.Instantiate(path, host.transform, 1f);
            if (go == null)
            {
                Check("npc anim " + npcId + ": model available", false, path + " missing");
                Object.DestroyImmediate(host);
                yield break;
            }
            yield return Frames(2);

            var anim = go.GetComponentInChildren<Animation>(true);
            Check("npc anim " + npcId + ": legacy idle state available",
                  anim != null && anim["idle"] != null,
                  anim == null ? "no Animation component" :
                      (anim["idle"] == null ? "state missing" : "ok"));

            var skinned = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var bones = skinned != null ? skinned.bones : null;
            Quaternion[] before = null;
            if (bones != null)
            {
                before = new Quaternion[bones.Length];
                for (int i = 0; i < bones.Length; i++)
                    before[i] = bones[i] != null ? bones[i].localRotation : Quaternion.identity;
            }
            float size0 = RenderedHeight(go);

            yield return Frames(30);

            float maxDelta = 0f;
            if (bones != null && before != null)
                for (int i = 0; i < bones.Length && i < before.Length; i++)
                    if (bones[i] != null)
                        maxDelta = Mathf.Max(maxDelta, Quaternion.Angle(before[i], bones[i].localRotation));

            Check("npc anim " + npcId + ": idle animates the skeleton", maxDelta > 0.05f,
                  bones == null || bones.Length == 0
                      ? "no skinned bones"
                      : "max bone delta " + maxDelta.ToString("0.00") + " deg over 30 frames");

            float size1 = RenderedHeight(go);
            Check("npc anim " + npcId + ": idle keeps the model human-sized",
                  size1 > 0.8f && size1 < 3f,
                  "rendered " + size0.ToString("0.00") + "u -> " + size1.ToString("0.00") +
                  "u, want 0.8-3u");

            Object.DestroyImmediate(host);
        }

        /// <summary>
        /// Union of the renderer bounds under a model, in world space. ModelLoader
        /// sets updateWhenOffscreen, so this follows the animated pose rather than
        /// the bind pose.
        /// </summary>
        static float RenderedHeight(GameObject model)
        {
            var rs = model.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return -1f;
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b.size.y;
        }

        /// <summary>
        /// Diagnostic: is a model's geometry imported at the wrong unit scale, or is
        /// only the animated pose inflating the bounds? localBounds is bind-pose and
        /// scale-independent, so it isolates the two.
        /// </summary>
        static void ReportScaleInternals(string path)
        {
            var host = new GameObject("Playtest_Scale");
            try
            {
                var go = ModelLoader.Instantiate(path, host.transform, 1f);
                if (go == null) { Line("scale internals " + path + ": model missing"); return; }
                var sb = new StringBuilder();
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is SkinnedMeshRenderer s)
                        sb.Append("skin[localBoundsY=").Append(s.localBounds.size.y.ToString("0.###"))
                          .Append(" worldY=").Append(r.bounds.size.y.ToString("0.###")).Append("] ");
                    else
                        sb.Append("mesh[worldY=").Append(r.bounds.size.y.ToString("0.###")).Append("] ");
                }
                Line("scale internals " + path + " rootScale=" + go.transform.localScale.x.ToString("0.####") +
                     " " + sb);
            }
            finally { Object.DestroyImmediate(host); }
        }

        /// <summary>
        /// Diagnostic: instantiate a model completely un-normalized (bypasses
        /// ModelLoader.Instantiate and its size pass, so localScale stays 1) and
        /// report every candidate height for it.
        ///
        /// The candidates disagree by 100x on the Legacy rigs, which is what pinned
        /// down the creature-size bug:
        ///   worldY - Renderer.bounds, i.e. localBounds * lossyScale
        ///   bake   - SkinnedMeshRenderer.BakeMesh, the bind pose while off-screen
        /// KayKit was the control (Generic import, renders at human height in game)
        /// and agrees only via worldY, so worldY is the metric that matches the
        /// pixels. With updateWhenOffscreen set — as ModelLoader does — localBounds
        /// is recomputed from the skinned pose and worldY becomes the animated
        /// height (see the 'scale internals' lines below).
        /// </summary>
        static IEnumerator DiagModel(string path)
        {
            float tableRaw = -1f;
            var field = typeof(ModelLoader).GetField("ModelRawHeight",
                BindingFlags.NonPublic | BindingFlags.Static);
            var table = field != null ? field.GetValue(null) as Dictionary<string, float> : null;
            if (table != null && table.TryGetValue(path, out var raw)) tableRaw = raw;

            var host = new GameObject("Playtest_Diag");
            host.transform.position = new Vector3(0f, -80f, 0f);
            var src = ModelLoader.Load(path);
            if (src == null) { Line("diag " + path + ": MISSING"); Object.DestroyImmediate(host); yield break; }

            var go = Object.Instantiate(src);
            go.name = "Diag_" + path;
            go.transform.SetParent(host.transform, false);
            go.transform.localScale = Vector3.one;

            Line("diag " + path + " tableRaw=" +
                 (tableRaw > 0f ? tableRaw.ToString("0.###") : "none") +
                 " t0: scales[" + ScaleChain(go) + "] " + DescribeRig(go));
            yield return Frames(3);
            Line("diag " + path + " t3: scales[" + ScaleChain(go) + "] " + DescribeRig(go));
            yield return Frames(15);
            Line("diag " + path + " t18: scales[" + ScaleChain(go) + "] " + DescribeRig(go) +
                 " bake=" + BakedHeight(go).ToString("0.####") +
                 " skin=" + SkinnedHeight(go).ToString("0.####"));
            Object.DestroyImmediate(host);
        }

        /// <summary>
        /// Local scales of the model's first few scaled nodes — the FBX unit
        /// conversion lives on the skeleton, so an idle clip that animates those
        /// nodes' scale shows up here.
        /// </summary>
        static string ScaleChain(GameObject go)
        {
            var sb = new StringBuilder();
            int n = 0;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                var ls = t.localScale;
                if (Mathf.Abs(ls.x - 1f) > 0.001f || Mathf.Abs(ls.y - 1f) > 0.001f ||
                    Mathf.Abs(ls.z - 1f) > 0.001f)
                {
                    sb.Append(t.name).Append('=').Append(ls.y.ToString("0.####")).Append(' ');
                    if (++n >= 4) break;
                }
            }
            return sb.Length == 0 ? "none" : sb.ToString().TrimEnd();
        }

        /// <summary>Renderer/skeleton numbers for a model at localScale 1.</summary>
        static string DescribeRig(GameObject go)
        {
            var sb = new StringBuilder();
            foreach (var s in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = s.sharedMesh;
                if (mesh == null) continue;
                sb.Append(" skin[lossyY=").Append(s.transform.lossyScale.y.ToString("0.###"))
                  .Append(" localBoundsY=").Append(s.localBounds.size.y.ToString("0.###"))
                  .Append(" worldY=").Append(s.bounds.size.y.ToString("0.###"))
                  .Append(" meshReadable=").Append(mesh.isReadable ? 1 : 0)
                  .Append(" meshBoundsY=").Append(mesh.bounds.size.y.ToString("0.####"))
                  .Append(" verts=").Append(mesh.vertexCount)
                  .Append(" bp=").Append(mesh.bindposes.Length)
                  .Append(" bw=").Append(mesh.boneWeights.Length).Append("]");
            }
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
                sb.Append(" mesh[worldY=").Append(r.bounds.size.y.ToString("0.###")).Append("]");
            return sb.Length == 0 ? "no renderers" : sb.ToString();
        }

        static float BakedHeight(GameObject go)
        {
            var b = BakedBounds(go);
            return b.HasValue ? b.Value.size.y : -1f;
        }

        /// <summary>
        /// Height of the actual skinned geometry, evaluated the way the GPU does
        /// (world = bone.localToWorldMatrix * bindpose * vertex, weighted by the
        /// bone weights). Independent of BakeMesh and of when Unity happens to
        /// update the skin, so it is the tie-breaker between the other two.
        /// Returns -1 when no mesh is readable.
        /// </summary>
        static float SkinnedHeight(GameObject go)
        {
            float minY = float.MaxValue, maxY = float.MinValue;
            bool any = false;
            foreach (var s in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = s.sharedMesh;
                if (mesh == null || !mesh.isReadable) continue;
                var verts = mesh.vertices;
                var bp = mesh.bindposes;
                var bw = mesh.boneWeights;
                var bones = s.bones;
                if (verts.Length == 0) continue;
                for (int i = 0; i < verts.Length; i++)
                {
                    var v = verts[i];
                    Vector3 w;
                    if (bw.Length == verts.Length && bw[i].weight0 + bw[i].weight1 +
                        bw[i].weight2 + bw[i].weight3 > 0.0001f)
                    {
                        var x = bw[i];
                        w = x.weight0 * Skin(bones, bp, x.boneIndex0, v)
                          + x.weight1 * Skin(bones, bp, x.boneIndex1, v)
                          + x.weight2 * Skin(bones, bp, x.boneIndex2, v)
                          + x.weight3 * Skin(bones, bp, x.boneIndex3, v);
                    }
                    else w = s.transform.localToWorldMatrix.MultiplyPoint3x4(v);
                    if (w.y < minY) minY = w.y;
                    if (w.y > maxY) maxY = w.y;
                    any = true;
                }
            }
            return any ? maxY - minY : -1f;
        }

        static Vector3 Skin(Transform[] bones, Matrix4x4[] bp, int bi, Vector3 v)
        {
            if (bones == null || bi < 0 || bi >= bones.Length || bones[bi] == null || bi >= bp.Length)
                return Vector3.zero;
            return bones[bi].localToWorldMatrix.MultiplyPoint3x4(bp[bi].MultiplyPoint3x4(v));
        }

        /// <summary>Press the title screen's Continue button (never New Game — that wipes the save).</summary>
        static string StartGameFromTitle()
        {
            var title = Object.FindObjectOfType<TitlePanel>();
            if (title == null) return "no title panel (already playing)";
            foreach (var b in Object.FindObjectsOfType<Button>(true))
            {
                if (b != null && b.name == "continue" && b.gameObject.activeInHierarchy)
                {
                    b.onClick.Invoke();
                    return "clicked Continue";
                }
            }
            title.Show(false);
            return "no save, hid title";
        }

        /// <summary>Editor chrome that is not a game error, but is worth reporting.</summary>
        static int editorChrome;

        /// <summary>
        /// The editor's own GUI failing, not the game: an editor session with no
        /// real window renders a 0x0 Game View, and GUIView then cannot build its
        /// RenderTexture. Worth a note rather than a failure — it is also the
        /// condition that would cull renderer-based animation, which is why
        /// ModelLoader forces AlwaysAnimate (see the note there).
        /// </summary>
        static bool EditorChromeOnly(string condition, string stack)
        {
            return condition != null && condition.Contains("RenderTexture.Create failed") &&
                   (stack ?? "").Contains("UnityEditor.GUIView");
        }

        static void OnLog(string condition, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (condition.Contains("Unhandled description string")) return; // editor-only noise
            if (EditorChromeOnly(condition, stack)) { editorChrome++; return; }
            if (runtimeErrors.Count < 12)
                runtimeErrors.Add(type + ": " + condition + "\n      " + FirstFrames(stack, 3));
        }

        // ===================================================================
        //  reporting
        // ===================================================================

        static string FirstFrames(string stack, int n)
        {
            if (string.IsNullOrEmpty(stack)) return "(no stack)";
            var parts = stack.Split('\n');
            var sb = new StringBuilder();
            for (int i = 0; i < parts.Length && i < n; i++)
                if (!string.IsNullOrWhiteSpace(parts[i])) sb.Append(parts[i].Trim()).Append(" > ");
            return sb.ToString();
        }

        static void Check(string name, bool ok, string detail = "")
        {
            if (!ok) failures++;
            Line((ok ? "PASS  " : "FAIL  ") + name + (string.IsNullOrEmpty(detail) ? "" : "  [" + detail + "]"));
        }

        static void Line(string s)
        {
            report.Add(s);
            Debug.Log("[Playtest] " + s);
        }

        static void Finish()
        {
            if (SessionState.GetBool(DoneKey, false)) return;

            Line(failures == 0 ? "== ALL PASS ==" : "== " + failures + " FAILURE(S) ==");
            try
            {
                var path = Path.Combine(Directory.GetCurrentDirectory(), ReportPath);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, string.Join("\n", report.ToArray()) + "\n", Encoding.UTF8);
                Debug.Log("[Playtest] report written to " + path);
            }
            catch (Exception e) { Debug.LogError("[Playtest] could not write report: " + e.Message); }

            SessionState.SetBool(DoneKey, true);
            Consume(MarkerPath);
            Disarm();

            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;

            if (File.Exists(ExitPath))
            {
                Consume(ExitPath);
                EditorApplication.delayCall += () => EditorApplication.Exit(failures == 0 ? 0 : 1);
            }
        }

        static void Disarm()
        {
            if (!armed) return;
            armed = false;
            EditorApplication.update -= Tick;
        }

        static void Consume(string path)
        {
            if (!AssetDatabase.DeleteAsset(path) && File.Exists(path)) File.Delete(path);
        }
    }

    /// <summary>Wakes EditorPlaytest when the marker is imported into a running editor.</summary>
    internal class PlaytestWatcher : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted,
                                           string[] moved, string[] movedFrom)
        {
            foreach (var p in imported)
                if (p == "Assets/Playtest.request") { EditorPlaytest.MarkerImported(); return; }
        }
    }
}
