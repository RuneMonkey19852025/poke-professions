using System.Collections.Generic;
using UnityEngine;

namespace PokeProfessions.Core
{
    /// <summary>Runtime lookup over all static defs, initialized once at boot.</summary>
    public static class ItemDB
    {
        public static Dictionary<string, ItemDef> All;
        public static List<Recipe> Recipes;

        public static void Init()
        {
            All = new Dictionary<string, ItemDef>();
            Recipes = new List<Recipe>();

            // ---------------- Materials ----------------
            Add(new ItemDef { id = "fiber", name = "Sunfiber", desc = "Tough golden stalks. Weaves into rope and cloth.", kind = ItemKind.Material, basePrice = 4, professionId = "Forager", iconColorHex = "#C9B458" });
            Add(new ItemDef { id = "bitterleaf", name = "Bitterleaf", desc = "Pungent herb. Cooks into medicine.", kind = ItemKind.Material, basePrice = 5, professionId = "Forager", iconColorHex = "#7C9A4E" });
            Add(new ItemDef { id = "glowcap", name = "Glowcap", desc = "A mushroom that hums with soft light.", kind = ItemKind.Material, basePrice = 7, professionId = "Forager", iconColorHex = "#BEE39B" });
            Add(new ItemDef { id = "emberpepper", name = "Emberpepper", desc = "Warm to the touch. Loved by Ember creatures.", kind = ItemKind.Material, basePrice = 6, professionId = "Forager", iconColorHex = "#D9543F" });
            Add(new ItemDef { id = "stone", name = "Fieldstone", desc = "Plain, honest rock.", kind = ItemKind.Material, basePrice = 2, professionId = "Miner", iconColorHex = "#8D9096" });
            Add(new ItemDef { id = "copper_ore", name = "Copper Ore", desc = "Rusty veins of workable metal.", kind = ItemKind.Material, basePrice = 8, professionId = "Miner", iconColorHex = "#C77B46" });
            Add(new ItemDef { id = "iron_ore", name = "Iron Ore", desc = "Heavy and dependable.", kind = ItemKind.Material, basePrice = 12, professionId = "Miner", iconColorHex = "#9AA3AB" });
            Add(new ItemDef { id = "moonshard", name = "Moonshard", desc = "Chipped from a Sleepers' monolith. Cold, faintly humming.", kind = ItemKind.Material, basePrice = 30, professionId = "Miner", iconColorHex = "#B7C4E8", sellable = false });
            Add(new ItemDef { id = "copper_bar", name = "Copper Bar", desc = "Smelted and ready for the forge.", kind = ItemKind.Material, basePrice = 18, professionId = "Smith", iconColorHex = "#E08D57" });
            Add(new ItemDef { id = "iron_bar", name = "Iron Bar", desc = "The backbone of good tools.", kind = ItemKind.Material, basePrice = 26, professionId = "Smith", iconColorHex = "#B8C0C8" });
            Add(new ItemDef { id = "rope", name = "Braided Rope", desc = "Three strands of Sunfiber, knotted by hand.", kind = ItemKind.Material, basePrice = 10, professionId = "Tailor", iconColorHex = "#D8C088" });
            Add(new ItemDef { id = "cloth", name = "Woven Cloth", desc = "Soft, sturdy fabric.", kind = ItemKind.Material, basePrice = 12, professionId = "Tailor", iconColorHex = "#C9CDD4" });
            Add(new ItemDef { id = "raw_fish", name = "Silverfin", desc = "A common river fish. Tastes better cooked.", kind = ItemKind.Material, basePrice = 6, professionId = "Fisher", iconColorHex = "#9FB8CC" });
            Add(new ItemDef { id = "gildfish", name = "Gildfish", desc = "A fat golden fish. Rare catch.", kind = ItemKind.Material, basePrice = 20, professionId = "Fisher", iconColorHex = "#E8C35A" });

            // ---------------- Food / Consumables ----------------
            Add(new ItemDef { id = "trail_snack", name = "Trail Snack", desc = "Restores 25 HP to one creature.", kind = ItemKind.Food, basePrice = 14, healAmount = 25, iconColorHex = "#D9A05B" });
            Add(new ItemDef { id = "hearty_stew", name = "Hearty Stew", desc = "Restores 70 HP to one creature.", kind = ItemKind.Food, basePrice = 40, healAmount = 70, iconColorHex = "#B3663B" });
            Add(new ItemDef { id = "spice_bun", name = "Spice Bun", desc = "Restores 45 HP and cures status.", kind = ItemKind.Food, basePrice = 30, healAmount = 45, conditionCure = 1, iconColorHex = "#E0B14C" });
            Add(new ItemDef { id = "salve", name = "Green Salve", desc = "Restores 40 HP. Bitterleaf medicine.", kind = ItemKind.Consumable, basePrice = 26, healAmount = 40, iconColorHex = "#6FA86F" });
            Add(new ItemDef { id = "purge_draught", name = "Purge Draught", desc = "Cures any status condition.", kind = ItemKind.Consumable, basePrice = 24, conditionCure = 1, iconColorHex = "#8FD0C8" });
            Add(new ItemDef { id = "revive_tonic", name = "Revive Tonic", desc = "Revives a fainted creature to half HP.", kind = ItemKind.Consumable, basePrice = 90, healAmount = 999, iconColorHex = "#E8E3B0" });

            // ---------------- Capture cores ----------------
            Add(new ItemDef { id = "core_wood", name = "Timber Core", desc = "A carved capture core. Catch bonus x1.", kind = ItemKind.CaptureCore, basePrice = 15, catchBonus = 1, iconColorHex = "#A0784C" });
            Add(new ItemDef { id = "core_iron", name = "Ironbound Core", desc = "Reinforced capture core. Catch bonus x2.", kind = ItemKind.CaptureCore, basePrice = 45, catchBonus = 2, iconColorHex = "#8E99A3" });
            Add(new ItemDef { id = "core_moon", name = "Moonlit Core", desc = "Inlaid with Moonshard. Catch bonus x3.5.", kind = ItemKind.CaptureCore, basePrice = 120, catchBonus = 3, iconColorHex = "#A9BCE8" });

            // ---------------- Equipment ----------------
            Add(new ItemDef { id = "forager_gloves", name = "Forager Gloves", desc = "Equipped: gathering yields +1.", kind = ItemKind.Equipment, basePrice = 60, equipBonus = "gather+1", iconColorHex = "#B08968", sellable = false });
            Add(new ItemDef { id = "miner_pick", name = "Reinforced Pick", desc = "Equipped: mining yields +1.", kind = ItemKind.Equipment, basePrice = 60, equipBonus = "mine+1", iconColorHex = "#7F8C99", sellable = false });
            Add(new ItemDef { id = "angler_rod", name = "Warded Rod", desc = "Equipped: fishing yields +1.", kind = ItemKind.Equipment, basePrice = 60, equipBonus = "fish+1", iconColorHex = "#6C8EA4", sellable = false });
            Add(new ItemDef { id = "lure_charm", name = "Lure Charm", desc = "Equipped: wild encounters are 25% more frequent.", kind = ItemKind.Equipment, basePrice = 80, equipBonus = "lure", iconColorHex = "#D4A5C9", sellable = false });
            Add(new ItemDef { id = "scout_band", name = "Scout Band", desc = "Equipped: wild creatures appear 2 levels weaker.", kind = ItemKind.Equipment, basePrice = 100, equipBonus = "scout", iconColorHex = "#98B4D4", sellable = false });

            // ---------------- Trainer melee weapons (overworld) ----------------
            // A five-rung ladder per weapon kind, Normal → Legendary. The item
            // carries only its kind and its tier: damage is the kind's base
            // (PlayerCombat.ClubDmg … AxeDmg) times ItemRarity.WeaponMultiplier,
            // so the numbers live in one place and the ladder cannot drift out
            // of order. Descriptions stay qualitative for the same reason.
            // Blade — 14 base, the quickest heavy hit.
            W("wpn_blade", "Copper Blade", PlayerWeaponKind.Blade, Rarity.Normal, 90, "#E08D57",
              "Trainer weapon: a keen medium swing, Smith-forged.");
            W("wpn_blade_uncommon", "Honed Blade", PlayerWeaponKind.Blade, Rarity.Uncommon, 210, "#D8A46A",
              "Trainer weapon: a honed edge that bites deeper than copper.");
            W("wpn_blade_rare", "Keenblade", PlayerWeaponKind.Blade, Rarity.Rare, 430, "#8CC8E8",
              "Trainer weapon: folded steel, thin as a promise.");
            W("wpn_blade_epic", "Stormcleaver", PlayerWeaponKind.Blade, Rarity.Epic, 820, "#A88CE8",
              "Trainer weapon: the ridge's lightning, coaxed into an edge.");
            W("wpn_blade_legendary", "Emberwake", PlayerWeaponKind.Blade, Rarity.Legendary, 1500, "#F2A33C",
              "Trainer weapon: a blade that remembers the fire it was born in.");

            // Spear — 11 base, the longest reach.
            W("wpn_spear", "Longspear", PlayerWeaponKind.Spear, Rarity.Normal, 65, "#A8B0B8",
              "Trainer weapon: long reach. Keeps the fangs at arm's length.");
            W("wpn_spear_uncommon", "Barbed Pike", PlayerWeaponKind.Spear, Rarity.Uncommon, 170, "#9CB4B0",
              "Trainer weapon: barbed, so nothing shrugs it off.");
            W("wpn_spear_rare", "Ridge Lance", PlayerWeaponKind.Spear, Rarity.Rare, 360, "#7FB8D8",
              "Trainer weapon: a rider's lance re-cut for the ridge.");
            W("wpn_spear_epic", "Skyfang Lance", PlayerWeaponKind.Spear, Rarity.Epic, 700, "#B09CE8",
              "Trainer weapon: long enough to argue with something twice your size.");
            W("wpn_spear_legendary", "Dawnpiercer", PlayerWeaponKind.Spear, Rarity.Legendary, 1300, "#F2C33C",
              "Trainer weapon: it goes first, and it goes furthest.");

            // Axe — 20 base, the heaviest hit and the slowest swing.
            W("wpn_axe", "Vale Axe", PlayerWeaponKind.Axe, Rarity.Normal, 150, "#B08968",
              "Trainer weapon: heavy. Splits logs; makes do with hide.");
            W("wpn_axe_uncommon", "Ironbark Axe", PlayerWeaponKind.Axe, Rarity.Uncommon, 300, "#A08A62",
              "Trainer weapon: an ironwood haft that will not jar your wrists.");
            W("wpn_axe_rare", "Rift Splitter", PlayerWeaponKind.Axe, Rarity.Rare, 560, "#88A8C8",
              "Trainer weapon: quarried from the ridge's own split stone.");
            W("wpn_axe_epic", "Thundercleave", PlayerWeaponKind.Axe, Rarity.Epic, 1000, "#A87CE8",
              "Trainer weapon: swings like weather coming down a valley.");
            W("wpn_axe_legendary", "Valesplinter", PlayerWeaponKind.Axe, Rarity.Legendary, 1800, "#F2A33C",
              "Trainer weapon: the axe the Vale tells stories about.");

            // Club — 8 base, the cheap honest starter.
            W("wpn_club", "Oaken Club", PlayerWeaponKind.Club, Rarity.Normal, 30, "#8A5E33",
              "Trainer weapon: an honest branch with intent.");
            W("wpn_club_uncommon", "Ironwood Club", PlayerWeaponKind.Club, Rarity.Uncommon, 90, "#7E8F5A",
              "Trainer weapon: ironwood — dull, dense, dependable.");
            W("wpn_club_rare", "Emberwillow Cudgel", PlayerWeaponKind.Club, Rarity.Rare, 200, "#93B0D8",
              "Trainer weapon: warm to the grip, even in the rain.");
            W("wpn_club_epic", "Stoneheart Maul", PlayerWeaponKind.Club, Rarity.Epic, 420, "#B79CE8",
              "Trainer weapon: a monolith shard on a rope-wrapped haft.");
            W("wpn_club_legendary", "Heartwood Worldbreaker", PlayerWeaponKind.Club, Rarity.Legendary, 800, "#F2B33C",
              "Trainer weapon: cut from the tree the Hollow grew around.");

            // ---------------- Player armour (body slot) ----------------
            // Worn by the trainer, not a creature. "def+N" is flat damage
            // taken off every hit a wild creature lands (PlayerCombat.Mitigate),
            // "hp+N" raises the trainer's health pool. One piece per tier.
            A("arm_padded", "Padded Jerkin", Rarity.Normal, 60, "def+2|hp+8", "#B08968",
              "Armour: quilted cloth. Turns a claw into a bruise.");
            A("arm_boiled", "Boiled Leather Vest", Rarity.Uncommon, 180, "def+4|hp+16", "#8A9A5B",
              "Armour: hardened hide, waxed until it rings.");
            A("arm_scale", "Mailscale Hauberk", Rarity.Rare, 380, "def+7|hp+26", "#7FA8D8",
              "Armour: overlapping scales, each one riveted by hand.");
            A("arm_plate", "Embersteel Plate", Rarity.Epic, 720, "def+11|hp+38", "#A08CE8",
              "Armour: forge-quenched plate with a ridge-forged temper.");
            A("arm_aegis", "Verdant Aegis", Rarity.Legendary, 1400, "def+16|hp+52", "#F2A33C",
              "Armour: living bark over steel. The Vale's own answer to claws.");

            // ---------------- Creature gear (weapons & armor) ----------------
            // equipBonus segments: atk+ / def+ / spd+ / hp+; primary segment first.
            Add(new ItemDef { id = "gear_claw", name = "Bone Claw", desc = "Creature weapon: +3 ATK.", kind = ItemKind.Equipment, basePrice = 35, equipBonus = "atk+3", iconColorHex = "#E4DCC8", sellable = true });
            Add(new ItemDef { id = "gear_fang", name = "Ember Fang", desc = "Creature weapon: +6 ATK, +1 SPD. Warm to the tooth.", kind = ItemKind.Equipment, basePrice = 75, equipBonus = "atk+6|spd+1", iconColorHex = "#E07040", sellable = true });
            Add(new ItemDef { id = "gear_stormspine", name = "Storm Spine", desc = "Creature weapon: +9 ATK, +2 SPD. Forged around a charged filament.", kind = ItemKind.Equipment, basePrice = 150, equipBonus = "atk+9|spd+2", iconColorHex = "#B8D8F0", sellable = true });
            Add(new ItemDef { id = "gear_band", name = "Traveler's Band", desc = "Creature armor: +2 DEF, +4 HP.", kind = ItemKind.Equipment, basePrice = 30, equipBonus = "def+2|hp+4", iconColorHex = "#C8A878", sellable = true });
            Add(new ItemDef { id = "gear_hide", name = "Ranger Hide", desc = "Creature armor: +5 DEF, +8 HP.", kind = ItemKind.Equipment, basePrice = 70, equipBonus = "def+5|hp+8", iconColorHex = "#8A9A5B", sellable = true });
            Add(new ItemDef { id = "gear_plate", name = "Vale Plate", desc = "Creature armor: +9 DEF, +14 HP. Smith-forged shell.", kind = ItemKind.Equipment, basePrice = 140, equipBonus = "def+9|hp+14", iconColorHex = "#A8B0B8", sellable = true });
            Add(new ItemDef { id = "gear_charm", name = "Zephyr Charm", desc = "Creature armor: +4 SPD. Light as a rumor.", kind = ItemKind.Equipment, basePrice = 90, equipBonus = "spd+4", iconColorHex = "#D8E8F8", sellable = true });

            // ---------------- Key items ----------------
            Add(new ItemDef { id = "lantern", name = "Sleeper's Lantern", desc = "Its flame leans toward the Hollow. Story item.", kind = ItemKind.KeyItem, basePrice = 0, sellable = false, iconColorHex = "#F5E6A8" });
            Add(new ItemDef { id = "sigil_piece_a", name = "Sigil Fragment: Root", desc = "One third of the Verdant Sigil.", kind = ItemKind.KeyItem, basePrice = 0, sellable = false, iconColorHex = "#78C850" });
            Add(new ItemDef { id = "sigil_piece_b", name = "Sigil Fragment: Tide", desc = "One third of the Verdant Sigil.", kind = ItemKind.KeyItem, basePrice = 0, sellable = false, iconColorHex = "#6890F0" });
            Add(new ItemDef { id = "sigil_piece_c", name = "Sigil Fragment: Ember", desc = "One third of the Verdant Sigil.", kind = ItemKind.KeyItem, basePrice = 0, sellable = false, iconColorHex = "#F08030" });
            Add(new ItemDef { id = "verdant_sigil", name = "Verdant Sigil", desc = "Complete. It answers the Hollow Sleepers.", kind = ItemKind.KeyItem, basePrice = 0, sellable = false, iconColorHex = "#9BE870" });

            // ---------------- Recipes ----------------
            Recipes.Add(new Recipe { id = "r_rope", resultItemId = "rope", resultCount = 1, profession = ProfessionId.Tailor, requiredLevel = 1, xpReward = 6, station = "", inputs = { new ItemStack { itemId = "fiber", count = 3 } } });
            Recipes.Add(new Recipe { id = "r_cloth", resultItemId = "cloth", resultCount = 1, profession = ProfessionId.Tailor, requiredLevel = 2, xpReward = 9, station = "", inputs = { new ItemStack { itemId = "fiber", count = 4 } } });
            Recipes.Add(new Recipe { id = "r_copper", resultItemId = "copper_bar", resultCount = 1, profession = ProfessionId.Smith, requiredLevel = 1, xpReward = 8, station = "forge", inputs = { new ItemStack { itemId = "copper_ore", count = 2 } } });
            Recipes.Add(new Recipe { id = "r_iron", resultItemId = "iron_bar", resultCount = 1, profession = ProfessionId.Smith, requiredLevel = 3, xpReward = 12, station = "forge", inputs = { new ItemStack { itemId = "iron_ore", count = 2 } } });
            Recipes.Add(new Recipe { id = "r_snack", resultItemId = "trail_snack", resultCount = 2, profession = ProfessionId.Chef, requiredLevel = 1, xpReward = 7, station = "campfire", inputs = { new ItemStack { itemId = "fiber", count = 1 }, new ItemStack { itemId = "bitterleaf", count = 1 } } });
            Recipes.Add(new Recipe { id = "r_stew", resultItemId = "hearty_stew", resultCount = 1, profession = ProfessionId.Chef, requiredLevel = 3, xpReward = 15, station = "campfire", inputs = { new ItemStack { itemId = "raw_fish", count = 1 }, new ItemStack { itemId = "glowcap", count = 1 }, new ItemStack { itemId = "bitterleaf", count = 1 } } });
            Recipes.Add(new Recipe { id = "r_bun", resultItemId = "spice_bun", resultCount = 1, profession = ProfessionId.Chef, requiredLevel = 2, xpReward = 10, station = "campfire", inputs = { new ItemStack { itemId = "emberpepper", count = 1 }, new ItemStack { itemId = "fiber", count = 2 } } });
            Recipes.Add(new Recipe { id = "r_salve", resultItemId = "salve", resultCount = 1, profession = ProfessionId.Chef, requiredLevel = 2, xpReward = 9, station = "", inputs = { new ItemStack { itemId = "bitterleaf", count = 2 }, new ItemStack { itemId = "glowcap", count = 1 } } });
            Recipes.Add(new Recipe { id = "r_purge", resultItemId = "purge_draught", resultCount = 1, profession = ProfessionId.Chef, requiredLevel = 3, xpReward = 12, station = "", inputs = { new ItemStack { itemId = "bitterleaf", count = 2 }, new ItemStack { itemId = "emberpepper", count = 1 } } });
            Recipes.Add(new Recipe { id = "r_revive", resultItemId = "revive_tonic", resultCount = 1, profession = ProfessionId.Chef, requiredLevel = 5, xpReward = 25, station = "", inputs = { new ItemStack { itemId = "gildfish", count = 1 }, new ItemStack { itemId = "glowcap", count = 2 }, new ItemStack { itemId = "bitterleaf", count = 2 } } });
            Recipes.Add(new Recipe { id = "r_core_wood", resultItemId = "core_wood", resultCount = 1, profession = ProfessionId.Smith, requiredLevel = 1, xpReward = 10, station = "forge", inputs = { new ItemStack { itemId = "stone", count = 2 }, new ItemStack { itemId = "rope", count = 1 } } });
            Recipes.Add(new Recipe { id = "r_core_iron", resultItemId = "core_iron", resultCount = 1, profession = ProfessionId.Smith, requiredLevel = 3, xpReward = 20, station = "forge", inputs = { new ItemStack { itemId = "iron_bar", count = 1 }, new ItemStack { itemId = "copper_bar", count = 1 }, new ItemStack { itemId = "rope", count = 1 } } });
            Recipes.Add(new Recipe { id = "r_gloves", resultItemId = "forager_gloves", resultCount = 1, profession = ProfessionId.Tailor, requiredLevel = 3, xpReward = 22, station = "workbench", inputs = { new ItemStack { itemId = "cloth", count = 2 }, new ItemStack { itemId = "rope", count = 1 }, new ItemStack { itemId = "copper_bar", count = 1 } } });
            Recipes.Add(new Recipe { id = "r_pick", resultItemId = "miner_pick", resultCount = 1, profession = ProfessionId.Smith, requiredLevel = 4, xpReward = 25, station = "forge", inputs = { new ItemStack { itemId = "iron_bar", count = 2 }, new ItemStack { itemId = "rope", count = 1 } } });
            Recipes.Add(new Recipe { id = "r_rod", resultItemId = "angler_rod", resultCount = 1, profession = ProfessionId.Tailor, requiredLevel = 4, xpReward = 25, station = "workbench", inputs = { new ItemStack { itemId = "rope", count = 2 }, new ItemStack { itemId = "iron_bar", count = 1 }, new ItemStack { itemId = "cloth", count = 1 } } });
            Recipes.Add(new Recipe { id = "r_lure", resultItemId = "lure_charm", resultCount = 1, profession = ProfessionId.Tailor, requiredLevel = 5, xpReward = 30, station = "workbench", inputs = { new ItemStack { itemId = "gildfish", count = 1 }, new ItemStack { itemId = "cloth", count = 2 }, new ItemStack { itemId = "glowcap", count = 1 } } });
            Recipes.Add(new Recipe { id = "r_scout", resultItemId = "scout_band", resultCount = 1, profession = ProfessionId.Tailor, requiredLevel = 5, xpReward = 30, station = "workbench", inputs = { new ItemStack { itemId = "cloth", count = 2 }, new ItemStack { itemId = "iron_bar", count = 1 }, new ItemStack { itemId = "moonshard", count = 1 } } });

            // ---------------- Weaponsmith recipes: the weapon ladder ----------------
            // One recipe per kind per tier. The tier sets BOTH the profession
            // level (2/4/6/8/10 — two a rung) and the materials, which climb from
            // fibre-and-stone to moonshard-and-gildfish. That makes the top rungs
            // genuinely late-game: moonshard only comes off the Hollow Monolith
            // (Miner 6) or a Snorlax jackpot, and gildfish is a rare lake catch.
            // Each kind keeps the trade that has always forged it.
            RW("r_wpn_club", "wpn_club", ProfessionId.Forager, 2, 8, S("fiber", 2), S("stone", 1));
            RW("r_wpn_club_uncommon", "wpn_club_uncommon", ProfessionId.Forager, 4, 45,
               S("fiber", 4), S("iron_bar", 1), S("rope", 2));
            RW("r_wpn_club_rare", "wpn_club_rare", ProfessionId.Forager, 6, 75,
               S("iron_bar", 2), S("rope", 2), S("fiber", 4), S("emberpepper", 2));
            RW("r_wpn_club_epic", "wpn_club_epic", ProfessionId.Forager, 8, 115,
               S("iron_bar", 3), S("moonshard", 1), S("fiber", 4), S("cloth", 2));
            RW("r_wpn_club_legendary", "wpn_club_legendary", ProfessionId.Forager, 10, 170,
               S("iron_bar", 4), S("moonshard", 2), S("gildfish", 1), S("glowcap", 3));

            RW("r_wpn_blade", "wpn_blade", ProfessionId.Smith, 2, 20, S("copper_bar", 2), S("rope", 1));
            RW("r_wpn_blade_uncommon", "wpn_blade_uncommon", ProfessionId.Smith, 4, 45,
               S("iron_bar", 2), S("copper_bar", 1), S("rope", 1));
            RW("r_wpn_blade_rare", "wpn_blade_rare", ProfessionId.Smith, 6, 75,
               S("iron_bar", 2), S("cloth", 2), S("rope", 1), S("emberpepper", 2));
            RW("r_wpn_blade_epic", "wpn_blade_epic", ProfessionId.Smith, 8, 115,
               S("iron_bar", 3), S("moonshard", 1), S("cloth", 2), S("copper_bar", 2));
            RW("r_wpn_blade_legendary", "wpn_blade_legendary", ProfessionId.Smith, 10, 170,
               S("iron_bar", 4), S("moonshard", 2), S("gildfish", 1), S("emberpepper", 3));

            RW("r_wpn_spear", "wpn_spear", ProfessionId.Tailor, 2, 16,
               S("rope", 1), S("copper_bar", 1), S("fiber", 2));
            RW("r_wpn_spear_uncommon", "wpn_spear_uncommon", ProfessionId.Tailor, 4, 45,
               S("iron_bar", 1), S("rope", 2), S("copper_bar", 1), S("fiber", 2));
            RW("r_wpn_spear_rare", "wpn_spear_rare", ProfessionId.Tailor, 6, 75,
               S("iron_bar", 2), S("rope", 2), S("cloth", 2), S("emberpepper", 1));
            RW("r_wpn_spear_epic", "wpn_spear_epic", ProfessionId.Tailor, 8, 115,
               S("iron_bar", 3), S("moonshard", 1), S("rope", 2), S("cloth", 2));
            RW("r_wpn_spear_legendary", "wpn_spear_legendary", ProfessionId.Tailor, 10, 170,
               S("iron_bar", 4), S("moonshard", 2), S("gildfish", 1), S("glowcap", 2), S("cloth", 2));

            RW("r_wpn_axe", "wpn_axe", ProfessionId.Smith, 2, 40,
               S("iron_bar", 2), S("rope", 1), S("fiber", 2));
            RW("r_wpn_axe_uncommon", "wpn_axe_uncommon", ProfessionId.Smith, 4, 45,
               S("iron_bar", 2), S("copper_bar", 1), S("rope", 1), S("fiber", 2));
            RW("r_wpn_axe_rare", "wpn_axe_rare", ProfessionId.Smith, 6, 75,
               S("iron_bar", 3), S("rope", 1), S("cloth", 2), S("emberpepper", 2));
            RW("r_wpn_axe_epic", "wpn_axe_epic", ProfessionId.Smith, 8, 115,
               S("iron_bar", 4), S("moonshard", 1), S("cloth", 2), S("rope", 1));
            RW("r_wpn_axe_legendary", "wpn_axe_legendary", ProfessionId.Smith, 10, 170,
               S("iron_bar", 5), S("moonshard", 2), S("gildfish", 1), S("emberpepper", 3));

            // ---------------- Armourer recipes: the armour ladder ----------------
            // Same five rungs, one piece per tier, forged by the Smith. "def+N"
            // is flat damage taken off every wild hit, "hp+N" raises the pool.
            RA("r_arm_padded", "arm_padded", 2, 25, S("fiber", 4), S("rope", 2));
            RA("r_arm_boiled", "arm_boiled", 4, 50,
               S("cloth", 2), S("rope", 2), S("copper_bar", 1));
            RA("r_arm_scale", "arm_scale", 6, 80,
               S("cloth", 3), S("iron_bar", 2), S("rope", 1));
            RA("r_arm_plate", "arm_plate", 8, 120,
               S("iron_bar", 3), S("cloth", 2), S("moonshard", 1));
            RA("r_arm_aegis", "arm_aegis", 10, 180,
               S("iron_bar", 4), S("moonshard", 2), S("glowcap", 3), S("gildfish", 1));

            // ---------------- Creature gear recipes ----------------
            Recipes.Add(new Recipe { id = "r_gear_claw", resultItemId = "gear_claw", resultCount = 1, profession = ProfessionId.Smith, requiredLevel = 1, xpReward = 12, station = "forge", inputs = { new ItemStack { itemId = "copper_bar", count = 1 }, new ItemStack { itemId = "stone", count = 2 } } });
            Recipes.Add(new Recipe { id = "r_gear_fang", resultItemId = "gear_fang", resultCount = 1, profession = ProfessionId.Smith, requiredLevel = 3, xpReward = 22, station = "forge", inputs = { new ItemStack { itemId = "iron_bar", count = 1 }, new ItemStack { itemId = "emberpepper", count = 2 }, new ItemStack { itemId = "rope", count = 1 } } });
            Recipes.Add(new Recipe { id = "r_gear_stormspine", resultItemId = "gear_stormspine", resultCount = 1, profession = ProfessionId.Smith, requiredLevel = 6, xpReward = 45, station = "forge", inputs = { new ItemStack { itemId = "iron_bar", count = 2 }, new ItemStack { itemId = "moonshard", count = 1 }, new ItemStack { itemId = "copper_bar", count = 2 } } });
            Recipes.Add(new Recipe { id = "r_gear_band", resultItemId = "gear_band", resultCount = 1, profession = ProfessionId.Tailor, requiredLevel = 1, xpReward = 10, station = "workbench", inputs = { new ItemStack { itemId = "fiber", count = 3 }, new ItemStack { itemId = "rope", count = 1 } } });
            Recipes.Add(new Recipe { id = "r_gear_hide", resultItemId = "gear_hide", resultCount = 1, profession = ProfessionId.Tailor, requiredLevel = 3, xpReward = 20, station = "workbench", inputs = { new ItemStack { itemId = "cloth", count = 2 }, new ItemStack { itemId = "rope", count = 1 }, new ItemStack { itemId = "copper_bar", count = 1 } } });
            Recipes.Add(new Recipe { id = "r_gear_plate", resultItemId = "gear_plate", resultCount = 1, profession = ProfessionId.Smith, requiredLevel = 5, xpReward = 40, station = "forge", inputs = { new ItemStack { itemId = "iron_bar", count = 2 }, new ItemStack { itemId = "cloth", count = 1 }, new ItemStack { itemId = "rope", count = 1 } } });
            Recipes.Add(new Recipe { id = "r_gear_charm", resultItemId = "gear_charm", resultCount = 1, profession = ProfessionId.Tailor, requiredLevel = 5, xpReward = 35, station = "workbench", inputs = { new ItemStack { itemId = "cloth", count = 1 }, new ItemStack { itemId = "glowcap", count = 2 }, new ItemStack { itemId = "copper_bar", count = 1 } } });
        }

