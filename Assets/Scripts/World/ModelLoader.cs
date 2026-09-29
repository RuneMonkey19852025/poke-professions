using System.Collections.Generic;
using UnityEngine;

namespace PokeProfessions.World
{
    /// <summary>
    /// Runtime loader for the third-party model packs under Resources/Models.
    /// Resolves a species id to the root GameObject of an imported FBX and
    /// provides animation-clip lookup. Every lookup fails gracefully so the
    /// procedural CreatureVisual stays as the safety net.
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
            { "idle",   new[] { "idle" } },
            { "walk",   new[] { "walk" } },
            { "run",    new[] { "run" } },
            { "attack", new[] { "attack", "bite", "sting", "swipe" } },
            { "hit",    new[] { "hitrecieve", "hit", "stagger", "alerted" } },
            { "death",  new[] { "death", "die" } },
        };

        // Mantis packs ship one animation per FBX; map states to file names
        static readonly Dictionary<string, string[]> MantisAnimFiles = new Dictionary<string, string[]>
        {
            { "idle",   new[] { "idle", "idle2" } },
            { "walk",   new[] { "walk" } },
            { "run",    new[] { "run" } },
            { "attack", new[] { "bite", "sting", "arm_swipe" } },
            { "hit",    new[] { "stagger_forward", "stagger", "alerted_forward", "alerted" } },
            { "death",  new[] { "die_forward", "die", "die_back" } },
        };
        // species id -> "PackFolder/ModelName" (no extension)
        static readonly Dictionary<string, string> SpeciesModel = new Dictionary<string, string>
        {
            // ---- quad-ish species -> Quaternius mammals ----
            { "sprigling",  "Quaternius/FarmAnimals/Sheep" },
            { "thornpaw",   "Quaternius/FarmAnimals/Pug" },
            { "briarwolf",  "Quaternius/Monsters/Dragon" },
            { "puddlepup",  "Quaternius/FarmAnimals/Pig" },
            { "tidehound",  "Quaternius/FarmAnimals/Cow" },
            { "maelmoor",   "Quaternius/FarmAnimals/Horse" },
            { "cindercub",  "Quaternius/FarmAnimals/Llama" },
            { "pyrelynx",   "Quaternius/Dinosaurs/Velociraptor" },
            { "solarmane",  "Quaternius/Dinosaurs/Trex" },
            { "burrowmouse","Quaternius/Enemies/Rat" },
            { "vaultmole",  "Quaternius/Enemies/Rat" },
            { "ashkit",     "Quaternius/Enemies/Frog" },
            { "smoulder",   "Quaternius/Dinosaurs/Parasaurolophus" },
            { "umbrafox",   "CuteMonsters/Ghost" },
            { "nocturne",   "CuteMonsters/Cthulhu" },
            { "gildra",     "CuteMonsters/Yeti" },

            // ---- avian ----
            { "flitwing",   "CuteMonsters/Chicken" },
            { "galedart",   "CuteMonsters/Penguin" },

            // ---- rock ----
            { "magmite",    "CuteMonsters/Mushroom" },
            { "cragma",     "CuteMonsters/Cyclops" },

            // ---- aquatic ----
            { "glimmfish",  "Quaternius/Fish/Fish1" },
            { "luminkeel",  "Quaternius/Fish/Fish2" },
            { "lilypad",    "CuteMonsters/Crab" },
            { "bloomdrift", "CuteMonsters/Cactus" },

            // ---- bug / spirit ----
            { "mothlumen",  "CuteMonsters/Bee" },
            { "dreamora",   "CuteMonsters/Alien" },
            { "gloomcap",   "CuteMonsters/Mushroom" },
            { "sporesage",  "CuteMonsters/GreenDemon" },
            { "sleeperling","CuteMonsters/Skull" },
            { "frostwisp",  "CuteMonsters/Demon" },
        };

        // Mantis high-poly creatures for evolved / boss-tier species
        static readonly Dictionary<string, string> BossModel = new Dictionary<string, string>
        {
            { "briarwolf",   "MantisHatchery/horns/horns" },
            { "maelmoor",    "MantisHatchery/undead_serpent/undead_serpent" },
            { "solarmane",   "MantisHatchery/dragonewt/dragonewt" },
            { "cragma",      "MantisHatchery/bug/bug" },
            { "bloomdrift",  "MantisHatchery/plant_monster/plant_monster" },
            { "galedart",    "MantisHatchery/mini_wyvern/mini_wyvern" },
            { "luminkeel",   "MantisHatchery/fish/fish" },
            { "dreamora",    "MantisHatchery/needles/needles" },
            { "gildra",      "MantisHatchery/sloth/sloth" },
        };

        // NPC id -> Kenney Blocky Character (chibi, 26 embedded clips each)
        static readonly Dictionary<string, string> NpcModel = new Dictionary<string, string>
        {
            { "maren",  "KenneyBlocky/character-d" },   // farmer overalls
            { "pell",   "KenneyBlocky/character-h" },   // grey elder
            { "mira",   "KenneyBlocky/character-i" },   // light blue
            { "juna",   "KenneyBlocky/character-e" },   // purple
            { "brann",  "KenneyBlocky/character-c" },   // smith red
            { "odessa", "KenneyBlocky/character-g" },   // captain
            { "tobin",  "KenneyBlocky/character-j" },   // rival
            { "sela",   "KenneyBlocky/character-f" },   // herbalist
            { "korrin", "KenneyBlocky/character-k" },
            { "vesna",  "KenneyBlocky/character-l" },
            { "hollis", "KenneyBlocky/character-m" },
            { "zephy",  "KenneyBlocky/character-n" },
        };

        static readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, Dictionary<string, AnimationClip>> clipCache = new Dictionary<string, Dictionary<string, AnimationClip>>();

        /// <summary>
        /// Load + map all animation clips for a model path into gameplay
        /// states (idle/walk/run/attack/hit/death). Handles both embedded
        /// clips (Quaternius "Armature|Name") and Mantis per-file clips.
        /// </summary>
        public static Dictionary<string, AnimationClip> ClipsFor(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath)) return null;
            if (clipCache.TryGetValue(resourcePath, out var cached)) return cached;

            var map = new Dictionary<string, AnimationClip>();

            // embedded clips inside the model FBX
            foreach (var clip in Resources.LoadAll<AnimationClip>(resourcePath))
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
                // generic take name (Mantis main FBX) -> idle fallback
                if (!map.ContainsKey("idle") && n.Contains("take")) map["idle"] = clip;
            }

            // Mantis: separate animation FBX files next to the model
            if (resourcePath.StartsWith("MantisHatchery"))
            {
                int slash = resourcePath.LastIndexOf('/');
                string baseFolder = slash > 0 ? resourcePath.Substring(0, slash + 1) : "";
                foreach (var kv in MantisAnimFiles)
                {
                    if (map.ContainsKey(kv.Key)) continue;
                    foreach (var file in kv.Value)
                    {
                        foreach (var folder in new[] { "animations/", "animation/" })
                        {
                            var clips = Resources.LoadAll<AnimationClip>(baseFolder + folder + file);
                            if (clips != null && clips.Length > 0) { map[kv.Key] = clips[0]; break; }
                        }
                        if (map.ContainsKey(kv.Key)) break;
                    }
                }
            }

            clipCache[resourcePath] = map;
            return map;
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
            if (BossModel.TryGetValue(speciesId, out var bossPath) && Resources.Load<GameObject>(bossPath) != null)
                return bossPath;
            return SpeciesModel.TryGetValue(speciesId, out var p) ? p : null;
        }

        /// <summary>Model resource path for an NPC, or null.</summary>
        public static string PathForNpc(string npcId)
        {
            return npcId != null && NpcModel.TryGetValue(npcId, out var p) ? p : null;
        }

        /// <summary>Load (and cache) the model root GameObject, or null.</summary>
        public static GameObject Load(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath)) return null;
            if (cache.TryGetValue(resourcePath, out var go) && go != null) return go;
            go = Resources.Load<GameObject>(resourcePath);
            if (go != null) cache[resourcePath] = go;
            return go;
        }

        /// <summary>
        /// Instantiate a creature/NPC model. Returns null when unavailable.
        /// Applies the style pass (chibi heads, anime eyes, boss aura) and
        /// sets up legacy Animation playback with mapped clips.
        /// </summary>
        public static GameObject Instantiate(string resourcePath, Transform parent, float scale, string accentHex = null)
        {
            var src = Load(resourcePath);
            if (src == null) return null;
            var go = Object.Instantiate(src);
            go.name = "Model_" + resourcePath;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one * scale;
            Chibiify(go, StyleFor(resourcePath), accentHex);
            SetupAnimation(go, resourcePath);
            return go;
        }

        /// <summary>Which visual treatment a pack folder gets.</summary>
        public static ModelStyle StyleFor(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath)) return ModelStyle.Creature;
            if (resourcePath.StartsWith("KayKit")) return ModelStyle.Human;
            if (resourcePath.StartsWith("KenneyBlocky")) return ModelStyle.Human;
            if (resourcePath.StartsWith("Mantis")) return ModelStyle.Boss;
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

            foreach (var kv in clips)
                anim.AddClip(kv.Value, kv.Key);

            AnimationClip idle;
            if (clips.TryGetValue("idle", out idle))
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
