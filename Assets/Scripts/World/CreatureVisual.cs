using UnityEngine;
using PokeProfessions.Core;

namespace PokeProfessions.World
{
    /// <summary>
    /// Builds and animates a stylized low-poly creature from SpeciesDef —
    /// modern creature-RPG look: chibi proportions, big expressive eyes with
    /// highlights, belly patches, layered foliage tones, blob contact shadow.
    /// Shapes: quad, serpent, avian, aquatic, rock, spirit, bug.
    /// </summary>
    public class CreatureVisual : MonoBehaviour
    {
        public SpeciesDef Species { get; private set; }
        public bool Shiny { get; private set; }

        Transform bodyT;
        float bobPhase;
        GameObject packModel;
        string packPath;
        Animation packAnim;
        string packState;      // the state packAnim is playing, so re-asking is free
        Coroutine arrivalCo;   // the running synthetic arrival, if any
        Vector3 arrivalHome;   // where the body belongs on the stage
        Vector3 arrivalHomeScale;

        /// <summary>
        /// Bumped on every pose change. A caller that schedules a later "back to
        /// idle" (BattlePanel.ReturnToIdle) captures this first and skips its write
        /// if the pose has moved on — otherwise a stale idle lands on top of a
        /// swing that started while it was waiting and cuts the swing short.
        /// </summary>
        public int PoseVersion { get; private set; }

        /// <summary>
        /// Set the pose (idle/walk/run/attack/hit/death) if the model has one.
        /// Idempotent: asking for the state that is already playing is a no-op,
        /// because the overworld wilds call this every frame as they walk, and
        /// Animation.Play restarts a clip from zero — without the guard, a roaming
        /// creature would be frozen on frame one of its walk cycle.
        /// Use ReplayState instead for a pose that repeats as an event.
        /// Loop states repeat; the event states (attack/hit/death) play once and
        /// hold their last frame until something asks for another state.
        /// </summary>
        public void PlayState(string state)
        {
            if (packAnim == null || packPath == null) return;
            if (state == packState && packAnim.IsPlaying(state)) return;
            if (!Configure(state)) return;
            packAnim.Play(state);
        }

        /// <summary>
        /// Play a pose as an event: always restart it from the wind-up, even when
        /// the same state is already playing.
        /// </summary>
        /// <remarks>
        /// The battle asks for "attack" once per swinging creature, and that ask is
        /// a new swing each time — a second one that fell through the idempotence
        /// guard above would leave the clip clamped on its last frame, so the
        /// creature would appear to wind up once and then hold still for the rest of
        /// the fight. That is the difference between the two entry points: PlayState
        /// is the per-frame roam ask, ReplayState is a discrete beat.
        /// </remarks>
        public void ReplayState(string state)
        {
            if (packAnim == null || packPath == null) return;
            if (!Configure(state)) return;

            // Rewinding is what makes it play again: Play(name) on a state that is
            // already playing (or clamped on its last frame) leaves the time alone.
            var st = packAnim[state];
            if (st != null) st.time = 0f;
            packAnim.Play(state);
        }

        /// <summary>
        /// Resolve a state's clip and configure how it plays. False when the pack
        /// has no such clip, in which case the current pose is left alone.
        /// </summary>
        bool Configure(string state)
        {
            if (string.IsNullOrEmpty(state)) return false;
            if (ModelLoader.GetClip(packPath, state) == null) return false;

            var st = packAnim[state];
            if (st == null) return false;

            st.wrapMode = Loops(state) ? WrapMode.Loop : WrapMode.ClampForever;
            st.speed = 1f;
            packState = state;
            PoseVersion++;
            return true;
        }

        /// <summary>
        /// Length in seconds of a state's clip, or 0 when the pack has none. A caller
        /// that has to come back from a one-shot pose needs the real length rather than
        /// a guess: the send-out clips run from 1.3 s (KayKit Spawn_Ground) to 3.6 s
        /// (Skeletons_Spawn_Ground), and cutting one short leaves the creature frozen
        /// mid-landing.
        /// </summary>
        public float ClipLength(string state)
        {
            if (packAnim == null || packPath == null || string.IsNullOrEmpty(state)) return 0f;
            var st = packAnim[state];
            return st != null && st.clip != null ? st.clip.length : 0f;
        }

