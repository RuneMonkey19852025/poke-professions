using System.Collections.Generic;

namespace PokeProfessions.Core
{
    /// <summary>
    /// The gear quality ladder. One place that knows what each tier is called,
    /// what colour it wears in the UI, and how much damage it adds, so the
    /// Weaponsmith/Armourer recipes, the item table and every panel agree.
    ///
    /// Player weapon damage is NOT authored per item: it is the weapon kind's
    /// base (PlayerCombat.ClubDmg etc.) scaled by the tier, which is what makes
    /// "normal to legendary" a ladder rather than twenty unrelated numbers.
    /// </summary>
    public static class ItemRarity
    {
        public const int Count = 5;

        static readonly string[] Names = { "Normal", "Uncommon", "Rare", "Epic", "Legendary" };

        // grey → green → blue → purple → gold: the genre-standard read
        static readonly string[] Colors = { "#C8CDD4", "#6FCF97", "#56A8F5", "#B57BEE", "#F2A33C" };

        // damage multiplier on a weapon kind's base: 8 dmg club → 20 at legendary
        static readonly float[] WeaponMult = { 1.00f, 1.25f, 1.55f, 1.95f, 2.45f };

        public static string Name(Rarity r) { return Names[Index(r)]; }
        public static string ColorHex(Rarity r) { return Colors[Index(r)]; }
        public static float WeaponMultiplier(Rarity r) { return WeaponMult[Index(r)]; }

        static int Index(Rarity r)
        {
            int i = (int)r;
            return i < 0 ? 0 : (i >= Count ? Count - 1 : i);
        }

        /// <summary>\"Uncommon\" style label used in recipe and gear rows.</summary>
        public static string Tag(Rarity r) { return Name(r); }

        /// <summary>The item's name wrapped in its tier colour for rich-text UI.</summary>
        public static string RichName(string itemId)
        {
            var d = ItemDB.Get(itemId);
            if (d == null) return itemId;
            if (d.rarity == Rarity.Normal) return d.name; // don't shout about the baseline
            return "<color=" + ColorHex(d.rarity) + ">" + d.name + "</color>";
        }

        /// <summary>\"Blade · Rare\" — the one-line tier read for a gear row.</summary>
        public static string Line(string itemId)
        {
            var d = ItemDB.Get(itemId);
            if (d == null) return "";
            return d.name + "  <color=" + ColorHex(d.rarity) + ">[" + Name(d.rarity) + "]</color>";
        }
    }

    /// <summary>
    /// Who is where on the ladder. The Weaponsmith's recipes walk the four
    /// weapon kinds up the five tiers, the Armourer's walk the body armour
    /// slot up the same five — this is the single map both sides read, so a
    /// missing rung is a code change in one place rather than a silent gap.
    /// </summary>
    public static class PlayerGear
    {
        /// <summary>Every tier, weakest first.</summary>
        public static readonly Rarity[] Tiers =
        {
            Rarity.Normal, Rarity.Uncommon, Rarity.Rare, Rarity.Epic, Rarity.Legendary
        };

        /// <summary>The player's body armour, one per tier (weakest first).</summary>
        public static readonly string[] Armors =
        {
            "arm_padded",   // Normal
            "arm_boiled",   // Uncommon
            "arm_scale",    // Rare
            "arm_plate",    // Epic
            "arm_aegis"     // Legendary
        };

        /// <summary>Weapon kinds the Weaponsmith forges, in panel order.</summary>
        public static readonly PlayerWeaponKind[] Kinds =
        {
            PlayerWeaponKind.Blade, PlayerWeaponKind.Spear,
            PlayerWeaponKind.Axe, PlayerWeaponKind.Club
        };

        /// <summary>Every player weapon id, weakest tier first, kind by kind.</summary>
        public static string[] Weapons()
        {
            var list = new List<string>();
            foreach (var kind in Kinds)
                foreach (var t in Tiers)
                {
                    string id = WeaponFor(kind, t);
                    if (!string.IsNullOrEmpty(id)) list.Add(id);
                }
            return list.ToArray();
        }

        /// <summary>The player weapon a given kind and tier resolves to (\"\" if unauthored).</summary>
        public static string WeaponFor(PlayerWeaponKind kind, Rarity r)
        {
            BuildIndex();
            string id;
            return index != null && index.TryGetValue(Key(kind, r), out id) ? id : "";
        }

        /// <summary>The armour piece for a tier (\"\" if unauthored).</summary>
        public static string ArmorFor(Rarity r)
        {
            int i = (int)r;
            return i >= 0 && i < Armors.Length ? Armors[i] : "";
        }

        static Dictionary<string, string> index;

        static string Key(PlayerWeaponKind kind, Rarity r) { return (int)kind + ":" + (int)r; }

        /// <summary>
        /// Index the authored weapons by kind+tier. Reads ItemDB, so this must
        /// run after ItemDB.Init(); it is built lazily on first use.
        /// </summary>
        static void BuildIndex()
        {
            if (index != null || ItemDB.PlayerWeapons == null) return;
            index = new Dictionary<string, string>();
            foreach (var id in ItemDB.PlayerWeapons)
            {
                var d = ItemDB.Get(id);
                if (d == null || d.playerWeaponKind == PlayerWeaponKind.None) continue;
                index[Key(d.playerWeaponKind, d.rarity)] = id;
            }
        }

        /// <summary>Drop the cached index (ItemDB re-inits in a fresh session).</summary>
        public static void Reset() { index = null; }
    }
}
