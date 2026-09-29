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
        public int[] profLevels = new int[6];
        public int[] profXp = new int[6];

        public int ProfLevel(ProfessionId p) { return profLevels[(int)p]; }
        public int ProfXp(ProfessionId p) { return profXp[(int)p]; }
        public void SetProfLevel(ProfessionId p, int v) { profLevels[(int)p] = v; }
        public void SetProfXp(ProfessionId p, int v) { profXp[(int)p] = v; }
        public void AddProfXp(ProfessionId p, int xp) { profXp[(int)p] += xp; }

        // ---- inventory ----
        public List<ItemStack> inventory = new List<ItemStack>();
        public string equippedItem = "";

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
        public float playerX = 6f;
        public float playerZ = -14f;
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
                // rehydrate DB-backed lookups
                foreach (var c in s.party) if (c != null && c.speciesId != null && CreatureDB.Get(c.speciesId) == null) return null;
                foreach (var c in s.box) if (c != null && c.speciesId != null && CreatureDB.Get(c.speciesId) == null) return null;
                // rehydrate non-serialized state on every creature
                foreach (var c in s.party) if (c != null) { c.NormalizeMoves(); c.RestorePPAfterLoad(); }
                foreach (var c in s.box) if (c != null) { c.NormalizeMoves(); c.RestorePPAfterLoad(); }
                return s;
            }
            catch (Exception) { return null; }
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
        }

        static int CurrentMovePP(string moveId, Creature c)
        {
            var md = MoveDB.Get(moveId);
            return md != null ? md.pp : 10;
        }
    }
}
