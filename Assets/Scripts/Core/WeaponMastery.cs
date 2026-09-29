using System.Collections.Generic;
using UnityEngine;
using PokeProfessions.UI;

namespace PokeProfessions.Core
{
    /// <summary>
    /// Weapon mastery: the more a creature fights with a class of equipped
    /// weapon (claws, fangs, spines, plates, charms), the stronger that
    /// class gets. Every KO with a weapon equipped grants XP to its class;
    /// each level adds +6% attack power with that class (mirrors how
    /// professions level from gathering/crafting).
    /// </summary>
    public static class WeaponMastery
    {
        public const int MaxLevel = 100;
        // per-level increments are sized so Lv100 reaches the same caps the old
        // Lv10 system had: +60% ATK for weapons, −50% damage taken for armor.
        public const float BonusPerLevel = 0.006f;    // +0.6% ATK per level → +60% at Lv100
        public const int XpPerKO = 8;                 // weapon XP: KO with the class equipped
        public const float DefReducePerLevel = 0.005f; // −0.5% damage taken per level → −50% at Lv100
        public const int ArmorXpPerWin = 6;           // armor XP: victory while wearing the class

        // ---- mastery techniques: moves unlocked at class levels ----
        // (moveId, unlockLevel, basePP). Techniques draw from per-class PP
        // pools stored on the save, refilled at any heal (heal items, rest).
        public struct Technique
        {
            public string moveId;
            public int unlockLevel;
            public int basePP;
            public Technique(string id, int lvl, int pp) { moveId = id; unlockLevel = lvl; basePP = pp; }
        }

        public static readonly Technique[] Techniques =
        {
            // weapons
            new Technique("mt_rend",         5,  5),   // Claws
            new Technique("mt_inferno",     20, 4),   // Fangs
            new Technique("mt_tempest",     45, 3),   // Spines
            // armor
            new Technique("mt_bulwarkslam", 8,  5),   // Plates
            new Technique("mt_gracewind",   20, 5),   // Charms
            new Technique("mt_aegis",       40, 4),   // Plates capstone
        };

        /// <summary>Techniques unlocked for a class at its current level.</summary>
        public static List<Technique> Unlocked(WeaponClass wc, int classLevel)
        {
            var res = new List<Technique>();
            foreach (var t in Techniques)
            {
                if (ClassOfTechnique(t.moveId) != wc) continue;
                if (classLevel >= t.unlockLevel) res.Add(t);
            }
            return res;
        }

        public static WeaponClass ClassOfTechnique(string moveId)
        {
            switch (moveId)
            {
                case "mt_rend": return WeaponClass.Claws;
                case "mt_inferno": return WeaponClass.Fangs;
                case "mt_tempest": return WeaponClass.Spines;
                case "mt_bulwarkslam": return WeaponClass.Plates;
                case "mt_gracewind": return WeaponClass.Charms;
                case "mt_aegis": return WeaponClass.Plates;
                default: return WeaponClass.None;
            }
        }

        static readonly string[] Names = { "Claws", "Fangs", "Spines", "Plates", "Charms" };

        static readonly System.Collections.Generic.Dictionary<string, WeaponClass> ItemClass =
            new System.Collections.Generic.Dictionary<string, WeaponClass>
            {
                { "gear_claw", WeaponClass.Claws },
                { "gear_fang", WeaponClass.Fangs },
                { "gear_stormspine", WeaponClass.Spines },
                { "gear_band", WeaponClass.Plates },
                { "gear_hide", WeaponClass.Plates },
                { "gear_plate", WeaponClass.Plates },
                { "gear_charm", WeaponClass.Charms },
            };

        public static WeaponClass ClassOf(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return WeaponClass.None;
            WeaponClass wc;
            return ItemClass.TryGetValue(itemId, out wc) ? wc : WeaponClass.None;
        }

        public static string Name(WeaponClass wc)
        {
            int i = (int)wc;
            return i >= 0 && i < Names.Length ? Names[i] : "";
        }

        /// <summary>Total attack multiplier for a weapon class: 1 + 0.006 * level (max +60%).</summary>
        public static float AttackMultiplier(GameState save, WeaponClass wc)
        {
            if (save == null || (int)wc < 0) return 1f;
            int lvl = save.weaponLevels[(int)wc];
            return 1f + BonusPerLevel * lvl;
        }

        /// <summary>Damage-taken multiplier for an armor class: 1 − 0.005 * level (max −50%).</summary>
        public static float DefenseMultiplier(GameState save, WeaponClass wc)
        {
            if (save == null || (int)wc < 0) return 1f;
            int lvl = save.weaponLevels[(int)wc];
            return Mathf.Max(0.5f, 1f - DefReducePerLevel * lvl);
        }

        // ---------------- technique PP (per class, stored on the save) ----------------

        /// <summary>Current technique PP for a class (full by default).</summary>
        public static int TechPP(GameState save, WeaponClass wc, string moveId)
        {
            if (save == null || (int)wc < 0) return 0;
            if (save.techPP.TryGetValue(moveId, out int v)) return v;
            return BasePP(moveId);
        }

        public static void SpendTechPP(GameState save, string moveId)
        {
            if (save == null) return;
            save.techPP[moveId] = Mathf.Max(0, TechPP(save, ClassOfTechnique(moveId), moveId) - 1);
        }

        public static void RestoreTechPP(GameState save, string moveId)
        {
            if (save == null) return;
            save.techPP[moveId] = BasePP(moveId);
        }

        public static int BasePP(string moveId)
        {
            foreach (var t in Techniques)
                if (t.moveId == moveId) return t.basePP;
            return 3;
        }

        /// <summary>Percent reduction (0..50) an armor class grants, for UI.</summary>
        public static int DefenseReducePercent(GameState save, WeaponClass wc)
        {
            return Mathf.RoundToInt((1f - DefenseMultiplier(save, wc)) * 100f);
        }

        public static int Level(GameState save, WeaponClass wc)
        {
            return save == null || (int)wc < 0 ? 0 : save.weaponLevels[(int)wc];
        }

        public static int Xp(GameState save, WeaponClass wc)
        {
            return save == null || (int)wc < 0 ? 0 : save.weaponXp[(int)wc];
        }

        public static int XpToNext(int level)
        {
            if (level >= MaxLevel) return int.MaxValue;
            return 10 + level * 4;
        }

        /// <summary>Award KO experience to a weapon class; toasts on level-up.</summary>
        public static void AddXp(GameState save, WeaponClass wc, int xp)
        {
            if (save == null || (int)wc < 0 || xp <= 0) return;
            int lvl = save.weaponLevels[(int)wc];
            if (lvl >= MaxLevel) return;

            save.weaponXp[(int)wc] += xp;
            while (save.weaponLevels[(int)wc] < MaxLevel &&
                   save.weaponXp[(int)wc] >= XpToNext(save.weaponLevels[(int)wc]))
            {
                save.weaponXp[(int)wc] -= XpToNext(save.weaponLevels[(int)wc]);
                save.weaponLevels[(int)wc]++;
                Toast.Push("⚔ " + Name(wc) + " mastery is now level " + save.weaponLevels[(int)wc] + "!");
            }
        }
    }
}
