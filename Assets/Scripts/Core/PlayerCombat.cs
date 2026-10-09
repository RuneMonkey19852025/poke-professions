using UnityEngine;
using PokeProfessions.UI;

namespace PokeProfessions.Core
{
    /// <summary>
    /// Player melee combat (overworld): stats for the weapon the trainer
    /// carries, and the small progression model around it. Separate from
    /// creature battles — this is what happens when the trainer swings a
    /// club at a wild creature instead of sending one out.
    /// </summary>
    public static class PlayerCombat
    {
        // weapon stats: [club, blade, spear, axe]
        public const int ClubDmg = 8, BladeDmg = 14, SpearDmg = 11, AxeDmg = 20;
        public const float ClubCd = 0.9f, BladeCd = 0.65f, SpearCd = 0.75f, AxeCd = 1.3f;
        public const float SpearReach = 3.6f;   // other weapons use melee range
        public const float ClubSwing = 0.22f, BladeSwing = 0.18f, SpearSwing = 0.22f, AxeSwing = 0.3f;
        /// <summary>
        /// How long the trainer's flinch pose holds after a wild creature lands a
        /// bite. Short on purpose: it sits between two swings without eating the
        /// swing that follows, and a flinch you cannot swing out of reads as a stun.
        /// </summary>
        public const float FlinchSeconds = 0.45f;

        /// <summary>
        /// Base damage of a trainer weapon (0 when bare-handed): the kind's base
        /// scaled by the item's rarity tier, so the Normal → Legendary ladder is
        /// one multiplier rather than twenty hand-tuned numbers. A Legendary club
        /// (8 × 2.45 = 20) lands exactly like a Normal axe, which is the point of
        /// having kinds and tiers as separate axes.
        /// </summary>
        public static int BaseDamage(string weaponId)
        {
            int baseDmg;
            switch (KindOf(weaponId))
            {
                case PlayerWeaponKind.Club: baseDmg = ClubDmg; break;
                case PlayerWeaponKind.Blade: baseDmg = BladeDmg; break;
                case PlayerWeaponKind.Spear: baseDmg = SpearDmg; break;
                case PlayerWeaponKind.Axe: baseDmg = AxeDmg; break;
                default: return 0;
            }
            return Mathf.RoundToInt(baseDmg * RarityMultiplier(weaponId));
        }

        /// <summary>The tier multiplier of an equipped weapon (1 when bare or unknown).</summary>
        public static float RarityMultiplier(string weaponId)
        {
            var d = ItemDB.Get(weaponId);
            return d == null ? 1f : ItemRarity.WeaponMultiplier(d.rarity);
        }

        /// <summary>Attack cooldown in seconds (bare hands swing as fast as a club).</summary>
        public static float Cooldown(string weaponId)
        {
            switch (KindOf(weaponId))
            {
                case PlayerWeaponKind.Blade: return BladeCd;
                case PlayerWeaponKind.Spear: return SpearCd;
                case PlayerWeaponKind.Axe: return AxeCd;
                default: return ClubCd;
            }
        }

        /// <summary>Swing animation duration in seconds.</summary>
        public static float SwingSeconds(string weaponId)
        {
            switch (KindOf(weaponId))
            {
                case PlayerWeaponKind.Blade: return BladeSwing;
                case PlayerWeaponKind.Spear: return SpearSwing;
                case PlayerWeaponKind.Axe: return AxeSwing;
                default: return ClubSwing;
            }
        }

        /// <summary>Attack reach in world units (spears poke from further out).</summary>
        public static float Reach(string weaponId)
        {
            return KindOf(weaponId) == PlayerWeaponKind.Spear ? SpearReach : 2.6f;
        }

        public static PlayerWeaponKind KindOf(string weaponId)
        {
            var d = ItemDB.Get(weaponId);
            return d != null ? d.playerWeaponKind : PlayerWeaponKind.None;
        }

        /// <summary>Display name, falling back to "Bare Hands".</summary>
        public static string NameOf(string weaponId)
        {
            return string.IsNullOrEmpty(weaponId) ? "Bare Hands" : ItemDB.Name(weaponId);
        }

