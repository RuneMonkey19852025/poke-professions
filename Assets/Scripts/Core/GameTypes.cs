using System;
using System.Collections.Generic;

namespace PokeProfessions.Core
{
    public enum Element
    {
        Normal, Ember, Tide, Verdant, Volt, Stone, Gale, Frost, Toxic, Mind, Shade, Beast
    }

    public enum ProfessionId
    {
        Forager,   // gathering plants, berries, herbs
        Miner,     // mining ore nodes, stone
        Fisher,    // fishing spots
        Chef,      // cooking food, battle snacks
        Smith,     // smelting ore, forging capture cores and gear
        Tailor     // weaving fibers into apparel, ropes, packs
    }

    public enum ItemKind
    {
        Material,
        Consumable,
        CaptureCore,
        KeyItem,
        Food,
        Equipment
    }

    [Serializable]
    public class ItemDef
    {
        public string id;
        public string name;
        public string desc;
        public ItemKind kind;
        public int basePrice;          // shop sell/buy baseline
        public bool sellable = true;
        public string professionId;    // which profession yields/crafts it ("" for none)
        public string iconColorHex = "#B9C4CC"; // UI swatch
        public int healAmount;         // for consumables/food: creature HP restored
        public int conditionCure;      // 0 none, 1 cures status
        public int catchBonus;         // for CaptureCore items
        public string equipBonus;      // e.g. "gather+1", "catch+1"
    }

    [Serializable]
    public class ItemStack
    {
        public string itemId;
        public int count;
    }

    [Serializable]
    public class Recipe
    {
        public string id;
        public string resultItemId;
        public int resultCount;
        public ProfessionId profession;
        public int requiredLevel;
        public int xpReward;
        public List<ItemStack> inputs = new List<ItemStack>();
        public string station; // "campfire", "forge", "workbench", "" = anywhere
    }

    [Serializable]
    public class MoveDef
    {
        public string id;
        public string name;
        public Element element;
        public int power;
        public int accuracy;   // 0 = never misses
        public int pp;
        public string desc;

        public string Effect;  // "", "buff_atk", "debuff_def", "heal", "drain", "status"
        public string statusId; // applied status, if any
        public int statusChance; // percent
    }

    [Serializable]
    public class SpeciesDef
    {
        public string id;
        public string name;
        public Element element;
        public Element element2; // second type or first
        public int baseHP, baseATK, baseDEF, baseSPD;
        public string desc;
        public string evolvesInto;   // "" if none
        public int evolveLevel;
        public List<string> learnset = new List<string>(); // move ids by level order (level = 1+index)
        public string paletteHex;  // primary body color
        public string accentHex;   // secondary/accent color
        public string shape;       // procedural body shape: "quad","serpent","avian","aquatic","rock","spirit","bug"
        public float scale = 1f;
        public int catchRate = 45;    // higher = easier
        public int xpYield = 24;
        public string habitatZone;   // where wild ones spawn
    }

    [Serializable]
    public class MovePPEntry
    {
        public string id;
        public int pp;
    }

    [Serializable]
    public class Creature
    {
        public string speciesId;
        public string nickname;
        public int level = 1;
        public int xp;
        public int curHP;
        public List<string> moves = new List<string>();
        public string weapon = "";   // equipped weapon item id ("" = none)
        public string armor = "";    // equipped armor item id ("" = none)
        public Dictionary<string, int> movePP = new Dictionary<string, int>();
        public List<MovePPEntry> movePPList = new List<MovePPEntry>(); // serialized mirror (JsonUtility can't do dicts)
        public string status = "";   // "", "poison", "burn", "sleep", "paralyze"
        public int statusTurns;
        public bool shiny;
        public string uid = Guid.NewGuid().ToString("N").Substring(0, 10);

        public SpeciesDef Species => CreatureDB.Get(speciesId);

        public int MaxHP
        {
            get
            {
                var s = Species;
                return s == null ? 20 : s.baseHP * 2 + level * 4 + 12 + GearHp();
            }
        }

        public int ATK
        {
            get
            {
                var s = Species;
                int v = (s == null ? 8 : s.baseATK) * 2 + level * 2;
                v += GearAtk();
                return MathfStatus(v, status == "burn");
            }
        }

