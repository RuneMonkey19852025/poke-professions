using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PokeProfessions.Core;
using PokeProfessions.UI;
using PokeProfessions.Audio;

namespace PokeProfessions.World
{
    /// <summary>
    /// Orchestrates DB init, world build, zone tracking, wild encounters and
    /// autosave. Lives under GameBootstrap (single-scene game).
    /// </summary>
    public class Game : MonoBehaviour
    {
        public static Game I { get; private set; }

        public GameState State { get; private set; }
        public WorldBuilder World { get; private set; }
        public string CurrentZone => State.curZone;

        PlayerController player;
        float nextAutosave;
        float encounterCooldown;
        bool sleeperSpawned;

        void Awake()
        {
            I = this;

            ItemDB.Init();
            MoveDB.Init();
            CreatureDB.Init();
            NodeDB.Init();
            StoryData.Init();

            State = GameState.Load();
            if (State == null)
            {
                State = new GameState();
                var starter = MonsterFactory.Create("sprigling", 5);
                if (starter != null) State.party.Add(starter);
            }

            World = gameObject.AddComponent<WorldBuilder>();

            var worldRoot = new GameObject("WorldRoot");
            World.ZoneVolumeRoot = worldRoot.transform;
            World.Build();

            SpawnPlayer();
            SpawnStoryObjects();
            SpawnTrainersIfDefeated();

            nextAutosave = Time.time + 20f;
        }

        void SpawnPlayer()
        {
            EnsureLighting();

            var pf = new GameObject("Player");
            pf.AddComponent<CapsuleCollider>();
            pf.AddComponent<PlayerController>();
            pf.transform.position = new Vector3(State.playerX, WorldBuilder.GroundHeight(State.playerX, State.playerZ) + 0.6f, State.playerZ);

            // Prefer a KayKit adventurer as the player body.
            var playerModel = ModelLoader.Instantiate("KayKitAdventurers/Characters/fbx/Ranger", pf.transform, 1f);
            if (playerModel != null)
            {
                playerModel.transform.localPosition = Vector3.zero;
                WorldBuilder.AddBlobShadow(pf.transform, 0.5f);
                SpawnCamera(pf);
                player = pf.GetComponent<PlayerController>();
                return;
            }

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "visual";
            body.transform.SetParent(pf.transform, false);
            body.transform.localPosition = new Vector3(0, 0.75f, 0);
            body.transform.localScale = new Vector3(0.55f, 0.8f, 0.55f);
            body.GetComponent<MeshRenderer>().material = WorldBuilder.ToonMat("#E0784A");
            var visCol = body.GetComponent<Collider>();
            if (visCol != null) Destroy(visCol);

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "head";
            head.transform.SetParent(pf.transform, false);
            head.transform.localPosition = new Vector3(0, 1.6f, 0);
            head.transform.localScale = new Vector3(0.42f, 0.42f, 0.42f);
            head.GetComponent<MeshRenderer>().material = WorldBuilder.ToonMat("#F2CFA0");
            var headCol = head.GetComponent<Collider>();
            if (headCol != null) Destroy(headCol);

            // explorer cap with brim
            var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cap.name = "cap";
            cap.transform.SetParent(pf.transform, false);
            cap.transform.localPosition = new Vector3(0, 1.74f, 0);
            cap.transform.localScale = new Vector3(0.44f, 0.26f, 0.44f);
            cap.GetComponent<MeshRenderer>().material = WorldBuilder.ToonMat("#D8604A");
            var capCol = cap.GetComponent<Collider>();
            if (capCol != null) Destroy(capCol);

            var brim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            brim.name = "capBrim";
            brim.transform.SetParent(pf.transform, false);
            brim.transform.localPosition = new Vector3(0, 1.66f, 0.28f);
            brim.transform.localScale = new Vector3(0.4f, 0.03f, 0.24f);
            brim.GetComponent<MeshRenderer>().material = WorldBuilder.ToonMat("#C05040");
            var brimCol = brim.GetComponent<Collider>();
            if (brimCol != null) Destroy(brimCol);

            // backpack
            var pack = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pack.name = "backpack";
            pack.transform.SetParent(pf.transform, false);
            pack.transform.localPosition = new Vector3(0, 0.95f, -0.4f);
            pack.transform.localScale = new Vector3(0.32f, 0.4f, 0.2f);
            pack.GetComponent<MeshRenderer>().material = WorldBuilder.ToonMat("#8A5E33");
            var packCol = pack.GetComponent<Collider>();
            if (packCol != null) Destroy(packCol);

            // eyes
            WorldBuilder.AddEyes(pf.transform, 1.62f, 0.13f, 0.3f, "#2A3438");
            WorldBuilder.AddBlobShadow(pf.transform, 0.5f);

            player = pf.GetComponent<PlayerController>();

            SpawnCamera(pf);
        }

