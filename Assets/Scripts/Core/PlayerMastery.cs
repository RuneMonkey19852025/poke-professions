using PokeProfessions.UI;

namespace PokeProfessions.Core
{
    /// <summary>
    /// Trainer skills, the player-side twin of creature mastery (WeaponMastery):
    /// the more the trainer fights with a KIND of weapon (club, blade, spear,
    /// axe), the stronger that kind gets, and every KO landed while wearing
    /// armour grows the armour skill. Each kind is its own skill — a lifetime
    /// with the club does nothing for the axe — and the numbers mirror the
    /// creature curve exactly: +0.6% damage per weapon level (max +60% at
    /// Lv100) and −0.5% damage taken per armour level (max −50%).
    /// </summary>
    public static class PlayerMastery
    {
        public const int MaxLevel = 100;
        public const float BonusPerLevel = 0.006f;     // +0.6% damage per level → +60% at Lv100
        public const int XpPerHit = 2;                 // weapon XP: a swing that lands
        public const int XpPerKO = 8;                  // weapon XP: a KO with the kind equipped
        public const float DefReducePerLevel = 0.005f; // −0.5% damage taken per level → −50% at Lv100
        public const int ArmorXpPerKO = 6;             // armour XP: a KO while wearing armour

        static readonly string[] KindNames = { "Club", "Blade", "Spear", "Axe" };

        public static string KindName(PlayerWeaponKind kind)
        {
            int i = (int)kind;
            return i >= 0 && i < KindNames.Length ? KindNames[i] : "";
        }

        // ---------------- weapon skills (per kind) ----------------

        public static int Level(GameState save, PlayerWeaponKind kind)
        {
            if (save == null || (int)kind < 0) return 0;
            return save.playerWeaponLevels[(int)kind];
        }

        public static int Xp(GameState save, PlayerWeaponKind kind)
        {
            if (save == null || (int)kind < 0) return 0;
            return save.playerWeaponXps[(int)kind];
        }

        public static int XpToNext(int level)
        {
            if (level >= MaxLevel) return int.MaxValue;
            return 10 + level * 4;
        }

        /// <summary>Damage multiplier for a weapon kind: 1 + 0.006 * level (max +60%).</summary>
        public static float AttackMultiplier(GameState save, PlayerWeaponKind kind)
        {
            if (save == null || (int)kind < 0) return 1f;
            return 1f + BonusPerLevel * save.playerWeaponLevels[(int)kind];
        }

        /// <summary>
        /// Award weapon-skill XP to a kind; bare hands teach nothing. Toasts on
        /// level-up, exactly as creature mastery does.
        /// </summary>
        public static void AddWeaponXp(GameState save, PlayerWeaponKind kind, int xp)
        {
            if (save == null || (int)kind < 0 || xp <= 0) return;
            if (save.playerWeaponLevels[(int)kind] >= MaxLevel) return;

            save.playerWeaponXps[(int)kind] += xp;
            while (save.playerWeaponLevels[(int)kind] < MaxLevel &&
                   save.playerWeaponXps[(int)kind] >= XpToNext(save.playerWeaponLevels[(int)kind]))
            {
                save.playerWeaponXps[(int)kind] -= XpToNext(save.playerWeaponLevels[(int)kind]);
                save.playerWeaponLevels[(int)kind]++;
                Toast.Push("⚔ " + KindName(kind) + " skill is now level " +
                           save.playerWeaponLevels[(int)kind] + "!");
            }
        }

        // ---------------- the armour skill ----------------
        // One body slot, one skill. Creature armour mastery grows per class on
        // victories; the trainer's grows on KOs landed while armour is worn —
        // bare-chested brawling teaches you nothing about wearing plate.

        public static int ArmorLevel(GameState save)
        {
            return save == null ? 0 : save.playerArmorLevel;
        }

        public static int ArmorXp(GameState save)
        {
            return save == null ? 0 : save.playerArmorXp;
        }

        /// <summary>Damage-taken multiplier for the armour skill: 1 − 0.005 * level (max −50%).</summary>
        public static float DefenseMultiplier(GameState save)
        {
            if (save == null) return 1f;
            return UnityEngine.Mathf.Max(0.5f, 1f - DefReducePerLevel * save.playerArmorLevel);
        }

        /// <summary>Percent reduction (0..50) the armour skill grants, for UI.</summary>
        public static int DefenseReducePercent(GameState save)
        {
            return UnityEngine.Mathf.RoundToInt((1f - DefenseMultiplier(save)) * 100f);
        }

        /// <summary>
        /// Armour-skill XP for knocking a wild creature out while wearing
        /// armour; a no-op bare-chested, so callers can award it unconditionally.
        /// </summary>
        public static void AddArmorXpForKO(GameState save)
        {
            if (save == null || string.IsNullOrEmpty(save.playerArmor)) return;
            if (save.playerArmorLevel >= MaxLevel) return;

            save.playerArmorXp += ArmorXpPerKO;
            while (save.playerArmorLevel < MaxLevel &&
                   save.playerArmorXp >= XpToNext(save.playerArmorLevel))
            {
                save.playerArmorXp -= XpToNext(save.playerArmorLevel);
                save.playerArmorLevel++;
                Toast.Push("🛡 Armour skill is now level " + save.playerArmorLevel + "!");
            }
        }

        // ---------------- UI helpers ----------------

        /// <summary>"Club Lv3 (12/22) +2%" for one kind, or "Club Lv0" when untrained.</summary>
        public static string KindLine(GameState save, PlayerWeaponKind kind)
        {
            int lvl = Level(save, kind);
            if (lvl <= 0) return KindName(kind) + " Lv0";
            string lv = lvl >= MaxLevel
                ? KindName(kind) + " Lv" + lvl + " MAX"
                : KindName(kind) + " Lv" + lvl + " (" + Xp(save, kind) + "/" + XpToNext(lvl) + ")";
            return lv + " +" + UnityEngine.Mathf.RoundToInt((AttackMultiplier(save, kind) - 1f) * 100f) + "%";
        }

        /// <summary>"Armour Lv2 (8/18) −1% taken", or the bare-chested nudge when nothing is worn.</summary>
        public static string ArmorLine(GameState save)
        {
            int lvl = ArmorLevel(save);
            string lv = lvl >= MaxLevel
                ? "Armour Lv" + lvl + " MAX"
                : "Armour Lv" + lvl + " (" + ArmorXp(save) + "/" + XpToNext(lvl) + ")";
            return lv + " −" + DefenseReducePercent(save) + "% taken";
        }

        /// <summary>Every trainer skill on one line, for the gear panel's detail box.</summary>
        public static string SummaryLine(GameState save)
        {
            var sb = new System.Text.StringBuilder();
            foreach (PlayerWeaponKind kind in System.Enum.GetValues(typeof(PlayerWeaponKind)))
            {
                if (kind == PlayerWeaponKind.None) continue;
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append(KindLine(save, kind));
            }
            sb.Append(" · ").Append(ArmorLine(save));
            return sb.ToString();
        }
    }
}