        /// <summary>
        /// Length of the code-driven arrival that stands in when a pack has no
        /// send-out clip. Sized to read as a single leap: long enough to see the
        /// body come up out of the ground and settle, short enough not to stall
        /// the start of the battle.
        /// </summary>
        public const float SyntheticArrivalSeconds = 0.72f;

        /// <summary>
        /// Bring the creature onto the field and return how long a caller should
        /// hold before asking for idle. A pack with a real send-out clip plays it
        /// (KayKit Spawn_Ground / Skeletons_Spawn_Ground, the creature packs'
        /// leap-to-idle clips); a pack without one gets the code-driven arrival.
        /// </summary>
        /// <remarks>
        /// The product ask is that every creature *enters* rather than simply
        /// appearing. Most packs have an arrival clip, but the fish, the dragon and
        /// the wasps ship none, and those thirteen species used to stand there while
        /// everything else made an entrance. The clip path and the synthetic path
        /// return the same contract — seconds to hold — so the caller does not have
        /// to know which kind of creature it is holding for.
        /// </remarks>
        public float EnterField()
        {
            float len = ClipLength("spawn");
            if (len > 0.01f)
            {
                ReplayState("spawn");
                return len;
            }
            BeginArrival();
            return SyntheticArrivalSeconds;
        }

        /// <summary>
        /// A code-driven arrival for bodies whose pack has no send-out clip: the
        /// creature rises out of the ground along a stretched arc, lands where it
        /// belongs and squashes flat before recovering. Motions the root transform,
        /// not the rig, so it layers over the idle pose already playing and works
        /// for the procedural fallback bodies too, which have no clips at all.
        /// </summary>
        void BeginArrival()
        {
            if (arrivalCo != null)
            {
                // A second arrival on the same body (a re-send) restarts from home
                // rather than from wherever the first one got to mid-leap.
                StopCoroutine(arrivalCo);
                ResetArrival();
            }
            else
            {
                arrivalHome = transform.localPosition;
                arrivalHomeScale = transform.localScale;
            }
            arrivalCo = StartCoroutine(SyntheticArrival());
        }

        void ResetArrival()
        {
            transform.localPosition = arrivalHome;
            transform.localScale = arrivalHomeScale;
        }

        System.Collections.IEnumerator SyntheticArrival()
        {
            // Scale the leap to the creature: a fish and a dragon should not drop in
            // from the same height. Species.scale is the size the body was built at.
            float height = Mathf.Max(0.6f, Species != null ? Species.scale : 1f);
            float drop = height * 0.75f;   // starts this far below its spot
            float hop = height * 0.30f;    // overshoots the ground at the arc's apex
            const float landAt = 0.72f;    // fraction of the arrival spent in the air

            float t = 0f;
            while (t < SyntheticArrivalSeconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / SyntheticArrivalSeconds);
                float y;
                float sy = 1f, sxz = 1f;

                if (k < landAt)
                {
                    float u = k / landAt;
                    float ease = 1f - (1f - u) * (1f - u);   // ease out of the rise
                    y = Mathf.Lerp(-drop, 0f, ease) + Mathf.Sin(u * Mathf.PI) * hop;
                    // stretch along the arc, the way a leaping body reads
                    sy = 1f + 0.14f * Mathf.Sin(u * Mathf.PI);
                    sxz = 1f - 0.09f * Mathf.Sin(u * Mathf.PI);
                }
                else
                {
                    float u = (k - landAt) / (1f - landAt);
                    y = 0f;
                    // impact squash that eases out over the tail of the arrival
                    float s = Mathf.Cos(u * Mathf.PI * 0.5f);
                    sy = 1f - 0.24f * s;
                    sxz = 1f + 0.18f * s;
                }

                transform.localPosition = arrivalHome + new Vector3(0f, y, 0f);
                transform.localScale = new Vector3(arrivalHomeScale.x * sxz,
                                                   arrivalHomeScale.y * sy,
                                                   arrivalHomeScale.z * sxz);
                yield return null;
            }

            ResetArrival();
            arrivalCo = null;
        }

        /// <summary>Which states are cycles rather than one-shot events.</summary>
        static bool Loops(string state)
        {
            return state == "idle" || state == "walk" || state == "run" || state == "swim";
        }