        void SpawnCamera(GameObject pf)
        {
            var camGo = new GameObject("MainCamera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.farClipPlane = 400f;
            cam.backgroundColor = ColorFromHex("#9FD4E8");
            cam.clearFlags = CameraClearFlags.Skybox;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<FollowCamera>().target = pf.transform;
            camGo.transform.position = pf.transform.position + new Vector3(0, 12f, -11f);
        }

        static void EnsureLighting()
        {
            if (Object.FindObjectOfType<Light>() != null) return;
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var ambientGo = new GameObject("AmbientFill");
            var fill = ambientGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.55f, 0.65f, 0.85f);
            fill.intensity = 0.45f;
            fill.shadows = LightShadows.None;
            ambientGo.transform.rotation = Quaternion.Euler(310f, 140f, 0f);
        }

        static Color ColorFromHex(string hex)
        {
            Color c;
            return ColorUtility.TryParseHtmlString(hex, out c) ? c : Color.white;
        }

        public void SetZone(string zoneId)
        {
            if (State.curZone == zoneId) return;
            State.curZone = zoneId;
            Debug.Log("[World] Entered zone: " + zoneId);
        }

        // ---------------- spawning by story state ----------------

        void SpawnStoryObjects()
        {
            // The Sleeper only appears in the deep hollow once the herald falls.
            if (State.defeatedTrainers.Contains("trainer_zephy"))
                World.SpawnNpc("sleepers", new Vector3(55f, 0f, 82f));
        }

        void SpawnTrainersIfDefeated()
        {
            // Trainer NPCs live in the world permanently; their battles start
            // through dialogue. Nothing to re-spawn here.
        }

        // ---------------- encounters ----------------

        public void RollEncounter()
        {
            if (UiRouter.I != null && UiRouter.I.UiOpen) return;
            if (Time.time < encounterCooldown) return;

            string zone = State.curZone;
            if (zone == "hollow_deep") return; // story area: no random fights

            float chance = Time.deltaTime * 0.09f; // ~10s average between wilds while walking
            string eq = State.equippedItem;
            if (eq == "lure_charm") chance *= 1.25f;

            if (UnityEngine.Random.value < chance)
            {
                encounterCooldown = 3.5f;
                var wild = MonsterFactory.CreateWildForZone(zone, State);
                if (wild == null) return;

                var engine = new BattleEngine(new List<Creature>(State.party), new List<Creature> { wild }, null);
                UiRouter.I.StartBattle(engine);
            }
        }

        public void NotifyPlayerDefeat()
        {
            StartCoroutine(RespawnAfterDefeat());
        }

        System.Collections.IEnumerator RespawnAfterDefeat()
        {
            yield return new WaitForSeconds(1.2f);
            // heal and respawn at the ranch
            State.HealAll();
            State.playerX = 0f;
            State.playerZ = -16f;
            var pc = FindObjectOfType<PlayerController>();
            if (pc != null)
            {
                pc.transform.position = new Vector3(State.playerX,
                    WorldBuilder.GroundHeight(State.playerX, State.playerZ) + 0.6f, State.playerZ);
            }
            Toast.Push("You wake at the ranch, creatures tended and healed.");
        }

        public void MarkBattleFinished()
        {
            encounterCooldown = 6f;
            State.Save();
        }

        void Update()
        {
            // position tracking for save
            var pc = player;
            if (pc != null)
            {
                State.playerX = pc.transform.position.x;
                State.playerZ = pc.transform.position.z;
            }

            if (Time.time >= nextAutosave)
            {
                nextAutosave = Time.time + 25f;
                State.Save();
            }

            if (!sleeperSpawned && State.defeatedTrainers.Contains("trainer_zephy"))
            {
                sleeperSpawned = true;
                World.SpawnNpc("sleepers", new Vector3(55f, 0f, 82f));
                Toast.Push("Something stirs in the deep Hollow...");
            }
        }
    }
}
