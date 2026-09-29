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
        public const int MaxLevel = 10;
        public const float BonusPerLevel = 0.06f;   // +6% ATK per level (weapon classes)
        public const int XpPerKO = 8;               // weapon XP: KO with the class equipped
        public const float DefReducePerLevel = 0.05f; // −5% damage taken per level (armor classes)
        public const int ArmorXpPerWin = 6;         // armor XP: victory while wearing the class

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

        /// <summary>Total attack multiplier for a weapon class: 1 + 0.06 * level (max +60%).</summary>
        public static float AttackMultiplier(GameState save, WeaponClass wc)
        {
            if (save == null || (int)wc < 0) return 1f;
            int lvl = save.weaponLevels[(int)wc];
            return 1f + BonusPerLevel * lvl;
        }

        /// <summary>Damage-taken multiplier for an armor class: 1 − 0.05 * level (max −50%).</summary>
        public static float DefenseMultiplier(GameState save, WeaponClass wc)
        {
            if (save == null || (int)wc < 0) return 1f;
            int lvl = save.weaponLevels[(int)wc];
            return Mathf.Max(0.5f, 1f - DefReducePerLevel * lvl);
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
            return 12 + level * 10;
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