        public static CreatureVisual Build(SpeciesDef sp, bool shiny, Transform parent)
        {
            var go = new GameObject("CreatureVisual_" + (sp != null ? sp.id : "???"));
            if (parent != null) go.transform.SetParent(parent, false);
            var cv = go.AddComponent<CreatureVisual>();
            cv.Species = sp;
            cv.Shiny = shiny;
            cv.Build();
            return cv;
        }

        void Build()
        {
            if (Species == null) return;

            // Prefer an imported model pack; fall back to procedural primitives.
            packPath = ModelLoader.PathForSpecies(Species.id);
            if (packPath != null)
            {
                float ms = Species.scale * 0.62f; // pack models are ~1.6x taller than primitives
                // boss-tier species get accent-colored aura; others get the creature pass
                string auraHex = ModelLoader.StyleFor(packPath) == ModelLoader.ModelStyle.Boss
                    ? Species.accentHex : null;
                packModel = ModelLoader.Instantiate(packPath, transform, ms, auraHex);
                if (packModel != null)
                {
                    // (ModelLoader.Instantiate already ground-aligned the model;
                    // do not reset localPosition or the lift is undone)
                    EnsureAnimator(packModel);
                    packAnim = packModel.GetComponentInChildren<Animation>();
                    WorldBuilder.AddBlobShadow(transform, 0.55f * Species.scale);
                    if (Shiny) AddShinyHalo(Species.scale);
                    bodyT = packModel.transform;
                    return;
                }
            }
            Color main = ParseHex(Species.paletteHex);
            Color accent = ParseHex(Species.accentHex);
            float s = Species.scale;

            switch (Species.shape)
            {
                case "serpent": BuildSerpent(main, accent, s); break;
                case "avian": BuildAvian(main, accent, s); break;
                case "aquatic": BuildAquatic(main, accent, s); break;
                case "rock": BuildRock(main, accent, s); break;
                case "spirit": BuildSpirit(main, accent, s); break;
                case "bug": BuildBug(main, accent, s); break;
                default: BuildQuad(main, accent, s); break;
            }

            if (Shiny) AddShinyHalo(Species.scale);

            WorldBuilder.AddBlobShadow(transform, 0.55f * Species.scale);
        }

        void AddShinyHalo(float s)
        {
            var halo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            halo.name = "shiny_halo";
            halo.transform.SetParent(transform, false);
            halo.transform.localPosition = new Vector3(0, 1.5f * s, 0);
            halo.transform.localScale = new Vector3(1.2f * s, 0.15f * s, 1.2f * s);
            var mr = halo.GetComponent<MeshRenderer>();
            mr.material = WorldBuilder.ToonMat("#FFE87A");
            Destroy(halo.GetComponent<Collider>());
        }

        /// <summary>
        /// Imported FBX have an Animator with no controller. Play the Idle
        /// clip through the legacy Animation component, which FBX imports
        /// provide for these packs.
        /// </summary>
        static void EnsureAnimator(GameObject model)
        {
            if (model == null) return;
            AnimationClip idle = null;
            foreach (var a in model.GetComponentsInChildren<Animation>())
                foreach (AnimationState st in a)
                    if (st.clip != null && st.clip.name.ToLower().Contains("idle")) { idle = st.clip; break; }

            if (idle == null) return;
            var legacy = model.GetComponentInChildren<Animation>();
            if (legacy != null)
            {
                legacy.playAutomatically = true;
                legacy.clip = idle;
                legacy.Play();
            }
        }

        // ---------------- shared part helpers ----------------

        Transform Part(string name, PrimitiveType t, Vector3 localPos, Vector3 scale, Color c)
        {
            return Part(name, t, localPos, scale, c, Vector3.zero);
        }

        Transform Part(string name, PrimitiveType t, Vector3 localPos, Vector3 scale, Color c, Vector3 euler)
        {
            var go = GameObject.CreatePrimitive(t);
            go.name = name;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.GetComponent<MeshRenderer>().material = WorldBuilder.ToonMat(
                "#" + ColorUtility.ToHtmlStringRGB(c));
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            return go.transform;
        }

