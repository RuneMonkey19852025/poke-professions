using System.Collections.Generic;
using UnityEngine;
using PokeProfessions.Core;
using PokeProfessions.UI;
using PokeProfessions.Audio;

namespace PokeProfessions.World
{
    /// <summary>Base for anything the player can press E on.</summary>
    public abstract class Interactable : MonoBehaviour
    {
        [Tooltip("Shown in the interact prompt.")]
        public string interactLabel;
        public float interactRange = 3.2f;

        public abstract void Interact(GameObject player);
        public virtual string Prompt() { return interactLabel; }
    }

    /// <summary>Player enters a zone volume -> world switches encounter zone.</summary>
    public class ZoneVolume : MonoBehaviour
    {
        public string zoneId;
        bool armed = true;

        void OnTriggerEnter(Collider other)
        {
            if (!armed) return;
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;
            armed = false;
            if (Game.I != null) Game.I.SetZone(zoneId);
        }

        void OnTriggerExit(Collider other)
        {
            armed = true;
        }
    }

    /// <summary>A harvestable resource: bush, vein, fishing spot.</summary>
    public class GatherNode : Interactable
    {
        public string nodeId;
        [HideInInspector] public float respawnAt = -1f;

        bool depleted;

        void Update()
        {
            if (depleted && respawnAt > 0f && Time.time >= respawnAt) ResetNode();
        }

        Transform visualRoot;

        public void BuildVisual()
        {
            var def = NodeDB.Get(nodeId);
            if (def == null) return;
            interactLabel = def.name;

            switch (def.profession)
            {
                case ProfessionId.Forager: visualRoot = BuildBushVisual(); break;
                case ProfessionId.Miner: visualRoot = BuildRockVisual(); break;
                default: visualRoot = BuildFishingVisual(); break;
            }
            if (visualRoot != null) visualRoot.name = "visual";

            var trigger = gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 1.6f;
            trigger.center = new Vector3(0, 0.7f, 0);
        }

        Transform NewVisualRoot()
        {
            var t = new GameObject("visual").transform;
            t.SetParent(transform, false);
            return t;
        }

        Transform Part(Transform parent, PrimitiveType t, Vector3 pos, Vector3 scale, string hex, Vector3? euler = null)
        {
            var go = GameObject.CreatePrimitive(t);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
            go.GetComponent<MeshRenderer>().material = WorldBuilder.ToonMat(hex);
            var c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
            return go.transform;
        }

        Transform BuildBushVisual()
        {
            var root = NewVisualRoot();
            string leaf = nodeId.Contains("fiber") ? "#6FA84E" : nodeId.Contains("pepper") ? "#4E8F42" : "#58A852";
            Part(root, PrimitiveType.Sphere, new Vector3(0f, 0.45f, 0f), new Vector3(0.95f, 0.8f, 0.95f), leaf);
            Part(root, PrimitiveType.Sphere, new Vector3(0.42f, 0.35f, 0.2f), new Vector3(0.6f, 0.55f, 0.6f), WorldBuilder.Lighten(leaf, 0.1f));
            Part(root, PrimitiveType.Sphere, new Vector3(-0.4f, 0.32f, -0.15f), new Vector3(0.55f, 0.5f, 0.55f), WorldBuilder.Darken(leaf, 0.1f));
            // berries/peppers
            string fruit = nodeId.Contains("pepper") ? "#E85030" : nodeId.Contains("fiber") ? "#D8C860" : "#E87088";
            Part(root, PrimitiveType.Sphere, new Vector3(0.3f, 0.62f, 0.35f), new Vector3(0.14f, 0.14f, 0.14f), fruit);
            Part(root, PrimitiveType.Sphere, new Vector3(-0.28f, 0.5f, 0.4f), new Vector3(0.12f, 0.12f, 0.12f), fruit);
            Part(root, PrimitiveType.Sphere, new Vector3(0.05f, 0.72f, -0.3f), new Vector3(0.13f, 0.13f, 0.13f), fruit);
            return root;
        }

        Transform BuildRockVisual()
        {
            var root = NewVisualRoot();
            Part(root, PrimitiveType.Cube, new Vector3(0f, 0.42f, 0f), new Vector3(1.0f, 0.8f, 0.95f), "#8E8A80", new Vector3(0f, 30f, 0f));
            Part(root, PrimitiveType.Cube, new Vector3(0.35f, 0.3f, -0.3f), new Vector3(0.5f, 0.5f, 0.5f), "#7E7A72", new Vector3(0f, -20f, 12f));
            Part(root, PrimitiveType.Cube, new Vector3(-0.38f, 0.28f, 0.25f), new Vector3(0.45f, 0.45f, 0.45f), "#98948A", new Vector3(0f, 10f, -14f));
            // ore flecks
            string ore = nodeId.Contains("copper") ? "#C87838" : nodeId.Contains("iron") ? "#B8BCC4" :
                         nodeId.Contains("moon") ? "#9AB8E8" : "#C8BC98";
            Part(root, PrimitiveType.Sphere, new Vector3(0.2f, 0.72f, 0.25f), new Vector3(0.16f, 0.12f, 0.14f), ore);
            Part(root, PrimitiveType.Sphere, new Vector3(-0.3f, 0.55f, -0.2f), new Vector3(0.13f, 0.1f, 0.12f), ore);
            Part(root, PrimitiveType.Sphere, new Vector3(0.42f, 0.4f, -0.05f), new Vector3(0.11f, 0.09f, 0.1f), ore);
            return root;
        }

