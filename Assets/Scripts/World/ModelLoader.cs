using System.Collections.Generic;
using UnityEngine;

namespace PokeProfessions.World
{
    /// <summary>
    /// Runtime loader for the third-party model packs under Resources/Models.
    /// Resolves a species id to the root GameObject of an imported FBX and
    /// provides animation-clip lookup. Every lookup fails gracefully so the
    /// procedural CreatureVisual stays as the safety net.
    /// Only CC0 packs are mapped (Quaternius + CuteMonsters + KayKit);
    /// the CC-BY-SA Mantis Hatchery pack is not referenced in code.
    /// </summary>
    public static class ModelLoader
    {
        /// <summary>Visual treatment applied to an imported model.</summary>
        public enum ModelStyle { Human, Creature, Boss }

        // ---- style tuning knobs (tweak after seeing in game) ----
        const float HumanHeadScale = 1.32f;   // chibi head boost for humans
        const float HumanEyeScale = 1.5f;     // eye bone boost when rig has eyes
        const float CreatureHeadScale = 1.26f;// cute head boost for creatures
        const float BossHeadScale = 1.15f;    // restrained boost for high-poly bosses
        const float AnimeEyeSize = 0.085f;    // sclera radius factor on head bone
        const float BossAuraRadius = 1.7f;    // aura ring radius factor
        // gameplay state -> clip keywords (first match wins)
        static readonly Dictionary<string, string[]> ClipKeywords = new Dictionary<string, string[]>
        {
            { "idle",   new[] { "idle", "static", "flying", "swim" } },
            { "walk",   new[] { "walk", "swim", "flying" } },
            { "run",    new[] { "run", "sprint", "gallop", "swim", "flying" } },
            { "attack", new[] { "attack_melee", "melee_attack", "attack", "1h_melee", "2h_melee", "bite", "sting", "swipe", "slash", "jump" } },
            { "hit",    new[] { "hitrecieve", "hit", "stagger", "alerted", "no" } },
            { "death",  new[] { "death_a", "death", "die", "defeat" } },
        };

        // Clip-name keywords that read as a creature taking the field, best first:
        // a dedicated spawn landing, then the packs' leap-into-idle clips. Kept out
        // of ClipKeywords on purpose — see MapSpawn.
        static readonly string[] SpawnKeywords =
            { "spawn_ground", "spawn", "jump_toidle", "_jump", "jump" };

        // ---- shared clip libraries -----------------------------------------
        // Some rigs keep their clips in a separate file from their characters.
        // The KayKit humans/skeletons carry no clips at all: their animations
        // are the "KayKit Character Animations" pack (CC0, Kay Lousberg),
        // imported Legacy. Each family gets its own folder so a model only ever
        // sees clips authored for its own rig; a model whose path starts with the
        // key borrows its states from the matching library, and a clip embedded
        // in the model itself always wins (see MapClips).
        static readonly Dictionary<string, string> ClipLibrary = new Dictionary<string, string>
        {
            { "KayKitAdventurers", "Models/KayKitAnimations" },            // Rig_Medium
            { "KayKitSkeletons",   "Models/KayKitSkeletons/Animations" },  // Rig_Medium Special
        };

        // Exact clip names to take for a gameplay state, tried before the keyword
        // mapping: the library ships variants, so keywords alone would take
        // "Melee_2H_Idle" for idle and "Jump_Full_Long" for attack (the attack
        // keywords include "jump"). Names not present in a family's folder fall
        // through to the keyword pass.
        static readonly Dictionary<string, string[]> PreferredClips =
            new Dictionary<string, string[]>
        {
            { "idle",   new[] { "Idle_A", "Skeletons_Idle" } },
            { "walk",   new[] { "Walking_A", "Skeletons_Walking" } },
            { "run",    new[] { "Running_A" } },
            { "attack", new[] { "Melee_1H_Attack_Chop", "Melee_1H_Attack_Stab" } },
            { "hit",    new[] { "Hit_A", "Hit_B" } },
            { "death",  new[] { "Death_A", "Skeletons_Death" } },
        };