        /// <summary>Unity has no cone primitive; a 45°-rotated cube reads as a diamond spike.</summary>
        void Diamond(string name, Vector3 localPos, Vector3 scale, Color c)
        {
            Part(name, PrimitiveType.Cube, localPos, scale, c, new Vector3(45f, 0f, 0f));
        }

        /// <summary>
        /// Big stylized eyes with white highlight dot — the single most
        /// important part of the modern look. headFront = center of face.
        /// </summary>
        void Eyes(Vector3 faceCenter, float headSize, Color pupil)
        {
            float ex = headSize * 0.32f;
            float ey = headSize * 0.06f;
            foreach (float sx in new[] { 1f, -1f })
            {
                var eye = Part("eye", PrimitiveType.Sphere,
                    faceCenter + new Vector3(sx * ex, ey, headSize * 0.38f),
                    new Vector3(headSize * 0.3f, headSize * 0.42f, headSize * 0.12f), pupil);
                // catchlight
                Part("spark", PrimitiveType.Sphere,
                    eye.localPosition + new Vector3(sx * headSize * 0.06f, headSize * 0.1f, headSize * 0.09f),
                    new Vector3(headSize * 0.09f, headSize * 0.09f, headSize * 0.04f), Color.white);
            }
        }

        /// <summary>Lighter belly/underside patch glued to the body front.</summary>
        void Belly(Vector3 center, Vector3 scale, Color body)
        {
            Part("belly", PrimitiveType.Sphere, center, scale, Color.Lerp(body, Color.white, 0.45f));
        }

        // ---------------- shapes ----------------

        void BuildQuad(Color main, Color accent, float s)
        {
            bodyT = NewPivot();
            Part("body", PrimitiveType.Capsule, new Vector3(0, 0.62f, 0), new Vector3(0.58f, 0.58f, 0.85f) * s, main);
            Belly(new Vector3(0, 0.5f, 0.28f) * s, new Vector3(0.36f, 0.42f, 0.42f) * s, main);

            // oversized head for the chibi read
            Part("head", PrimitiveType.Sphere, new Vector3(0, 1.08f, 0.52f) * s, new Vector3(0.62f, 0.58f, 0.58f) * s, main);
            Part("snout", PrimitiveType.Sphere, new Vector3(0, 0.98f, 0.86f) * s, new Vector3(0.24f, 0.18f, 0.24f) * s,
                Color.Lerp(main, Color.white, 0.25f));

            // ears with inner-ear color
            Part("earL", PrimitiveType.Cube, new Vector3(0.2f, 1.48f, 0.48f) * s, new Vector3(0.1f, 0.3f, 0.1f) * s, main);
            Part("earR", PrimitiveType.Cube, new Vector3(-0.2f, 1.48f, 0.48f) * s, new Vector3(0.1f, 0.3f, 0.1f) * s, main);
            Part("earInL", PrimitiveType.Cube, new Vector3(0.2f, 1.46f, 0.53f) * s, new Vector3(0.05f, 0.18f, 0.05f) * s, accent);
            Part("earInR", PrimitiveType.Cube, new Vector3(-0.2f, 1.46f, 0.53f) * s, new Vector3(0.05f, 0.18f, 0.05f) * s, accent);

            // tail with lighter tip
            Part("tail", PrimitiveType.Cylinder, new Vector3(0, 0.78f, -0.62f) * s, new Vector3(0.12f, 0.3f, 0.12f) * s, accent, new Vector3(60f, 0f, 0f));
            Part("tailTip", PrimitiveType.Sphere, new Vector3(0, 1.0f, -0.82f) * s, new Vector3(0.16f, 0.16f, 0.16f) * s,
                Color.Lerp(accent, Color.white, 0.4f));

            foreach (var (x, z, nm) in new[] { (0.26f, 0.35f, "legFL"), (-0.26f, 0.35f, "legFR"), (0.26f, -0.35f, "legBL"), (-0.26f, -0.35f, "legBR") })
                Part(nm, PrimitiveType.Cylinder, new Vector3(x, 0.18f, z) * s, new Vector3(0.13f, 0.18f, 0.13f) * s, accent);

            Eyes(new Vector3(0, 1.14f, 0.52f) * s, 0.58f * s, HexColor("#2A3038"));
        }

