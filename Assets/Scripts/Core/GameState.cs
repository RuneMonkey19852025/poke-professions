using System;
using System.Collections.Generic;
using UnityEngine;

namespace PokeProfessions.Core
{
    /// <summary>
    /// All persistent state, serializable to JSON for save/load.
    /// One instance lives on the GameBootstrap GameObject (DontDestroyOnLoad).
    /// </summary>
    [System.Serializable]
    public class GameState
    {
        // ---- identity ----
        public string playerName = "Robin";
        public int coins = 120;
        public int playSeconds;

        // ---- professions ----
        // every trade skill starts at level 1 (basics are common knowledge)
        public int[] profLevels = new int[6] { 1, 1, 1, 1, 1, 1 };
        public int[] profXp = new int[6];

        // ---- weapon mastery ----
        // grows as equipped creature gear lands KOs in battle; each level
        // strengthens that class of weapon (see WeaponMastery.AttackBonus).
        public int[] weaponLevels = new int[5];
        public int[] weaponXp = new int[5];

        // mastery technique PP, per class (not serialized — refills on load)
        [NonSerialized] public Dictionary<string, int> techPP = new Dictionary<string, int>();

        public int ProfLevel(ProfessionId p) { return profLevels[(int)p]; }
        public int ProfXp(ProfessionId p) { return profXp[(int)p]; }
        public void SetProfLevel(ProfessionId p, int v) { profLevels[(int)p] = v; }
        public void SetProfXp(ProfessionId p, int v) { profXp[(int)p] = v; }
        public void AddProfXp(ProfessionId p, int xp) { profXp[(int)p] += xp; }

        // ---- inventory ----
        public List<ItemStack> inventory = new List<ItemStack>();
        public string equippedItem = "";

        // ---- player melee combat (overworld) ----
        // The trainer themself can swing a weapon outside battles: Space or
        // left-click near a wild creature.
        public string playerWeapon = "";         // equipped weapon item id ("" = bare hands)

        // ---- trainer skills (PlayerMastery) ----
        // Levels the way creature mastery does: landed hits and KOs grow the
        // swung weapon's KIND (club/blade/spear/axe are separate skills), and
        // KOs landed while wearing armour grow the armour skill. +0.6% damage
        // per weapon level, −0.5% damage taken per armour level, Lv100 cap.
        public int[] playerWeaponLevels = new int[4];   // indexed by PlayerWeaponKind
        public int[] playerWeaponXps = new int[4];
        public int playerArmorLevel = 0;
        public int playerArmorXp = 0;

        // pre-per-kind save compat: one global weapon skill old saves carried;
        // folded into the equipped kind on load, then kept at zero.
        public int playerWeaponLevel = 0;
        public int playerWeaponXp = 0;

        // Worn body armour ("" = none). One piece at a time; its "def+N" is flat
        // damage taken off every wild hit and "hp+N" sets the health pool above
        // the base — see PlayerCombat.MaxHP/Mitigation.
        public string playerArmor = "";
        // Trainer health. The ceiling is derived (base + armour), never stored,
        // so equipping better armour can only ever raise it.
        public int playerHP = PlayerCombat.BaseHP;

        // ---- Lucky Snorlax jackpot ----
        // Shared pot across both machines: every losing spin feeds it, a
        // triple-star jackpot pays the whole pot and resets it.
        public int jackpotPot = 150;

        public int CountItem(string id)
        {
            foreach (var s in inventory) if (s.itemId == id) return s.count;
            return 0;
        }

        public void AddItem(string id, int n)
        {
            if (n <= 0) return;
            foreach (var s in inventory)
                if (s.itemId == id) { s.count += n; return; }
            inventory.Add(new ItemStack { itemId = id, count = n });
        }