        // ---- measured import heights (FBX probe) for every mapped creature
        // model. Instantiate rescales so a model lands at the size the
        // procedural visuals expect (CuteMonsters ~1.45 units == 1.0x).
        // Measured from the FBX vertex bounds; keep in sync with the packs.
        static readonly Dictionary<string, float> ModelRawHeight = new Dictionary<string, float>
        {
            // CuteMonsters (reference pack — ~1.45 units)
            { "CuteMonsters/Alien", 1.42f }, { "CuteMonsters/Alien_Tall", 1.57f },
            { "CuteMonsters/Bat", 1.42f }, { "CuteMonsters/Bee", 1.49f },
            { "CuteMonsters/Cactus", 1.71f }, { "CuteMonsters/Chicken", 1.55f },
            { "CuteMonsters/Crab", 1.42f }, { "CuteMonsters/Cthulhu", 1.49f },
            { "CuteMonsters/Cyclops", 1.47f }, { "CuteMonsters/Deer", 1.45f },
            { "CuteMonsters/Demon", 1.42f }, { "CuteMonsters/Ghost", 1.46f },
            { "CuteMonsters/GreenDemon", 1.42f }, { "CuteMonsters/Mushroom", 3.31f },
            { "CuteMonsters/Panda", 1.47f }, { "CuteMonsters/Penguin", 1.55f },
            { "CuteMonsters/Pig", 1.89f }, { "CuteMonsters/Skull", 1.46f },
            { "CuteMonsters/Tree", 1.73f }, { "CuteMonsters/YellowDragon", 1.42f },
            { "CuteMonsters/Yeti", 1.44f },
            // Quaternius Farm Animals (3.5–9.8)
            { "Quaternius/FarmAnimals/Cow", 9.17f }, { "Quaternius/FarmAnimals/Horse", 9.24f },
            { "Quaternius/FarmAnimals/Llama", 4.91f }, { "Quaternius/FarmAnimals/Pig", 9.78f },
            { "Quaternius/FarmAnimals/Pug", 3.51f }, { "Quaternius/FarmAnimals/Sheep", 6.66f },
            { "Quaternius/FarmAnimals/Zebra", 9.24f },
            // Quaternius Ultimate Animated Animals (3.9–8.1)
            { "Quaternius/Animals/Alpaca", 4.30f }, { "Quaternius/Animals/Bull", 8.07f },
            { "Quaternius/Animals/Cow", 8.07f }, { "Quaternius/Animals/Deer", 4.40f },
            { "Quaternius/Animals/Donkey", 4.71f }, { "Quaternius/Animals/Fox", 5.88f },
            { "Quaternius/Animals/Horse", 5.68f }, { "Quaternius/Animals/Horse_White", 5.68f },
            { "Quaternius/Animals/Husky", 3.89f }, { "Quaternius/Animals/ShibaInu", 3.95f },
            { "Quaternius/Animals/Stag", 4.84f }, { "Quaternius/Animals/Wolf", 5.55f },
            // Quaternius Easy Enemies (2.4–7.0)
            { "Quaternius/Enemies/Frog", 3.21f }, { "Quaternius/Enemies/Rat", 6.98f },
            { "Quaternius/Enemies/Snake", 2.88f }, { "Quaternius/Enemies/Snake_angry", 2.88f },
            { "Quaternius/Enemies/Spider", 5.27f }, { "Quaternius/Enemies/Wasp", 2.39f },
            // Quaternius Dinosaurs (3.6–49.7)
            { "Quaternius/Dinosaurs/Apatosaurus", 49.74f }, { "Quaternius/Dinosaurs/Parasaurolophus", 12.48f },
            { "Quaternius/Dinosaurs/Stegosaurus", 23.05f }, { "Quaternius/Dinosaurs/Trex", 31.03f },
            { "Quaternius/Dinosaurs/Triceratops", 3.60f }, { "Quaternius/Dinosaurs/Velociraptor", 3.77f },
            // Quaternius Monsters (3.7–4.3)
            { "Quaternius/Monsters/Dragon", 3.71f },
            // Quaternius Fish (1.6–30.6)
            { "Quaternius/Fish/Dolphin", 9.27f }, { "Quaternius/Fish/Fish1", 3.19f },
            { "Quaternius/Fish/Fish2", 1.97f }, { "Quaternius/Fish/Fish3", 1.58f },
            { "Quaternius/Fish/Manta ray", 11.73f }, { "Quaternius/Fish/Shark", 15.83f },
            { "Quaternius/Fish/Whale", 30.58f },
        };
        // A model at its raw import height should appear this tall after the
        // caller's scale (CreatureVisual passes Species.scale * 0.62, which
        // was calibrated against the ~1.45-unit CuteMonsters models).
        const float ReferenceModelHeight = 1.45f;