        private static void Add(ItemDef d) => All[d.id] = d;

        /// <summary>Player melee weapon at a tier — see ItemRarity for the damage ladder.</summary>
        private static void W(string id, string name, PlayerWeaponKind kind, Rarity r,
                              int price, string hex, string desc)
        {
            Add(new ItemDef
            {
                id = id, name = name, desc = desc, kind = ItemKind.Equipment,
                basePrice = price, playerWeaponKind = kind, rarity = r, iconColorHex = hex
            });
        }

        /// <summary>Player body armour at a tier; bonus is "def+N|hp+N".</summary>
        private static void A(string id, string name, Rarity r, int price, string bonus,
                              string hex, string desc)
        {
            Add(new ItemDef
            {
                id = id, name = name, desc = desc, kind = ItemKind.Equipment,
                basePrice = price, equipBonus = bonus, rarity = r, iconColorHex = hex
            });
        }

        static ItemStack S(string itemId, int count) { return new ItemStack { itemId = itemId, count = count }; }

        /// <summary>A Weaponsmith recipe (the station is implied by the ladder).</summary>
        private static void RW(string id, string weaponId, ProfessionId prof, int lvl, int xp,
                               params ItemStack[] inputs)
        {
            var r = new Recipe
            {
                id = id, resultItemId = weaponId, resultCount = 1, profession = prof,
                requiredLevel = lvl, xpReward = xp, station = "weaponsmith"
            };
            r.inputs.AddRange(inputs);
            Recipes.Add(r);
        }