        void BuildSerpent(Color main, Color accent, float s)
        {
            bodyT = NewPivot();
            for (int i = 0; i < 5; i++)
            {
                float t = i / 4f;
                float r = Mathf.Lerp(0.42f, 0.16f, t) * s;
                Part("seg" + i, PrimitiveType.Sphere,
                    new Vector3(0, r + 0.05f, -i * 0.5f * s),
                    new Vector3(r * 2f, r * 2f, r * 2f), Color.Lerp(main, accent, t * 0.6f));
                // under-stripe per segment
                if (i < 4)
                    Part("stripe" + i, PrimitiveType.Sphere,
                        new Vector3(0, r * 0.35f + 0.05f, -i * 0.5f * s + 0.05f),
                        new Vector3(r * 1.1f, r * 0.5f, r * 1.1f), Color.Lerp(main, Color.white, 0.4f));
            }
            Part("head", PrimitiveType.Sphere, new Vector3(0, 0.6f, 0.45f) * s, new Vector3(0.66f, 0.58f, 0.66f) * s, main);
            Diamond("crest", new Vector3(0, 1.05f, 0.32f) * s, new Vector3(0.2f, 0.36f, 0.2f) * s, accent);
            Belly(new Vector3(0, 0.4f, 0.62f) * s, new Vector3(0.3f, 0.16f, 0.3f) * s, main);
            Eyes(new Vector3(0, 0.66f, 0.45f) * s, 0.6f * s, HexColor("#2A3038"));
        }

        void BuildAvian(Color main, Color accent, float s)
        {
            bodyT = NewPivot();
            Part("body", PrimitiveType.Sphere, new Vector3(0, 0.72f, 0) * s, new Vector3(0.56f, 0.6f, 0.68f) * s, main);
            Belly(new Vector3(0, 0.6f, 0.28f) * s, new Vector3(0.36f, 0.42f, 0.36f) * s, main);
            Part("head", PrimitiveType.Sphere, new Vector3(0, 1.22f, 0.22f) * s, new Vector3(0.5f, 0.48f, 0.5f) * s, main);

            Diamond("beak", new Vector3(0, 1.16f, 0.66f) * s, new Vector3(0.16f, 0.32f, 0.16f) * s,
                Color.Lerp(accent, HexColor("#FFB13B"), 0.65f));

            // head crest feathers
            Diamond("crest1", new Vector3(0, 1.55f, 0.1f) * s, new Vector3(0.1f, 0.28f, 0.1f) * s, accent);
            Diamond("crest2", new Vector3(0.1f, 1.5f, -0.05f) * s, new Vector3(0.08f, 0.22f, 0.08f) * s, accent);

            // wings angled out with tip color
            Part("wingL", PrimitiveType.Cube, new Vector3(0.44f, 0.78f, -0.05f) * s, new Vector3(0.16f, 0.1f, 0.72f) * s, accent, new Vector3(0f, 0f, -18f));
            Part("wingR", PrimitiveType.Cube, new Vector3(-0.44f, 0.78f, -0.05f) * s, new Vector3(0.16f, 0.1f, 0.72f) * s, accent, new Vector3(0f, 0f, 18f));
            Part("wingTipL", PrimitiveType.Sphere, new Vector3(0.52f, 0.7f, -0.42f) * s, new Vector3(0.14f, 0.1f, 0.2f) * s, main);
            Part("wingTipR", PrimitiveType.Sphere, new Vector3(-0.52f, 0.7f, -0.42f) * s, new Vector3(0.14f, 0.1f, 0.2f) * s, main);

            Part("tail", PrimitiveType.Cube, new Vector3(0, 0.72f, -0.6f) * s, new Vector3(0.3f, 0.06f, 0.42f) * s, accent, new Vector3(12f, 0f, 0f));
            Part("legL", PrimitiveType.Cylinder, new Vector3(0.14f, 0.18f, 0.05f) * s, new Vector3(0.06f, 0.18f, 0.06f) * s, HexColor("#E8A040"));
            Part("legR", PrimitiveType.Cylinder, new Vector3(-0.14f, 0.18f, 0.05f) * s, new Vector3(0.06f, 0.18f, 0.06f) * s, HexColor("#E8A040"));

            Eyes(new Vector3(0, 1.26f, 0.22f) * s, 0.48f * s, HexColor("#2A3038"));
        }