        public int DEF
        {
            get
            {
                var s = Species;
                int v = (s == null ? 8 : s.baseDEF) * 2 + level * 2;
                return v + GearDef();
            }
        }

        public int SPD
        {
            get
            {
                var s = Species;
                int v = (s == null ? 8 : s.baseSPD) * 2 + level;
                return v + GearSpd();
            }
        }

        // ---------------- creature equipment ----------------
        // Weapons add ATK (and sometimes SPD), armor adds DEF (and sometimes HP).
        // Bonuses are flat and small: a lv5 starter gains ~+3-6 from early gear,
        // meaningful without eclipsing level-ups.

        public int GearAtk() { return SumGear(weapon, "atk+") + SumGear(armor, "atk+"); }
        public int GearDef() { return SumGear(weapon, "def+") + SumGear(armor, "def+"); }
        public int GearSpd() { return SumGear(weapon, "spd+") + SumGear(armor, "spd+"); }
        public int GearHp() { return SumGear(weapon, "hp+") + SumGear(armor, "hp+"); }

        /// <summary>Sum every "prefix+N" segment (e.g. "atk+3|spd+1") on one equipped item.</summary>
        static int SumGear(string itemId, string prefix)
        {
            if (string.IsNullOrEmpty(itemId)) return 0;
            var d = ItemDB.Get(itemId);
            if (d == null || string.IsNullOrEmpty(d.equipBonus)) return 0;
            int sum = 0;
            foreach (var seg in d.equipBonus.Split('|'))
            {
                if (seg.StartsWith(prefix))
                {
                    int v;
                    if (int.TryParse(seg.Substring(prefix.Length), out v)) sum += v;
                }
            }
            return sum;
        }

        private static int MathfStatus(int v, bool halved) => halved ? v / 2 : v;

        public string DisplayName => string.IsNullOrEmpty(nickname) ? (Species != null ? Species.name : "???") : nickname;

        public bool KnowsMove(string moveId) => moves != null && moves.Contains(moveId);

        /// <summary>PP for a move; missing entries count as full (never-zero by default).</summary>
        public int CurrentPP(string moveId)
        {
            int pp;
            if (movePP.TryGetValue(moveId, out pp)) return pp;
            var md = MoveDB.Get(moveId);
            return md != null ? md.pp : 10;
        }

        public void SetPP(string moveId, int pp) { movePP[moveId] = pp; }

        /// <summary>Copy the runtime PP dict into the serializable list (call before saving).</summary>
        public void SyncPPForSave()
        {
            movePPList.Clear();
            foreach (var kv in movePP)
                movePPList.Add(new MovePPEntry { id = kv.Key, pp = kv.Value });
        }

        /// <summary>Rebuild the PP dict from the saved list; missing moves get full PP.</summary>
        public void RestorePPAfterLoad()
        {
            movePP.Clear();
            if (movePPList != null)
                foreach (var e in movePPList)
                    if (e != null && !string.IsNullOrEmpty(e.id) && !movePP.ContainsKey(e.id))
                        movePP[e.id] = e.pp;
            if (moves != null)
                foreach (var m in moves)
                    if (!movePP.ContainsKey(m))
                    {
                        var md = MoveDB.Get(m);
                        movePP[m] = md != null ? md.pp : 10;
                    }
        }

        /// <summary>Remove duplicate moves (heals old saves poisoned by the learnset bug).</summary>
        public void NormalizeMoves()
        {
            if (moves == null) return;
            var clean = new List<string>();
            foreach (var m in moves)
                if (!string.IsNullOrEmpty(m) && !clean.Contains(m)) clean.Add(m);
            if (clean.Count == 0) clean.Add("tackle");
            moves = clean;
        }

        public void RecalcAfterLevel()
        {
            curHP = Math.Min(curHP, MaxHP);
        }
    }

    public enum StatusKind { None, Poison, Burn, Sleep, Paralyze }

    /// <summary>Weapon classes for mastery. None covers bare attacks (no XP).</summary>
    public enum WeaponClass
    {
        None = -1,
        Claws = 0,   // gear_claw
        Fangs = 1,   // gear_fang
        Spines = 2,  // gear_stormspine
        Plates = 3,  // gear_band, gear_hide, gear_plate
        Charms = 4   // gear_charm
    }
}