        /// <summary>An Armourer recipe (Smith work, at the Armourer's Stand).</summary>
        private static void RA(string id, string armorId, int lvl, int xp, params ItemStack[] inputs)
        {
            var r = new Recipe
            {
                id = id, resultItemId = armorId, resultCount = 1, profession = ProfessionId.Smith,
                requiredLevel = lvl, xpReward = xp, station = "armorer"
            };
            r.inputs.AddRange(inputs);
            Recipes.Add(r);
        }

        public static ItemDef Get(string id)
        {
            ItemDef d;
            return All != null && All.TryGetValue(id, out d) ? d : null;
        }

        /// <summary>
        /// The recipe that produces this item, or null. One result has one recipe in
        /// this game, so the first match is the only match — used by the gear panel to
        /// say where a rung you have not reached can be made.
        /// </summary>
        public static Recipe RecipeFor(string resultItemId)
        {
            if (Recipes == null || string.IsNullOrEmpty(resultItemId)) return null;
            foreach (var r in Recipes)
                if (r != null && r.resultItemId == resultItemId) return r;
            return null;
        }

        public static string Name(string id)
        {
            var d = Get(id);
            return d != null ? d.name : id;
        }

        // ---------------- shared gear pools (single source of truth for UI) ----------------

        /// <summary>Creature weapon items, strongest last (also the picker order).</summary>
        public static readonly string[] CreatureWeapons = { "gear_claw", "gear_fang", "gear_stormspine" };