        void BuildAquatic(Color main, Color accent, float s)
        {
            bodyT = NewPivot();
            Part("body", PrimitiveType.Sphere, new Vector3(0, 0.68f, 0) * s, new Vector3(0.5f, 0.55f, 0.85f) * s, main);
            Belly(new Vector3(0, 0.52f, 0.3f) * s, new Vector3(0.32f, 0.34f, 0.42f) * s, main);
            Diamond("finTop", new Vector3(0, 1.18f, -0.05f) * s, new Vector3(0.12f, 0.42f, 0.32f) * s, accent);
            Part("finL", PrimitiveType.Cube, new Vector3(0.42f, 0.62f, 0.08f) * s, new Vector3(0.3f, 0.06f, 0.3f) * s, accent, new Vector3(0f, 0f, -14f));
            Part("finR", PrimitiveType.Cube, new Vector3(-0.42f, 0.62f, 0.08f) * s, new Vector3(0.3f, 0.06f, 0.3f) * s, accent, new Vector3(0f, 0f, 14f));
            Diamond("tailfin", new Vector3(0, 0.68f, -0.72f) * s, new Vector3(0.3f, 0.42f, 0.12f) * s, accent);
            Eyes(new Vector3(0, 0.8f, 0.38f) * s, 0.48f * s, HexColor("#2A3038"));
        }

        void BuildRock(Color main, Color accent, float s)
        {
            bodyT = NewPivot();
            Part("core", PrimitiveType.Cube, new Vector3(0, 0.58f, 0) * s, new Vector3(0.95f, 0.75f, 0.85f) * s, main, new Vector3(0f, 8f, 0f));
            Part("chunk1", PrimitiveType.Cube, new Vector3(0.32f, 1.08f, 0.15f) * s, new Vector3(0.36f, 0.3f, 0.36f) * s, accent, new Vector3(0f, 0f, 12f));
            Part("chunk2", PrimitiveType.Cube, new Vector3(-0.34f, 0.98f, -0.2f) * s, new Vector3(0.3f, 0.36f, 0.3f) * s, main, new Vector3(0f, 0f, -9f));
            Part("chunk3", PrimitiveType.Cube, new Vector3(0.05f, 1.3f, -0.3f) * s, new Vector3(0.25f, 0.25f, 0.25f) * s, accent, new Vector3(14f, 0f, 0f));
            Part("arms", PrimitiveType.Cylinder, new Vector3(0, 0.48f, 0.55f) * s, new Vector3(0.16f, 0.3f, 0.16f) * s, accent);

            // moss cap + gem eyes give it life
            Part("moss", PrimitiveType.Sphere, new Vector3(-0.18f, 1.0f, 0.28f) * s, new Vector3(0.3f, 0.12f, 0.3f) * s, HexColor("#5FA052"));
            Eyes(new Vector3(0, 0.72f, 0.42f) * s, 0.5f * s, HexColor("#FFD75E"));
        }

        void BuildSpirit(Color main, Color accent, float s)
        {
            bodyT = NewPivot();
            Part("core", PrimitiveType.Sphere, new Vector3(0, 0.9f, 0) * s, new Vector3(0.6f, 0.7f, 0.6f) * s, main);
            Part("trail1", PrimitiveType.Sphere, new Vector3(0, 0.52f, -0.3f) * s, new Vector3(0.42f, 0.42f, 0.42f) * s,
                Color.Lerp(accent, main, 0.35f));
            Part("trail2", PrimitiveType.Sphere, new Vector3(0, 0.26f, -0.55f) * s, new Vector3(0.28f, 0.28f, 0.28f) * s, accent);
            Diamond("hornL", new Vector3(0.2f, 1.5f, 0.1f) * s, new Vector3(0.12f, 0.3f, 0.12f) * s, accent);
            Diamond("hornR", new Vector3(-0.2f, 1.5f, 0.1f) * s, new Vector3(0.12f, 0.3f, 0.12f) * s, accent);

            // wisp arms
            Part("wispL", PrimitiveType.Sphere, new Vector3(0.42f, 0.75f, 0.1f) * s, new Vector3(0.18f, 0.18f, 0.18f) * s, accent);
            Part("wispR", PrimitiveType.Sphere, new Vector3(-0.42f, 0.75f, 0.1f) * s, new Vector3(0.18f, 0.18f, 0.18f) * s, accent);

            // glowing oval eyes (no pupils — spectral)
            foreach (float sx in new[] { 1f, -1f })
                Part("glowEye", PrimitiveType.Sphere,
                    new Vector3(sx * 0.17f, 1.0f, 0.44f) * s,
                    new Vector3(0.12f, 0.2f, 0.08f) * s, HexColor("#EAF6FF"));
        }

