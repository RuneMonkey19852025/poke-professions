using System.Collections.Generic;
using UnityEngine;

namespace PokeProfessions.Core
{
    [System.Serializable]
    public class NodeDef
    {
        public string id;
        public string name;
        public ProfessionId profession;
        public int requiredLevel;
        public List<string> itemPool = new List<string>(); // pick weighted, last entry rare
        public List<int> poolWeights = new List<int>();
        public int baseYield = 1;
        public int xpReward = 8;
        public string zone;
    }

    public static class NodeDB
    {
        public static Dictionary<string, NodeDef> All;

        public static void Init()
        {
            All = new Dictionary<string, NodeDef>();

            // ---- Meadowfall ----
            // Lv0 starter nodes: gatherable before any profession training
            N("bush_meadowgrass", "Meadow Grass", ProfessionId.Forager, 0, 8,
              new[] { "fiber" }, "meadow");
            N("rock_pebble", "Pebble Scatter", ProfessionId.Miner, 0, 8,
              new[] { "stone" }, "meadow");
            N("spot_creek", "Creek Shallows", ProfessionId.Fisher, 0, 9,
              new[] { "raw_fish" }, "meadow");
            N("bush_bitterleaf", "Bitterleaf Bush", ProfessionId.Forager, 1, 8,
              new[] { "bitterleaf", "bitterleaf", "fiber" }, "meadow");
            N("bush_fiber", "Sunfiber Patch", ProfessionId.Forager, 1, 8,
              new[] { "fiber", "fiber", "bitterleaf" }, "meadow");
            N("bush_emberpepper", "Emberpepper Plant", ProfessionId.Forager, 2, 8,
              new[] { "emberpepper", "emberpepper", "bitterleaf" }, "meadow");
            N("stump_glowcap", "Glowcap Stump", ProfessionId.Forager, 3, 8,
              new[] { "glowcap", "glowcap", "fiber" }, "hollow");
            N("rock_stone", "Loose Scree", ProfessionId.Miner, 1, 8,
              new[] { "stone", "stone", "copper_ore" }, "meadow");
            N("rock_copper", "Copper Vein", ProfessionId.Miner, 2, 10,
              new[] { "copper_ore", "copper_ore", "stone", "sigil_piece_c" }, "ridge");
            N("rock_iron", "Iron Vein", ProfessionId.Miner, 4, 12,
              new[] { "iron_ore", "iron_ore", "stone", "sigil_piece_c" }, "ridge");
            N("rock_moon", "Hollow Monolith", ProfessionId.Miner, 6, 20,
              new[] { "moonshard", "stone", "iron_ore" }, "hollow_deep");

            // Lv2 outcrops ringing Emberpost: the start area's own ore, so a
            // new Miner can work the meadow instead of walking out to the
            // ridge. No sigil fragments here — the Ember fragment stays a
            // frontier (ridge vein) drop so the quest still sends you there.
            N("rock_iron_shallow", "Iron Outcrop", ProfessionId.Miner, 2, 10,
              new[] { "iron_ore", "iron_ore", "stone" }, "meadow");
            N("rock_copper_shallow", "Copper Outcrop", ProfessionId.Miner, 2, 10,
              new[] { "copper_ore", "copper_ore", "stone" }, "meadow");
            N("spot_river", "River Shoal", ProfessionId.Fisher, 1, 9,
              new[] { "raw_fish", "raw_fish", "raw_fish", "gildfish" }, "meadow");
            N("spot_lake", "Mirrorlake Pier", ProfessionId.Fisher, 3, 10,
              new[] { "raw_fish", "gildfish", "raw_fish", "gildfish" }, "lake");

            // weights: uniform 3 except last (rare) 1
            foreach (var kv in All)
            {
                var w = new List<int>();
                for (int i = 0; i < kv.Value.itemPool.Count; i++)
                    w.Add(i == kv.Value.itemPool.Count - 1 ? 1 : 3);
                kv.Value.poolWeights = w;
            }
        }

        static void N(string id, string name, ProfessionId prof, int reqLvl, int xp,
                      string[] pool, string zone, int baseYield = 1)
        {
            All[id] = new NodeDef
            {
                id = id, name = name, profession = prof, requiredLevel = reqLvl,
                xpReward = xp, itemPool = new List<string>(pool), zone = zone, baseYield = baseYield
            };
        }

        public static NodeDef Get(string id)
        {
            NodeDef n;
            return All != null && All.TryGetValue(id, out n) ? n : null;
        }

        public static string RollItem(NodeDef n)
        {
            int total = 0;
            foreach (int w in n.poolWeights) total += w;
            int roll = Random.Range(0, total);
            for (int i = 0; i < n.itemPool.Count; i++)
            {
                roll -= n.poolWeights[i];
                if (roll < 0) return n.itemPool[i];
            }
            return n.itemPool[0];
        }
    }
}
