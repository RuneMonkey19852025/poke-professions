using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PokeProfessions.Core;
using PokeProfessions.World;
using PokeProfessions.Audio;

namespace PokeProfessions.UI
{
    /// <summary>
    /// Character gear screen (key Q): the trainer's weapons and body armour.
    ///
    /// Both are ladders now — four weapon kinds and one armour slot, each
    /// Normal → Legendary (see ItemRarity) — so this lists what is actually in
    /// the bag rather than a fixed row per authored item, and colours each row
    /// by its tier. Equipping a weapon shows its real, rarity-scaled damage;
    /// equipping armour shows the damage it soaks and the health it grants.
    /// Key: Q.
    /// </summary>
    public class WeaponPanel : MonoBehaviour
    {
        CanvasGroup cg;
        Text equippedText, skillText, detailText;
        RectTransform weaponContent, armourContent;
        ScrollRect weaponScroll, armourScroll;
        readonly List<Button> weaponRows = new List<Button>();
        readonly List<Button> armourRows = new List<Button>();

        const float RowHeight = 32f, RowGap = 36f;

        public static WeaponPanel Build(Canvas canvas)
        {
            var go = new GameObject("WeaponPanel", typeof(RectTransform), typeof(WeaponPanel));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(canvas.transform, false);
            UIFactory.Stretch(rt);
            var p = go.GetComponent<WeaponPanel>();
            p.Build();
            p.Show(false);
            return p;
        }

        void Build()
        {
            cg = gameObject.AddComponent<CanvasGroup>();

            var window = UIFactory.BoxRounded(transform, "window", UiTheme.Bg,
                new Color(0.16f, 0.20f, 0.35f), 4f,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-300, -230), new Vector2(300, 230));

            UIFactory.TextAt(window, "header", "Weapons & Armour", 20,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -40), new Vector2(-160, -8),
                TextAnchor.MiddleLeft);