        Transform BuildFishingVisual()
        {
            var root = NewVisualRoot();
            // dark water disc + ripple rings + floating bobber
            Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(1.7f, 0.02f, 1.7f), "#3E7CA8");
            Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.04f, 0f), new Vector3(1.1f, 0.015f, 1.1f), "#5FA0CC");
            Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.055f, 0f), new Vector3(0.55f, 0.012f, 0.55f), "#8FC8E0");
            Part(root, PrimitiveType.Sphere, new Vector3(0.15f, 0.14f, 0.1f), new Vector3(0.16f, 0.16f, 0.16f), "#E85848");
            return root;
        }

        public override string Prompt()
        {
            var def = NodeDB.Get(nodeId);
            if (def == null) return interactLabel;
            if (depleted) return def.name + " (depleted)";
            if (Game.I != null && Game.I.State != null && Game.I.State.ProfLevel(def.profession) < def.requiredLevel)
                return def.name + " (needs " + ProfessionSystem.Name(def.profession) + " Lv" + def.requiredLevel + ")";
            return def.name + "  [" + ProfessionSystem.Icon(def.profession) + "]";
        }

        public override void Interact(GameObject player)
        {
            if (depleted) return;
            var save = Game.I.State;
            var def = NodeDB.Get(nodeId);
            if (def == null) return;

            if (save.ProfLevel(def.profession) < def.requiredLevel)
            {
                Toast.Push("Requires " + ProfessionSystem.Name(def.profession) + " level " + def.requiredLevel);
                return;
            }

            int xp;
            int amount = ProfessionSystem.GatherYield(save, nodeId, out xp);
            if (amount <= 0) return;

            string item = NodeDB.RollItem(def);
            save.AddItem(item, amount);
            ProfessionSystem.AddXp(save, def.profession, xp);

            Toast.Push("+" + amount + " " + ItemDB.Name(item) + "  (+" + xp + " xp)");
            if (Sfx.I != null) Sfx.I.Play("gather");

            depleted = true;
            SetVisualEnabled(false);
            respawnAt = Time.time + RespawnSeconds();
        }

        float RespawnSeconds()
        {
            var def = NodeDB.Get(nodeId);
            if (def == null) return 45f;
            switch (def.profession)
            {
                case ProfessionId.Forager: return 40f;
                case ProfessionId.Miner: return 70f;
                default: return 55f;
            }
        }

        void SetVisualEnabled(bool on)
        {
            foreach (var rend in GetComponentsInChildren<MeshRenderer>())
                rend.enabled = on;
            foreach (var c in GetComponentsInChildren<Collider>())
                if (!c.isTrigger) c.enabled = on;
            var trig = GetComponent<Collider>();
            if (trig != null) trig.enabled = on;
        }

        void ResetNode()
        {
            depleted = false;
            respawnAt = -1f;
            SetVisualEnabled(true);
        }
    }

    /// <summary>Crafting station: forge / campfire / workbench.</summary>
    public class CraftingStation : Interactable
    {
        public string stationId;

        void Awake()
        {
            // Station visuals are built by WorldBuilder.SpawnStation; this only
            // adds the interaction trigger.
            var trigger = gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 1.8f;
            trigger.center = new Vector3(0, 0.8f, 0);
            interactLabel = StationLabel(stationId);
        }

        string StationLabel(string s)
        {
            if (s == "forge") return "Forge";
            if (s == "campfire") return "Campfire";
            if (s == "workbench") return "Workbench";
            return s;
        }

        public override string Prompt() { return interactLabel + "  [craft]"; }

        public override void Interact(GameObject player)
        {
            if (UiRouter.I != null) UiRouter.I.OpenCrafting(stationId);
        }
    }

    /// <summary>Vale Post traders stall.</summary>
    public class ShopInteractable : Interactable
    {
        public string shopId;

        void Awake()
        {
            // Stall visuals are built by WorldBuilder.SpawnShop; this only
            // adds the interaction trigger.
            var trigger = gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 2f;
            trigger.center = new Vector3(0, 1f, 0);
            interactLabel = "Vale Post Traders  [shop]";
        }

        public override string Prompt() { return interactLabel; }

        public override void Interact(GameObject player)
        {
            if (UiRouter.I != null) UiRouter.I.OpenShop(shopId);
        }
    }

    /// <summary>Talking NPC. Dialogue node is chosen by DialogueRuntime.PickNode.</summary>
    public class NpcInteractable : Interactable
    {
        public string npcId;

        void Awake() { BuildVisual(); }

        void BuildVisual()
        {
            // Body/head visuals are built by WorldBuilder.SpawnNpc; this only
            // adds the interaction trigger.
            var trigger = gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 2.4f;
            trigger.center = new Vector3(0, 1f, 0);
        }

        public override string Prompt()
        {
            NpcDef def;
            if (StoryData.Npcs != null && StoryData.Npcs.TryGetValue(npcId, out def) && def != null)
                return def.name + "  [talk]";
            return "Talk";
        }

        public override void Interact(GameObject player)
        {
            if (UiRouter.I != null) UiRouter.I.TalkTo(npcId);
        }
    }

    /// <summary>
    /// The Lucky Snorlax — a slot machine in Emberpost. Press E to play:
    /// 25c a spin, pays coins and crafting materials.
    /// </summary>
    public class SlotsInteractable : Interactable
    {
        void Awake()
        {
            interactLabel = "Lucky Snorlax  [slots]";
        }

        public override string Prompt() { return interactLabel; }

        public override void Interact(GameObject player)
        {
            if (UiRouter.I != null) UiRouter.I.OpenSlots();
        }
    }
}