        // NOTE on scale: this table is the whole sizing rule — no measured
        // rescale. The raw height above is the size the *animated* model lands
        // near, because the FBX unit conversion lives on the skeleton: a
        // Legacy-imported rig carries scale 100 on its armature nodes while its
        // mesh vertices are metres, so a bind-pose bounds read is ~100x the
        // animated model (measured: Deer 449.0u worldY vs 4.40u raw FBX).
        // Dividing a measurement by that is exactly the bug that shrank
        // creatures to 0.02u, hence the fixed formula in Instantiate.
        // Ground alignment is the only thing left to do at runtime, and it
        // lives in ModelFooting.

        // species id -> "PackFolder/ModelName" (no extension)
        static readonly Dictionary<string, string> SpeciesModel = new Dictionary<string, string>
        {
            // ---- quad-ish species -> Quaternius mammals ----
            { "sprigling",  "Quaternius/Animals/Deer" },
            { "thornpaw",   "Quaternius/Animals/Alpaca" },
            { "briarwolf",  "Quaternius/Dinosaurs/Stegosaurus" },
            { "thornmare",  "Quaternius/Monsters/Dragon" },
            { "puddlepup",  "Quaternius/Animals/Husky" },
            { "tidehound",  "Quaternius/Animals/Cow" },
            { "maelmoor",   "Quaternius/Animals/Horse" },
            { "abyssshepherd", "CuteMonsters/Cthulhu" },
            { "cindercub",  "Quaternius/Animals/Fox" },
            { "pyrelynx",   "Quaternius/Enemies/Snake_angry" },
            { "solarmane",  "Quaternius/Animals/Stag" },
            { "pyrelord",   "Quaternius/Dinosaurs/Trex" },
            { "burrowmouse","Quaternius/Enemies/Rat" },
            { "vaultmole",  "Quaternius/Enemies/Rat" },
            { "ashkit",     "Quaternius/Enemies/Frog" },
            { "smoulder",   "Quaternius/Dinosaurs/Parasaurolophus" },
            { "sparkit",    "Quaternius/Animals/ShibaInu" },
            { "voltfang",   "Quaternius/Animals/Husky" },
            { "fulgurox",   "Quaternius/Animals/Bull" },
            { "thumper",    "Quaternius/Dinosaurs/Velociraptor" },
            { "boulderhare","Quaternius/Animals/Donkey" },
            { "duskstalker","Quaternius/Enemies/Spider" },
            { "nightmaw",   "Quaternius/Animals/Wolf" },
            { "umbrafox",   "CuteMonsters/Ghost" },
            { "nocturne",   "CuteMonsters/Cthulhu" },
            { "gildra",     "CuteMonsters/YellowDragon" },

            // ---- avian ----
            { "flitwing",   "CuteMonsters/Chicken" },
            { "galedart",   "CuteMonsters/Penguin" },

            // ---- rock ----
            { "magmite",    "CuteMonsters/Mushroom" },
            { "cragma",     "CuteMonsters/Cyclops" },

            // ---- aquatic ----
            { "glimmfish",  "Quaternius/Fish/Fish1" },
            { "luminkeel",  "Quaternius/Fish/Fish2" },
            { "aurorafin",  "Quaternius/Fish/Dolphin" },
            { "frostfin",   "Quaternius/Fish/Fish3" },
            { "rimefin",    "Quaternius/Fish/Shark" },
            { "lilypad",    "CuteMonsters/Crab" },
            { "bloomdrift", "CuteMonsters/Cactus" },

            // ---- bug / spirit ----
            { "mothlumen",  "CuteMonsters/Bee" },
            { "dreamora",   "CuteMonsters/Alien" },
            { "reverie",    "CuteMonsters/Deer" },
            { "gloomcap",   "CuteMonsters/Mushroom" },
            { "sporesage",  "CuteMonsters/GreenDemon" },
            { "mycelorn",   "CuteMonsters/Tree" },
            { "sleeperling","CuteMonsters/Skull" },
            { "frostwisp",  "CuteMonsters/Demon" },
        };