        /// <summary>Creature armor items, strongest last.</summary>
        public static readonly string[] CreatureArmors = { "gear_band", "gear_hide", "gear_charm", "gear_plate" };

        /// <summary>Trainer equipment: one may be equipped at a time via GameState.equippedItem.</summary>
        public static readonly string[] TrainerGear = { "forager_gloves", "miner_pick", "angler_rod", "lure_charm", "scout_band" };

        /// <summary>
        /// Every trainer melee weapon, grouped by kind and walking the rarity
        /// ladder (Normal → Legendary). PlayerGear indexes this by kind+tier, so
        /// a rung missing here shows up as an unauthored recipe rather than a
        /// silently unbuildable item — EditorPlaytest asserts the ladder is
        /// complete for all four kinds.
        /// </summary>
        public static readonly string[] PlayerWeapons =
        {
            // Blade
            "wpn_blade", "wpn_blade_uncommon", "wpn_blade_rare", "wpn_blade_epic", "wpn_blade_legendary",
            // Spear
            "wpn_spear", "wpn_spear_uncommon", "wpn_spear_rare", "wpn_spear_epic", "wpn_spear_legendary",
            // Axe
            "wpn_axe", "wpn_axe_uncommon", "wpn_axe_rare", "wpn_axe_epic", "wpn_axe_legendary",
            // Club
            "wpn_club", "wpn_club_uncommon", "wpn_club_rare", "wpn_club_epic", "wpn_club_legendary"
        };

        /// <summary>Trainer body armour, weakest tier first (see PlayerGear.Armors).</summary>
        public static readonly string[] PlayerArmors =
        {
            "arm_padded", "arm_boiled", "arm_scale", "arm_plate", "arm_aegis"
        };

        /// <summary>Use-on-creature consumables in priority order (best fit first).</summary>
        public static readonly string[] HealOrder = { "revive_tonic", "hearty_stew", "spice_bun", "salve", "trail_snack", "purge_draught" };
    }
}