        void BuildBug(Color main, Color accent, float s)
        {
            bodyT = NewPivot();
            Part("abdomen", PrimitiveType.Sphere, new Vector3(0, 0.58f, -0.35f) * s, new Vector3(0.6f, 0.6f, 0.7f) * s, main);
            // abdomen stripes
            Part("stripe1", PrimitiveType.Cube, new Vector3(0, 0.58f, -0.18f) * s, new Vector3(0.62f, 0.62f, 0.08f) * s, accent);
            Part("stripe2", PrimitiveType.Cube, new Vector3(0, 0.58f, -0.44f) * s, new Vector3(0.6f, 0.58f, 0.08f) * s, accent);
            Part("thorax", PrimitiveType.Sphere, new Vector3(0, 0.65f, 0.2f) * s, new Vector3(0.45f, 0.45f, 0.45f) * s,
                Color.Lerp(main, accent, 0.5f));
            Part("head", PrimitiveType.Sphere, new Vector3(0, 0.75f, 0.58f) * s, new Vector3(0.4f, 0.4f, 0.4f) * s, main);

            // antennae with tip balls
            Part("antL", PrimitiveType.Cylinder, new Vector3(0.14f, 1.08f, 0.6f) * s, new Vector3(0.04f, 0.25f, 0.04f) * s, accent, new Vector3(-12f, 0f, 14f));
            Part("antR", PrimitiveType.Cylinder, new Vector3(-0.14f, 1.08f, 0.6f) * s, new Vector3(0.04f, 0.25f, 0.04f) * s, accent, new Vector3(-12f, 0f, -14f));
            Part("antTipL", PrimitiveType.Sphere, new Vector3(0.2f, 1.32f, 0.62f) * s, new Vector3(0.09f, 0.09f, 0.09f) * s, HexColor("#FFD75E"));
            Part("antTipR", PrimitiveType.Sphere, new Vector3(-0.2f, 1.32f, 0.62f) * s, new Vector3(0.09f, 0.09f, 0.09f) * s, HexColor("#FFD75E"));

            Part("wingL", PrimitiveType.Cube, new Vector3(0.36f, 0.95f, -0.12f) * s, new Vector3(0.38f, 0.04f, 0.5f) * s,
                Color.Lerp(accent, Color.white, 0.55f), new Vector3(0f, 0f, -10f));
            Part("wingR", PrimitiveType.Cube, new Vector3(-0.36f, 0.95f, -0.12f) * s, new Vector3(0.38f, 0.04f, 0.5f) * s,
                Color.Lerp(accent, Color.white, 0.55f), new Vector3(0f, 0f, 10f));

            Eyes(new Vector3(0, 0.8f, 0.58f) * s, 0.4f * s, HexColor("#2A3038"));
        }

        // ---------------- misc ----------------

        Transform NewPivot()
        {
            var t = new GameObject("pivot").transform;
            t.SetParent(transform, false);
            return t;
        }

        static Color ParseHex(string hex)
        {
            Color c;
            return ColorUtility.TryParseHtmlString(hex, out c) ? c : Color.gray;
        }

        static Color HexColor(string hex)
        {
            Color c;
            return ColorUtility.TryParseHtmlString(hex, out c) ? c : Color.black;
        }

        void Update()
        {
            if (bodyT == null) return;
            bobPhase += Time.deltaTime * 2.2f;
            float bob = Mathf.Sin(bobPhase) * 0.06f;
            var p = bodyT.localPosition;
            p.y = packModel != null ? Mathf.Max(0f, bob) : bob; // pack models: don't sink below ground
            bodyT.localPosition = p;
        }
    }
}