        public bool RemoveItem(string id, int n)
        {
            if (CountItem(id) < n) return false;
            for (int i = 0; i < inventory.Count; i++)
            {
                if (inventory[i].itemId == id)
                {
                    inventory[i].count -= n;
                    if (inventory[i].count <= 0) inventory.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        // ---- party & box ----
        public List<Creature> party = new List<Creature>();
        public List<Creature> box = new List<Creature>();

        // ---- story ----
        public List<string> flags = new List<string>();
        public List<string> activeQuests = new List<string>();
        public List<string> doneQuests = new List<string>();
        public List<string> defeatedTrainers = new List<string>();

        public bool HasFlag(string f) { return !string.IsNullOrEmpty(f) && flags.Contains(f); }
        public void SetFlag(string f) { if (!string.IsNullOrEmpty(f) && !flags.Contains(f)) flags.Add(f); }

        // ---- journal / bestiary ----
        public List<string> seenSpecies = new List<string>();
        public List<string> caughtSpecies = new List<string>();
        public int catchCount, battleWins;

        // ---- world ----
        public float playerX = 0f;   // wake up on Emberpost's plaza
        public float playerZ = -16f;
        public string curZone = "meadow";
        public List<string> claimedNodes = new List<string>(); // kept for save compat

        // ================= persistence =================
        const string Key = "pokeprofessions_save_v1";

        public static bool SaveExists() { return PlayerPrefs.HasKey(Key); }

        public static GameState Load()
        {
            try
            {
                if (!PlayerPrefs.HasKey(Key)) return null;
                var json = PlayerPrefs.GetString(Key);
                var s = JsonUtility.FromJson<GameState>(json);
                if (s == null) return null;
                // trades start at 1: bump saves made before that rule
                if (s.profLevels != null)
                    for (int i = 0; i < s.profLevels.Length; i++)
                        if (s.profLevels[i] < 1) s.profLevels[i] = 1;
                // rehydrate DB-backed lookups
                foreach (var c in s.party) if (c != null && c.speciesId != null && CreatureDB.Get(c.speciesId) == null) return null;
                foreach (var c in s.box) if (c != null && c.speciesId != null && CreatureDB.Get(c.speciesId) == null) return null;
                // rehydrate non-serialized state on every creature
                foreach (var c in s.party) if (c != null) { c.NormalizeMoves(); c.RestorePPAfterLoad(); ValidateGear(c); }
                foreach (var c in s.box) if (c != null) { c.NormalizeMoves(); c.RestorePPAfterLoad(); ValidateGear(c); }
                if (!string.IsNullOrEmpty(s.playerWeapon) && ItemDB.Get(s.playerWeapon) == null) s.playerWeapon = "";
                if (!string.IsNullOrEmpty(s.playerArmor) && ItemDB.Get(s.playerArmor) == null) s.playerArmor = "";
                MigrateLegacyWeaponSkill(s);
                // saves written before the trainer had health (and any save made
                // while knocked out) wake up whole; health can never exceed the
                // ceiling its current armour allows.
                int maxHP = PlayerCombat.MaxHP(s);
                if (s.playerHP <= 0) s.playerHP = maxHP;
                if (s.playerHP > maxHP) s.playerHP = maxHP;
                return s;
            }
            catch (Exception) { return null; }
        }

        /// <summary>
        /// Saves made before trainer skills were per-kind carry one global
        /// weapon skill (max Lv50 at +1%/level = +50%). Fold it into the level
        /// of the kind that was equipped, doubled: the new curve is +0.6%/level
        /// with a Lv100 cap (+60%), so doubling lands every returning trainer
        /// at or above the power they had. The old fields zero out so the
        /// migration runs once, and whatever partial XP sat on the old curve is
        /// let go — it meant nothing on the new one.
        /// </summary>
        public static void MigrateLegacyWeaponSkill(GameState s)
        {
            if (s == null || s.playerWeaponLevel <= 0) return;
            var kind = PlayerCombat.KindOf(s.playerWeapon);
            if ((int)kind >= 0)
                s.playerWeaponLevels[(int)kind] = Mathf.Min(PlayerMastery.MaxLevel, s.playerWeaponLevel * 2);
            s.playerWeaponLevel = 0;
            s.playerWeaponXp = 0;
        }

        /// <summary>Drop equipped gear ids that no longer exist in the ItemDB (save compat).</summary>
        static void ValidateGear(Creature c)
        {
            if (!string.IsNullOrEmpty(c.weapon) && ItemDB.Get(c.weapon) == null) c.weapon = "";
            if (!string.IsNullOrEmpty(c.armor) && ItemDB.Get(c.armor) == null) c.armor = "";
        }

        public void Save()
        {
            try
            {
                // copy runtime PP dicts into their serializable mirrors
                foreach (var c in party) if (c != null) c.SyncPPForSave();
                foreach (var c in box) if (c != null) c.SyncPPForSave();
                PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
                PlayerPrefs.Save();
            }
            catch (Exception) { /* storage full or disabled: fail silently */ }
        }

        public static void DeleteSave() { PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save(); }

        // ================= helpers =================
        public Creature FirstHealthy()
        {
            foreach (var c in party) if (c != null && c.curHP > 0) return c;
            return null;
        }

        public bool PartyAlive()
        {
            return FirstHealthy() != null;
        }

        public void HealAll()
        {
            foreach (var c in party)
            {
                if (c == null) continue;
                c.curHP = c.MaxHP;
                c.status = "";
                c.statusTurns = 0;
                foreach (var m in c.moves) c.SetPP(m, CurrentMovePP(m, c));
            }
            // techniques are per-class resources: a full heal refills them all
            foreach (var t in WeaponMastery.Techniques)
                WeaponMastery.RestoreTechPP(this, t.moveId);
            // and a rest patches the trainer up too
            playerHP = PlayerCombat.MaxHP(this);
        }

        static int CurrentMovePP(string moveId, Creature c)
        {
            var md = MoveDB.Get(moveId);
            return md != null ? md.pp : 10;
        }
    }
}