        // NPC id -> KayKit human/skeleton models (textured, embedded clips)
        static readonly Dictionary<string, string> NpcModel = new Dictionary<string, string>
        {
            { "maren",  "KayKitAdventurers/Characters/fbx/Ranger" },   // farmer overalls
            { "pell",   "KayKitAdventurers/Characters/fbx/Mage" },   // grey elder
            { "mira",   "KayKitAdventurers/Characters/fbx/Rogue_Hooded" },   // light blue
            { "juna",   "KayKitAdventurers/Characters/fbx/Rogue" },   // purple
            { "brann",  "KayKitAdventurers/Characters/fbx/Barbarian" },   // smith red
            { "odessa", "KayKitAdventurers/Characters/fbx/Knight" },   // captain
            { "hask",   "KayKitAdventurers/Characters/fbx/Knight" },   // quartermaster
            { "tobin",  "KayKitAdventurers/Characters/fbx/Barbarian" },   // rival
            { "sela",   "KayKitAdventurers/Characters/fbx/Mage" },   // herbalist
            { "korrin", "KayKitSkeletons/Skeleton_Warrior" },
            { "vesna",  "KayKitSkeletons/Skeleton_Minion" },
            { "hollis", "KayKitSkeletons/Skeleton_Rogue" },
            { "zephy",  "KayKitSkeletons/Skeleton_Mage" },
        };

        static readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, Dictionary<string, AnimationClip>> clipCache = new Dictionary<string, Dictionary<string, AnimationClip>>();

        /// <summary>
        /// Load + map all animation clips for a model path into gameplay
        /// states (idle/walk/run/attack/hit/death) from the embedded clips
        /// inside the model FBX (Quaternius "Armature|Name" style).
        /// </summary>
        public static Dictionary<string, AnimationClip> ClipsFor(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath)) return null;
            string lookup = Normalize(resourcePath);
            if (clipCache.TryGetValue(lookup, out var cached)) return cached;

            var map = new Dictionary<string, AnimationClip>();

            // The shared library comes first for the rig families that have one:
            // a KayKit model's own clips are imported Generic and cannot be played
            // by the legacy Animation component at all, so they must not occupy a
            // state the Legacy library can fill. Anything the library does not
            // cover falls through to the model's own clips below.
            foreach (var lib in ClipLibrary)
            {
                if (!resourcePath.StartsWith(lib.Key)) continue;
                var library = Resources.LoadAll<AnimationClip>(lib.Value);
                MapPreferred(library, map);
                MapClips(library, map);
            }