        /// <summary>Actual damage dealt: weapon base, the kind's skill, small variance.</summary>
        public static int DamageRoll(GameState save)
        {
            string w = save != null ? save.playerWeapon : "";
            int dmg = BaseDamage(w);
            if (save != null && dmg > 0)
                dmg = Mathf.RoundToInt(dmg * PlayerMastery.AttackMultiplier(save, KindOf(w)));
            return Mathf.Max(1, Random.Range(Mathf.Max(1, dmg - 1), dmg + 2));
        }

        /// <summary>Seconds between swings for the equipped weapon.</summary>
        public static float CooldownOf(GameState save)
        {
            return Cooldown(save != null ? save.playerWeapon : "");
        }

        public static float SwingSecondsOf(GameState save)
        {
            return SwingSeconds(save != null ? save.playerWeapon : "");
        }

        public static float ReachOf(GameState save)
        {
            return Reach(save != null ? save.playerWeapon : "");
        }

        // ---------------- the trainer's own health ----------------
        // Wild creatures hit back (see MeleeTarget), so the trainer carries a
        // health pool of their own. Armour is what makes that pool matter: the
        // worn piece's "def+N" comes straight off every hit and "hp+N" widens the
        // pool, so the armour ladder and the health pool scale together. Health
        // is not a stat you raise otherwise — it is what you buy with armour.

        public const int BaseHP = 60;
        /// <summary>Seconds unhurt before the trainer starts mending.</summary>
        public const float RegenDelay = 6f;
        /// <summary>Health regained per second once regen has kicked in.</summary>
        public const float RegenPerSecond = 1.5f;

        /// <summary>The trainer's maximum health: base plus armour "hp+N".</summary>
        public static int MaxHP(GameState save)
        {
            return BaseHP + ArmorBonus(save, "hp+");
        }

        /// <summary>Flat damage taken off every incoming hit, from armour "def+N".</summary>
        public static int Mitigation(GameState save)
        {
            return ArmorBonus(save, "def+");
        }

        /// <summary>
        /// What a wild creature's raw hit actually does to the trainer: the worn
        /// armour's flat soak comes off first, then the armour skill trims a
        /// percentage of what is left (−0.5%/level, mirroring creature armor
        /// mastery). Always ≥1.
        /// </summary>
        public static int DamageTaken(GameState save, int raw)
        {
            int afterSoak = Mathf.Max(1, raw - Mitigation(save));
            return Mathf.Max(1, Mathf.RoundToInt(afterSoak * PlayerMastery.DefenseMultiplier(save)));
        }

        /// <summary>Sum one "prefix+N" segment off the worn armour piece.</summary>
        static int ArmorBonus(GameState save, string prefix)
        {
            if (save == null) return 0;
            var d = ItemDB.Get(save.playerArmor);
            if (d == null || string.IsNullOrEmpty(d.equipBonus)) return 0;
            int sum = 0;
            foreach (var seg in d.equipBonus.Split('|'))
                if (seg.StartsWith(prefix))
                {
                    int v;
                    if (int.TryParse(seg.Substring(prefix.Length), out v)) sum += v;
                }
            return sum;
        }

        /// <summary>Health as "42/60" for the HUD and gear panel.</summary>
        public static string HealthLabel(GameState save)
        {
            if (save == null) return "";
            return Mathf.Max(0, save.playerHP) + "/" + MaxHP(save);
        }

        // ---------------- mastery ----------------
        // Trainer skills live in PlayerMastery (the player-side twin of
        // WeaponMastery): one skill per weapon kind plus the armour skill.

        /// <summary>One-line summary for UI (HUD, weapon panel).</summary>
        public static string MasteryLabel(GameState save)
        {
            if (save == null) return "";
            var kind = KindOf(save.playerWeapon);
            if (kind == PlayerWeaponKind.None) return "Bare Hands";
            int lvl = PlayerMastery.Level(save, kind);
            if (lvl <= 0) return NameOf(save.playerWeapon);
            return NameOf(save.playerWeapon) + "  Lv" + lvl +
                   "  (" + PlayerMastery.KindName(kind) + " skill +" +
                   Mathf.RoundToInt((PlayerMastery.AttackMultiplier(save, kind) - 1f) * 100f) + "% damage)";
        }
    }
}
