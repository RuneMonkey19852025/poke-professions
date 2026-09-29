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

        public static ItemDef Get(string id)
        {
            ItemDef d;
            return All != null && All.TryGetValue(id, out d) ? d : null;
        }

        public static string Name(string id)
        {
            var d = Get(id);
            return d != null ? d.name : id;
        }
    }
}