            // embedded clips inside the model FBX
            var own = Resources.LoadAll<AnimationClip>(lookup);
            MapClips(own, map);

            // Last, and additively: the send-out pose (see MapSpawn).
            var seen = new List<AnimationClip>();
            foreach (var lib in ClipLibrary)
                if (resourcePath.StartsWith(lib.Key))
                    seen.AddRange(Resources.LoadAll<AnimationClip>(lib.Value));
            seen.AddRange(own);
            MapSpawn(seen.ToArray(), map);

            clipCache[lookup] = map;
            return map;
        }

        /// <summary>
        /// Fill the <c>spawn</c> state — the send-out a creature plays as it takes
        /// the field — from whatever clip the pack has that reads as an arrival.
        /// </summary>
        /// <remarks>
        /// Deliberately its own pass, run after every other state has had its pick,
        /// and only into a clip no other state already took. The clips that read as
        /// an arrival are the packs' jump clips, and "jump" is also an attack
        /// keyword (some creatures attack by lunging), so leaving spawn to the
        /// general pass could quietly hand a creature's attack to its arrival pose.
        /// Purely additive: it can add a state, never re-point one.
        ///
        /// Families differ. The KayKit characters and skeletons have real send-outs
        /// (Spawn_Ground, Skeletons_Spawn_Ground); Quaternius Animals leap in and land
        /// on Jump_toIdle, Dinosaurs have *_Jump, most CuteMonsters carry Jump. The
        /// rest — fish, the dragon, the wasps — simply have none, and those creatures
        /// appear without one rather than borrowing a pose that means something else.
        /// </remarks>
        static void MapSpawn(AnimationClip[] clips, Dictionary<string, AnimationClip> map)
        {
            if (clips == null || map.ContainsKey("spawn")) return;

            var claimed = new HashSet<string>();
            foreach (var kv in map)
                if (kv.Value != null) claimed.Add(kv.Value.name);

            foreach (var want in SpawnKeywords)
                foreach (var clip in clips)
                {
                    if (clip == null || claimed.Contains(clip.name)) continue;
                    if (clip.name.ToLower().Contains(want)) { map["spawn"] = clip; return; }
                }
        }

        /// <summary>
        /// Fill states by exact clip name (see PreferredClips), for the states
        /// the keyword mapping would guess wrong.
        /// </summary>
        static void MapPreferred(AnimationClip[] clips, Dictionary<string, AnimationClip> map)
        {
            if (clips == null) return;
            foreach (var kv in PreferredClips)
            {
                if (map.ContainsKey(kv.Key)) continue;
                foreach (var want in kv.Value)
                {
                    bool found = false;
                    foreach (var clip in clips)
                        if (clip != null && clip.name == want) { map[kv.Key] = clip; found = true; break; }
                    if (found) break;
                }
            }
        }

        /// <summary>
        /// Map clips onto gameplay states by name (first keyword match wins),
        /// filling only the states still missing — so a clip embedded in the
        /// model outranks the one borrowed from a library.
        /// </summary>
        static void MapClips(AnimationClip[] clips, Dictionary<string, AnimationClip> map)
        {
            if (clips == null) return;
            foreach (var clip in clips)
            {
                if (clip == null) continue;
                string n = clip.name.ToLower();
                foreach (var kv in ClipKeywords)
                {
                    if (map.ContainsKey(kv.Key)) continue;
                    foreach (var kw in kv.Value)
                        if (n.Contains(kw)) { map[kv.Key] = clip; break; }
                    if (map.ContainsKey(kv.Key)) break;
                }
                // generic take name -> idle fallback
                if (!map.ContainsKey("idle") && n.Contains("take")) map["idle"] = clip;
            }
        }

        /// <summary>Clip for a gameplay state on the given model path, or null.</summary>
        public static AnimationClip GetClip(string resourcePath, string state)
        {
            var map = ClipsFor(resourcePath);
            AnimationClip c;
            return map != null && map.TryGetValue(state, out c) ? c : null;
        }

        /// <summary>Model resource path for a species, or null to use procedural visuals.</summary>
        public static string PathForSpecies(string speciesId)
        {
            if (string.IsNullOrEmpty(speciesId)) return null;
            return SpeciesModel.TryGetValue(speciesId, out var p) ? p : null;
        }

        /// <summary>Model resource path for an NPC, or null.</summary>
        public static string PathForNpc(string npcId)
        {
            return npcId != null && NpcModel.TryGetValue(npcId, out var p) ? p : null;
        }

        /// <summary>Model paths are stored without their "Models/" folder
        /// segment; Resources loads relative to Assets/Resources, so prepend it.</summary>
        static string Normalize(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath)) return resourcePath;
            return resourcePath.StartsWith("Models/") ? resourcePath : "Models/" + resourcePath;
        }

        /// <summary>Load (and cache) the model root GameObject, or null.</summary>
        public static GameObject Load(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath)) return null;
            if (cache.TryGetValue(resourcePath, out var go) && go != null) return go;
            go = Resources.Load<GameObject>(Normalize(resourcePath));
            if (go != null) cache[resourcePath] = go;
            return go;
        }

        /// <summary>
        /// Instantiate a creature/NPC model. Returns null when unavailable.
        /// Applies the style pass (chibi heads, anime eyes, boss aura) and
        /// sets up legacy Animation playback with mapped clips.
        /// </summary>
        /// <summary>Boot-time diagnostic: probe Resources load paths in the player.
        /// Silent when everything loads; warns loudly when they don't.</summary>
        public static void Diag()
        {
            var kk = Resources.Load<GameObject>(Normalize("KayKitAdventurers/Characters/fbx/Ranger"));
            var sk = Resources.Load<GameObject>(Normalize("KayKitSkeletons/Skeleton_Warrior"));
            if (kk == null || sk == null)
                Debug.LogWarning("[ModelDiag] KK=" + (kk != null) + " SK=" + (sk != null) +
                                 " — model packs not fully available; some visuals will fall back");
        }

        public static GameObject Instantiate(string resourcePath, Transform parent, float scale, string accentHex = null)
        {
            var src = Load(resourcePath);
            if (src == null)
            {
                Debug.LogWarning("[Model] MISSING from build: '" + resourcePath + "' — falling back to procedural visual");
                return null;
            }
            var go = Object.Instantiate(src);
            go.name = "Model_" + resourcePath;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one;

            Chibiify(go, StyleFor(resourcePath), accentHex);
            SetupAnimation(go, resourcePath);

            // The size, final on the very first frame: the recorded raw FBX
            // height, which is the size the animated model lands near. Models
            // with no recorded height (KayKit NPCs, Generic-imported like the
            // Nature kit) keep the caller's scale.
            float rawH = 0f;
            ModelRawHeight.TryGetValue(resourcePath, out rawH);
            if (rawH > 0.001f)
                go.transform.localScale = Vector3.one * (scale * ReferenceModelHeight / rawH);
            else
                go.transform.localScale = Vector3.one * scale;

            // Keep skinned meshes updating even off-screen: a culled
            // SkinnedMeshRenderer is not re-skinned, so its bounds stay at the
            // bind pose (~100x here) and Unity culls creatures that are on
            // screen. Doing it before any measurement also means the deferred
            // ground pass below reads the animated pose, not the bind pose.
            KeepSkinnedMeshesVisible(go);

            // Feet on the ground once the idle pose has been skinned (ModelFooting).
            ModelFooting.Attach(go, scale * ReferenceModelHeight);
            return go;
        }

        /// <summary>
        /// Skinned renderers with a scaled skeleton get a bounds estimate that can
        /// be far too small, which makes Unity cull creatures that are still on
        /// screen. These packs all have few enough meshes that keeping them
        /// updated off-screen is cheaper than the popping.
        /// </summary>
        public static void KeepSkinnedMeshesVisible(GameObject model)
        {
            foreach (var s in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                if (s != null) s.updateWhenOffscreen = true;
        }

        /// <summary>Which visual treatment a pack folder gets.</summary>
        public static ModelStyle StyleFor(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath)) return ModelStyle.Creature;
            if (resourcePath.StartsWith("KayKit")) return ModelStyle.Human;
            return ModelStyle.Creature;
        }

        /// <summary>
        /// Anime/monster-collector proportions, applied to the rig bones:
        /// - Humans: big chibi head + enlarged anime eyes.
        /// - Creatures: bigger head for the cute mascot read.
        /// - Bosses: head boost + glowing evolution ring at the feet.
        /// </summary>
        public static void Chibiify(GameObject model, ModelStyle style, string accentHex = null)
        {
            if (model == null) return;
            Transform head = null;
            var eyeBones = new List<Transform>();
            foreach (var t in model.GetComponentsInChildren<Transform>())
            {
                string n = t.name.ToLower();
                if (head == null && (n.Contains("head") || n.Contains("skull")) && !n.Contains("band") && !n.Contains("top"))
                    head = t;
                if (n.Contains("eye")) eyeBones.Add(t);
            }

            switch (style)
            {
                case ModelStyle.Human:
                    if (head != null)
                    {
                        head.localScale = Vector3.Scale(head.localScale,
                            new Vector3(HumanHeadScale, HumanHeadScale, HumanHeadScale));
                        if (eyeBones.Count > 0)
                            foreach (var e in eyeBones)
                                e.localScale = Vector3.Scale(e.localScale,
                                    new Vector3(HumanEyeScale, HumanEyeScale, 1.15f));
                        else
                            AttachAnimeEyes(head);
                    }
                    break;

                case ModelStyle.Creature:
                    if (head != null)
                        head.localScale = Vector3.Scale(head.localScale,
                            new Vector3(CreatureHeadScale, CreatureHeadScale, CreatureHeadScale));
                    break;

                case ModelStyle.Boss:
                    if (head != null)
                        head.localScale = Vector3.Scale(head.localScale,
                            new Vector3(BossHeadScale, BossHeadScale, BossHeadScale));
                    AttachAuraRing(model, accentHex);
                    break;
            }
        }

        /// <summary>Big anime eyes parented to the head bone (sclera + pupil + catchlight).</summary>
        static void AttachAnimeEyes(Transform head)
        {
            foreach (float sx in new[] { 1f, -1f })
            {
                var sclera = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sclera.name = "animeEye" + (sx > 0 ? "L" : "R");
                sclera.transform.SetParent(head, false);
                sclera.transform.localPosition = new Vector3(sx * AnimeEyeSize, 0.045f, 0.135f);
                sclera.transform.localScale = new Vector3(AnimeEyeSize * 0.9f, AnimeEyeSize * 1.2f, AnimeEyeSize * 0.5f);
                sclera.GetComponent<MeshRenderer>().material = WorldBuilder.ToonMat("#FFFFFF");
                var c1 = sclera.GetComponent<Collider>(); if (c1 != null) Object.Destroy(c1);

                var pupil = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pupil.name = "animePupil";
                pupil.transform.SetParent(sclera.transform, false);
                pupil.transform.localPosition = new Vector3(0, -0.02f, 0.55f);
                pupil.transform.localScale = new Vector3(0.55f, 0.75f, 0.5f);
                pupil.GetComponent<MeshRenderer>().material = WorldBuilder.ToonMat("#2A3038");
                var c2 = pupil.GetComponent<Collider>(); if (c2 != null) Object.Destroy(c2);

                var spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                spark.name = "animeSpark";
                spark.transform.SetParent(pupil.transform, false);
                spark.transform.localPosition = new Vector3(-0.18f, 0.22f, 0.6f);
                spark.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
                spark.GetComponent<MeshRenderer>().material = WorldBuilder.ToonMat("#FFFFFF");
                var c3 = spark.GetComponent<Collider>(); if (c3 != null) Object.Destroy(c3);
            }
        }

        /// <summary>Glowing evolution-style ring flat on the ground around a boss creature.</summary>
        static void AttachAuraRing(GameObject model, string accentHex)
        {
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "aura_ring";
            ring.transform.SetParent(model.transform, false);
            ring.transform.localPosition = new Vector3(0, 0.04f, 0);
            ring.transform.localScale = new Vector3(BossAuraRadius, 0.02f, BossAuraRadius);
            ring.GetComponent<MeshRenderer>().material = WorldBuilder.ToonMat(
                string.IsNullOrEmpty(accentHex) ? "#7FB0E8" : accentHex);
            var c = ring.GetComponent<Collider>(); if (c != null) Object.Destroy(c);
        }

        /// <summary>Wire mapped clips into a legacy Animation component and play idle.</summary>
        static void SetupAnimation(GameObject go, string resourcePath)
        {
            var clips = ClipsFor(resourcePath);
            if (clips == null || clips.Count == 0) return;

            // the Animator has no controller and would override legacy playback
            var animator = go.GetComponent<Animator>();
            if (animator != null) animator.enabled = false;

            var anim = go.GetComponent<Animation>();
            if (anim == null) anim = go.AddComponent<Animation>();

            // Keep the clips evaluated when the rig is not being drawn. The default
            // for an imported component is BasedOnRenderers, which stops ticking a
            // model that is off-camera — and "not drawn" is broader than
            // "off-screen": with no real viewport (an editor session launched
            // without a window leaves the Game View at 0x0) the whole scene is
            // unrendered and every pose freezes mid-cycle. A frozen rig is not just
            // a missing animation, it is a creature that pops mid-stride when it
            // comes back into view. This is the same trade KeepSkinnedMeshesVisible
            // makes for skinning, and it exists for the same reason: the player
            // looks one way at a time, so the wilds behind them, the NPC around the
            // corner and both battle creatures spend most of their lives off-camera.
            anim.cullingType = AnimationCullingType.AlwaysAnimate;

            foreach (var kv in clips)
                if (kv.Value != null) anim.AddClip(kv.Value, kv.Key);

            // Only play when the state actually landed: AddClip silently refuses
            // some clips (non-legacy / mismatched rigs), and Play("idle") then
            // spams "state could not be found" on every instantiation.
            AnimationClip idle;
            if (clips.TryGetValue("idle", out idle) && idle != null && anim["idle"] != null)
            {
                anim.clip = idle;
                anim.wrapMode = WrapMode.Loop;
                anim.Play("idle");
            }
        }

        /// <summary>
        /// Find the first animation clip whose name contains any of the
        /// given keywords (case-insensitive), e.g. "idle", "walk".
        /// </summary>
        public static AnimationClip FindClip(GameObject modelRoot, params string[] keywords)
        {
            if (modelRoot == null || keywords == null || keywords.Length == 0) return null;
            var anims = modelRoot.GetComponentsInChildren<Animation>();
            foreach (var kw in keywords)
            {
                foreach (var anim in anims)
                {
                    foreach (AnimationState st in anim)
                        if (st.clip != null && st.clip.name.ToLower().Contains(kw.ToLower()))
                            return st.clip;
                }
                var animators = modelRoot.GetComponentsInChildren<Animator>();
                foreach (var an in animators)
                {
                    if (an.runtimeAnimatorController == null) continue;
                    foreach (var clip in an.runtimeAnimatorController.animationClips)
                        if (clip != null && clip.name.ToLower().Contains(kw.ToLower()))
                            return clip;
                }
            }
            return null;
        }

        public static void ClearCache() => cache.Clear();
    }
}
