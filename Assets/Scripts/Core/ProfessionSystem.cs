using System.Collections.Generic;
using UnityEngine;
using PokeProfessions.UI;

namespace PokeProfessions.Core
{
    /// <summary>
    /// Tracks profession levels/XP and computes gathering yields.
    /// Design: each action gives ~6-10 XP; a level is ~5-8 early actions.
    /// Levels unlock higher-yield nodes and harder recipes (see Recipe.requiredLevel).
    /// </summary>
    public static class ProfessionSystem
    {
        public const int MaxLevel = 10;
        public static readonly ProfessionId[] All = new ProfessionId[]
        {
            ProfessionId.Forager, ProfessionId.Miner, ProfessionId.Fisher,
            ProfessionId.Chef, ProfessionId.Smith, ProfessionId.Tailor
        };

        public static string Name(ProfessionId p)
        {
            switch (p)
            {
                case ProfessionId.Forager: return "Forager";
                case ProfessionId.Miner: return "Miner";
                case ProfessionId.Fisher: return "Fisher";
                case ProfessionId.Chef: return "Chef";
                case ProfessionId.Smith: return "Smith";
                case ProfessionId.Tailor: return "Tailor";
            }
            return p.ToString();
        }

        public static string Icon(ProfessionId p)
        {
            switch (p)
            {
                case ProfessionId.Forager: return "🌿";
                case ProfessionId.Miner: return "⛏";
                case ProfessionId.Fisher: return "🎣";
                case ProfessionId.Chef: return "🍲";
                case ProfessionId.Smith: return "🔨";
                case ProfessionId.Tailor: return "🧵";
            }
            return "·";
        }

        public static int XpToNext(int level)
        {
            if (level >= MaxLevel) return int.MaxValue;
            return 40 + level * 30;
        }

        /// <summary>Yield bonus from raw profession level (perks).</summary>
        public static int LevelYieldBonus(int level)
        {
            if (level >= 8) return 2;
            if (level >= 4) return 1;
            return 0;
        }

        /// <summary>
        /// How many of nodeId a player gathers: base + level perks + equipment.
        /// Also returns xp gained via out param.
        /// </summary>
        public static int GatherYield(GameState save, string nodeId, out int xp)
        {
            xp = 0;
            if (save == null) return 0;
            NodeDef node = NodeDB.Get(nodeId);
            if (node == null) return 0;

            int profLevel = save.ProfLevel(node.profession);
            if (profLevel < node.requiredLevel) return 0;

            int amount = node.baseYield + LevelYieldBonus(profLevel);

            string eq = save.equippedItem;
            if (eq == "forager_gloves" && node.profession == ProfessionId.Forager) amount += 1;
            if (eq == "miner_pick" && node.profession == ProfessionId.Miner) amount += 1;
            if (eq == "angler_rod" && node.profession == ProfessionId.Fisher) amount += 1;

            xp = node.xpReward;
            return Mathf.Max(1, amount);
        }

        public static void AddXp(GameState save, ProfessionId p, int xp)
        {
            int lvl = save.ProfLevel(p);
            if (lvl >= MaxLevel) return;
            save.AddProfXp(p, xp);
            while (save.ProfLevel(p) < MaxLevel && save.ProfXp(p) >= XpToNext(save.ProfLevel(p)))
            {
                save.SetProfXp(p, save.ProfXp(p) - XpToNext(save.ProfLevel(p)));
                save.SetProfLevel(p, save.ProfLevel(p) + 1);
                int newLvl = save.ProfLevel(p);
                Toast.Push(Icon(p) + " " + Name(p) + " is now level " + newLvl + "!");
            }
        }

        /// <summary>
        /// Whether a recipe bound to <paramref name="recipeStation"/> can be made at
        /// <paramref name="currentStation"/>. The specialist benches each own one
        /// ladder; the Gearwright's Table is the generalist — the single bench that
        /// will work both the weapon and the armour ladder, so a trainer who just
        /// wants gear does not have to learn which bench is which.
        /// </summary>
        public static bool StationAccepts(string recipeStation, string currentStation)
        {
            if (string.IsNullOrEmpty(recipeStation)) return true;   // craftable anywhere
            if (recipeStation == currentStation) return true;
            if (currentStation == "gear_table")
                return recipeStation == "weaponsmith" || recipeStation == "armorer";
            return false;
        }

        /// <summary>Can the player craft this recipe right now (level + inputs + station)?</summary>
        public static bool CanCraft(GameState save, Recipe r, string currentStation)
        {
            if (r == null || save == null) return false;
            if (save.ProfLevel(r.profession) < r.requiredLevel) return false;
            if (!StationAccepts(r.station, currentStation)) return false;
            foreach (var input in r.inputs)
                if (save.CountItem(input.itemId) < input.count) return false;
            return true;
        }

        public static string CraftBlocker(GameState save, Recipe r, string currentStation)
        {
            if (save.ProfLevel(r.profession) < r.requiredLevel)
                return Name(r.profession) + " Lv" + r.requiredLevel + " required";
            if (!StationAccepts(r.station, currentStation))
                return "Requires " + StationName(r.station);
            foreach (var input in r.inputs)
                if (save.CountItem(input.itemId) < input.count)
                    return "Need " + input.count + "x " + ItemDB.Name(input.itemId);
            return "";
        }

        public static string StationName(string station)
        {
            switch (station)
            {
                case "forge": return "a Forge";
                case "campfire": return "a Campfire";
                case "workbench": return "a Workbench";
                case "weaponsmith": return "a Weaponsmith's Bench";
                case "armorer": return "an Armourer's Stand";
                case "gear_table": return "a Gearwright's Table";
            }
            return "anywhere";
        }
    }
}