            UIFactory.TextAt(window, "hint", "Swing in the world with Space or click. Wild creatures hit back — armour is what you stand behind.", 12,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -62), new Vector2(-16, -40),
                TextAnchor.MiddleLeft, UiTheme.TextDim);

            UIFactory.Button(window, "close", "X  [Esc]", new Vector2(1, 1), new Vector2(96, 30),
                () => { Show(false); if (UiRouter.I != null) UiRouter.I.ClosePanel(gameObject); }, 13, UiTheme.Panel);

            equippedText = UIFactory.TextAt(window, "equipped", "", 14,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -90), new Vector2(-16, -66),
                TextAnchor.MiddleLeft);

            skillText = UIFactory.TextAt(window, "skill", "", 13,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -114), new Vector2(-16, -90),
                TextAnchor.MiddleLeft, UiTheme.TextDim);

            UIFactory.TextAt(window, "wh", "Weapons", 13,
                new Vector2(0, 1), new Vector2(0.5f, 1), new Vector2(16, -138), new Vector2(-6, -116),
                TextAnchor.MiddleLeft, UiTheme.TextDim);
            UIFactory.TextAt(window, "ah", "Armour", 13,
                new Vector2(0.5f, 1), new Vector2(1, 1), new Vector2(6, -138), new Vector2(-16, -116),
                TextAnchor.MiddleLeft, UiTheme.TextDim);

            MakeList(window, "weapons", new Vector2(0, 0), new Vector2(0.5f, 1),
                new Vector2(12, 96), new Vector2(-6, -142), out weaponContent, out weaponScroll);
            MakeList(window, "armour", new Vector2(0.5f, 0), new Vector2(1, 1),
                new Vector2(6, 96), new Vector2(-12, -142), out armourContent, out armourScroll);

            detailText = UIFactory.TextAt(window, "detail", "", 12,
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(16, 12), new Vector2(-16, 90),
                TextAnchor.LowerLeft, UiTheme.TextDim);
        }

        /// <summary>Borderless, masked, vertically scrollable row column.</summary>
        void MakeList(RectTransform window, string name, Vector2 anchorMin, Vector2 anchorMax,
                      Vector2 offMin, Vector2 offMax, out RectTransform content, out ScrollRect scroll)
        {
            var frame = UIFactory.Box(window, name, new Color(0, 0, 0, 0.15f),
                anchorMin, anchorMax, offMin, offMax);
            frame.gameObject.AddComponent<RectMask2D>();

            content = UIFactory.Box(frame, name + "Content", new Color(0, 0, 0, 0),
                new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0, 0);

            scroll = frame.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = frame;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;
        }

        public void Open()
        {
            Refresh();
            Show(true);
        }

        void Refresh()
        {
            var s = Game.I.State;

            equippedText.text = "Wielding: " +
                (string.IsNullOrEmpty(s.playerWeapon)
                    ? "<color=#6FCF97>Bare Hands</color>"
                    : ItemRarity.RichName(s.playerWeapon)) +
                "    Armour: " +
                (string.IsNullOrEmpty(s.playerArmor)
                    ? "<color=#9FB0C0>none</color>"
                    : ItemRarity.RichName(s.playerArmor)) +
                "    <color=#6FCF97>" + PlayerCombat.HealthLabel(s) + " HP</color>";

            if (string.IsNullOrEmpty(s.playerWeapon))
                skillText.text = "Weapon skill grows as you land hits.";
            else
            {
                int need = PlayerCombat.XpToNext(Mathf.Min(s.playerWeaponLevel, (int)PlayerCombat.MaxMasteryLevel - 1));
                skillText.text = "Skill Lv" + s.playerWeaponLevel + "   xp " + s.playerWeaponXp + "/" + need +
                                 "   (+" + s.playerWeaponLevel + "% damage)";
            }

            // ---- weapons: bare hands, then everything owned (or worn) ----
            foreach (var b in weaponRows) if (b != null) Destroy(b.gameObject);
            weaponRows.Clear();
            AddWeaponRow(s, "", "Bare Hands", 0);
            foreach (var id in ItemDB.PlayerWeapons)
            {
                int owned = s.CountItem(id);
                if (owned <= 0 && s.playerWeapon != id) continue; // the bag leads
                AddWeaponRow(s, id, ItemDB.Name(id), owned);
            }
            // ...then the rungs still ahead of you, greyed rather than hidden. A ladder
            // whose top simply vanishes reads as a short game; showing it turns the
            // same list into what is next, each row saying how to get there.
            foreach (var id in ItemDB.PlayerWeapons)
            {
                if (s.CountItem(id) > 0 || s.playerWeapon == id) continue;
                AddLockedRow(weaponContent, weaponRows, s, id);
            }
            weaponContent.sizeDelta = new Vector2(0, 12f + weaponRows.Count * RowGap);
            if (weaponScroll != null) weaponScroll.verticalNormalizedPosition = 1f;

            // ---- armour: none, then everything owned (or worn) ----
            foreach (var b in armourRows) if (b != null) Destroy(b.gameObject);
            armourRows.Clear();
            AddArmourRow(s, "", "No armour", 0);
            foreach (var id in ItemDB.PlayerArmors)
            {
                int owned = s.CountItem(id);
                if (owned <= 0 && s.playerArmor != id) continue;
                AddArmourRow(s, id, ItemDB.Name(id), owned);
            }
            foreach (var id in ItemDB.PlayerArmors)
            {
                if (s.CountItem(id) > 0 || s.playerArmor == id) continue;
                AddLockedRow(armourContent, armourRows, s, id);
            }
            armourContent.sizeDelta = new Vector2(0, 12f + armourRows.Count * RowGap);
            if (armourScroll != null) armourScroll.verticalNormalizedPosition = 1f;

            detailText.text = DetailText(s);
        }

        void AddWeaponRow(GameState s, string id, string name, int owned)
        {
            bool worn = s.playerWeapon == id;
            var btn = UIFactory.Button(weaponContent, "w_" + (id == "" ? "none" : id),
                RowLabel(worn, name, owned), new Vector2(0, 1), new Vector2(240, RowHeight),
                () => EquipWeapon(id), 12);
            PlaceRow(btn, weaponContent, weaponRows.Count);
            ColourRow(btn, id, worn, id == "" || owned > 0 || worn);
            weaponRows.Add(btn);
        }

        void AddArmourRow(GameState s, string id, string name, int owned)
        {
            bool worn = s.playerArmor == id;
            var btn = UIFactory.Button(armourContent, "a_" + (id == "" ? "none" : id),
                RowLabel(worn, name, owned), new Vector2(0, 1), new Vector2(240, RowHeight),
                () => EquipArmor(id), 12);
            PlaceRow(btn, armourContent, armourRows.Count);
            ColourRow(btn, id, worn, id == "" || owned > 0 || worn);
            armourRows.Add(btn);
        }

        /// <summary>
        /// A rung of the ladder you have not reached: dim, but still on the list and
        /// still telling you how to get it — buy it from Hask once he deals that tier,
        /// or forge it at its bench once your profession is high enough. This is the
        /// same progression the armoury spells out, kept beside the gear it describes.
        /// </summary>
        void AddLockedRow(RectTransform content, List<Button> rows, GameState s, string id)
        {
            var def = ItemDB.Get(id);
            string tier = def != null ? ItemRarity.Name(def.rarity) : "?";
            string tierHex = def != null ? ItemRarity.ColorHex(def.rarity) : "#C8CDD4";
            string label = "<color=#8A94A0>[not yet]</color>  <color=" + tierHex + ">" +
                           ItemDB.Name(id) + "</color>  " + tier + "  —  " + LockHint(s, id);

            string itemId = id;
            var btn = UIFactory.Button(content, "locked_" + id, label, new Vector2(0, 1),
                new Vector2(240, RowHeight),
                () => Toast.Push(ItemDB.Name(itemId) + ": " + LockHint(Game.I.State, itemId)), 12);
            PlaceRow(btn, content, rows.Count);

            var lbl = btn.GetComponentInChildren<Text>();
            if (lbl != null) lbl.color = UiTheme.TextDim;   // the name keeps its tier colour
            rows.Add(btn);
        }

        /// <summary>How to get an item you do not own yet: the buy-or-forge line.</summary>
        static string LockHint(GameState s, string itemId)
        {
            if (s != null && StoryData.Stocks("armory", itemId, s)) return "buy at Hask's Armoury";
            var r = ItemDB.RecipeFor(itemId);
            if (r == null) return "not available yet";
            string bench = string.IsNullOrEmpty(r.station) ? "any bench" : BenchName(r.station);
            return ProfessionSystem.Name(r.profession) + " Lv" + r.requiredLevel + " at " + bench;
        }

        /// <summary>Short bench name for a hint line (matches CraftingPanel / the station labels).</summary>
        static string BenchName(string station)
        {
            switch (station)
            {
                case "forge": return "the Forge";
                case "campfire": return "the Campfire";
                case "workbench": return "the Workbench";
                case "weaponsmith": return "the Weaponsmith's Bench";
                case "armorer": return "the Armourer's Stand";
                case "gear_table": return "the Gearwright's Table";
            }
            return station;
        }

        static string RowLabel(bool worn, string name, int owned)
        {
            return (worn ? "[x] " : "[  ] ") + name + (owned > 0 ? "  x" + owned : "") +
                   (worn ? "  (worn)" : "");
        }

        static void PlaceRow(Button btn, RectTransform parent, int index)
        {
            btn.transform.SetParent(parent, false);
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -6 - index * RowGap);
            rt.sizeDelta = new Vector2(0, RowHeight);

            var lbl = btn.GetComponentInChildren<Text>();
            if (lbl != null) lbl.alignment = TextAnchor.MiddleLeft;
        }

        /// <summary>Worn wins over tier; a rung you cannot afford yet sits dim.</summary>
        static void ColourRow(Button btn, string itemId, bool worn, bool available)
        {
            var lbl = btn.GetComponentInChildren<Text>();
            if (lbl == null) return;
            if (worn) { lbl.color = UiTheme.Accent; return; }
            if (!available) { lbl.color = UiTheme.TextDim; return; }

            var def = ItemDB.Get(itemId);
            lbl.color = def != null ? UiTheme.Hex(ItemRarity.ColorHex(def.rarity)) : UiTheme.Text;
        }

        string DetailText(GameState s)
        {
            var sb = new System.Text.StringBuilder();
            string w = s.playerWeapon;
            if (string.IsNullOrEmpty(w))
                sb.AppendLine("Bare hands: 2 dmg, short reach. Doesn't build skill.");
            else
                sb.AppendLine(ItemRarity.Line(w) + "   DMG " + PlayerCombat.BaseDamage(w) +
                              "   cooldown " + PlayerCombat.Cooldown(w).ToString("0.0") + "s" +
                              "   reach " + PlayerCombat.Reach(w).ToString("0.0") +
                              "   (+" + s.playerWeaponLevel + "% from skill)");

            string a = s.playerArmor;
            if (string.IsNullOrEmpty(a))
                sb.AppendLine("No armour: every wild hit lands for full. " + PlayerCombat.BaseHP + " max HP.");
            else
                sb.AppendLine(ItemRarity.Line(a) + "   soaks " + PlayerCombat.Mitigation(s) +
                              " per hit   HP " + PlayerCombat.BaseHP + " +" +
                              (PlayerCombat.MaxHP(s) - PlayerCombat.BaseHP) + " = " + PlayerCombat.MaxHP(s));

            sb.Append("Craft the ladder at the Weaponsmith's Bench, the Armourer's Stand or the Gearwright's Table.\n");
            sb.Append("Greyed rungs aren't yours yet — the line beside them says where to buy or forge one.");
            return sb.ToString();
        }

        // ---------------- equip ----------------

        void EquipWeapon(string itemId)
        {
            var s = Game.I.State;
            if (string.IsNullOrEmpty(itemId))
            {
                if (string.IsNullOrEmpty(s.playerWeapon)) return;
                // put it back in the bag — a legendary must never vanish on unequip
                s.AddItem(s.playerWeapon, 1);
                s.playerWeapon = "";
                Sfx.I.Play("ui");
                Toast.Push("You put the weapon away. Bare hands it is.");
                AfterEquip();
                return;
            }

            if (s.playerWeapon == itemId) return; // already worn
            if (s.CountItem(itemId) <= 0)
            {
                Toast.Push("You don't own the " + ItemDB.Name(itemId) + " yet.");
                return;
            }

            if (!string.IsNullOrEmpty(s.playerWeapon)) s.AddItem(s.playerWeapon, 1);
            s.RemoveItem(itemId, 1);
            s.playerWeapon = itemId;
            Sfx.I.Play("craft");
            Toast.Push("Equipped " + ItemDB.Name(itemId) + ".");
            AfterEquip();
        }

        void EquipArmor(string itemId)
        {
            var s = Game.I.State;
            if (s.playerArmor == itemId) return;

            if (string.IsNullOrEmpty(itemId))
            {
                if (string.IsNullOrEmpty(s.playerArmor)) return;
                s.AddItem(s.playerArmor, 1);
                s.playerArmor = "";
                Sfx.I.Play("ui");
                Toast.Push("You take the armour off.  " + PlayerCombat.HealthLabel(s) + " HP");
                AfterEquip();
                return;
            }

            if (s.CountItem(itemId) <= 0)
            {
                Toast.Push("You don't own the " + ItemDB.Name(itemId) + " yet.");
                return;
            }

            if (!string.IsNullOrEmpty(s.playerArmor)) s.AddItem(s.playerArmor, 1);
            s.RemoveItem(itemId, 1);
            s.playerArmor = itemId;
            // new armour can raise the ceiling; it never tops the pool up by itself
            if (s.playerHP > PlayerCombat.MaxHP(s)) s.playerHP = PlayerCombat.MaxHP(s);
            Sfx.I.Play("craft");
            Toast.Push("Equipped " + ItemDB.Name(itemId) + ".  " +
                       PlayerCombat.Mitigation(s) + " soak, " + PlayerCombat.MaxHP(s) + " max HP");
            AfterEquip();
        }

        void AfterEquip()
        {
            if (PlayerController.I != null) PlayerController.I.RefreshGearProps();
            Refresh();
        }

        public void Show(bool on)
        {
            cg.alpha = on ? 1f : 0f;
            cg.blocksRaycasts = on;
            cg.interactable = on;
        }
    }
}
