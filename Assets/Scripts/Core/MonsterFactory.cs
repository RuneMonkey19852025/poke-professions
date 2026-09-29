using System.Collections.Generic;
using UnityEngine;

namespace PokeProfessions.Core
{
    /// <summary>
    /// Wild-encounter loot: defeated creatures shed crafting materials themed
    /// to their element. Diggers kick up ore; the Hollow's deep rares hum with
    /// moonshards. Quantity scales with the fallen creature's level.
    /// </summary>
    public static class WildLoot
    {
        public struct Drop { public string id; public int n; public Drop(string id, int n) { this.id = id; this.n = n; } }

        public static List<Drop> Roll(Creature wild)
        {
            var drops = new List<Drop>();
            if (wild == null || wild.Species == null) return drops;
            string sp = wild.speciesId;

            // specials first
            if (sp == "vaultmole") drops.Add(new Drop("copper_ore", 1));
            if (sp == "cragma")
            {
                drops.Add(new Drop("copper_ore", 1));
                if (Random.value < 0.35f) drops.Add(new Drop("iron_ore", 1));
            }
            if ((sp == "sleeperling" || sp == "frostwisp" || sp == "gildra") && Random.value < 0.6f)
                drops.Add(new Drop("moonshard", 1));

            // element-themed material, quantity scaling with level
            string mat = MaterialFor(wild.Species.element);
            int n = 1 + wild.level / 12;
            drops.Add(new Drop(mat, n));
            if (Random.value < 0.25f) drops.Add(new Drop(mat, 1)); // bonus handful
            return drops;
        }

        static string MaterialFor(Element el)
        {
            switch (el)
            {
                case Element.Verdant: return "fiber";
                case Element.Tide: return "raw_fish";
                case Element.Ember: return "emberpepper";
                case Element.Stone: return "stone";
                case Element.Toxic: return "bitterleaf";
                case Element.Mind: return "glowcap";
                case Element.Shade: return "bitterleaf";
                case Element.Frost: return "glowcap";
                default: return "fiber"; // Gale, Beast, Normal
            }
        }
    }

    /// <summary>Builds Creature instances for wild spawns, trainers and gifts.</summary>
    public static class MonsterFactory
    {
        public static Creature Create(string speciesId, int level)
        {
            var sp = CreatureDB.Get(speciesId);
            if (sp == null) return null;

            var c = new Creature { speciesId = speciesId, level = Mathf.Clamp(level, 1, BattleEngine.MaxCreatureLevel) };
            FillMoves(c, sp);
            c.curHP = c.MaxHP;
            c.shiny = Random.value < 0.015f; // 1.5% shiny
            return c;
        }

        public static void FillMoves(Creature c, SpeciesDef sp)
        {
            c.moves.Clear();
            if (sp.learnset == null) return;
            // a creature knows up to its level's learnset entries, keeps last 4
            int upTo = Mathf.Min(sp.learnset.Count, c.level);
            int start = Mathf.Max(0, upTo - 4);
            for (int i = start; i < upTo; i++)
            {
                string mv = sp.learnset[i];
                if (!c.moves.Contains(mv)) c.moves.Add(CreatureDB.SafeMove(mv));
            }
            if (c.moves.Count == 0) c.moves.Add("tackle");
        }

        public static Creature CreateWildForZone(string zone, GameState save)
        {
            var pool = CreatureDB.WildPoolFor(zone);
            if (pool == null || pool.Count == 0) return Create("burrowmouse", 3);

            string species = pool[Random.Range(0, pool.Count)];
            var sp = CreatureDB.Get(species);

            int lo = 2, hi = 4; // meadow: gentler than the starter (lv5)
            switch (zone)
            {
                case "ridge": lo = 8; hi = 12; break;
                case "lake": lo = 6; hi = 10; break;
                case "hollow": lo = 10; hi = 14; break;
                case "hollow_deep": lo = 14; hi = 18; break;
            }

            int lvl = Random.Range(lo, hi + 1);
            string eq = save != null ? save.equippedItem : "";
            if (eq == "scout_band") lvl = Mathf.Max(2, lvl - 2);

            return Create(species, lvl);
        }

        public static Creature FromTrainerEntry(string entry)
        {
            var parts = entry.Split(':');
            int lvl = parts.Length > 1 && int.TryParse(parts[1], out int l) ? l : 5;
            return Create(parts[0], lvl);
        }
    }
}
