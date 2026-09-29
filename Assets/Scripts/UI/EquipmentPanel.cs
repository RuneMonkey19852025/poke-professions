using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PokeProfessions.Core;
using PokeProfessions.World;
using PokeProfessions.Audio;

namespace PokeProfessions.UI
{
    /// <summary>
    /// Gear screen: trainer equipment on the left, creature weapon/armor on the
    /// right. Pick a creature on top, then equip from the pools below. Key: G.
    /// </summary>
    public class EquipmentPanel : MonoBehaviour
    {
        CanvasGroup cg;
        Text trainerSlot;
        Text[] creatureGear = new Text[6];
        RectTransform creatureList;
        Text detail;
        readonly List<Button> rowButtons = new List<Button>();
        readonly List<Button> creatureButtons = new List<Button>();
        int selectedIdx = -1;

        public static EquipmentPanel Build(Canvas canvas)
        {
            var go = new GameObject("EquipmentPanel", typeof(RectTransform), typeof(EquipmentPanel));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(canvas.transform, false);
            UIFactory.Stretch(rt);
            var p = go.GetComponent<EquipmentPanel>();
            p.Build();
            p.Show(false);
            return p;
        }

        void Build()
        {
            cg = gameObject.AddComponent<CanvasGroup>();

            var window = UIFactory.Box(transform, "window", UiTheme.Bg,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-430, -265), new Vector2(430, 265));

            UIFactory.TextAt(window, "header", "Gear", 20,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -36), new Vector2(-160, -8),
                TextAnchor.MiddleLeft);

            UIFactory.TextAt(window, "coins", "", 14,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(-230, -32), new Vector2(-128, -10),
                TextAnchor.MiddleRight, UiTheme.Accent2);

            UIFactory.Button(window, "close", "X  [Esc]", new Vector2(1, 1), new Vector2(96, 30),
                () => { Show(false); if (UiRouter.I != null) UiRouter.I.ClosePanel(gameObject); }, 13, UiTheme.Panel);

            // ---- left column: trainer equipment ----
            var left = UIFactory.Box(window, "left", new Color(0, 0, 0, 0.15f),
                new Vector2(0, 0), new Vector2(0.42f, 1), new Vector2(10, 10), new Vector2(-6, -44));
            UIFactory.TextAt(left, "leftHeader", "Trainer equipment", 14,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -26), new Vector2(-8, -4),
                TextAnchor.MiddleLeft, UiTheme.TextDim);

            trainerSlot = UIFactory.TextAt(left, "trainerSlot", "", 13,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -52), new Vector2(-8, -30),
                TextAnchor.UpperLeft);

            float y = 96f;
            foreach (var id in ItemDB.TrainerGear)
            {
                var captured = id;
                UIFactory.Button(left, "tg_" + id, "", new Vector2(0, 1), new Vector2(240, 34),
                    () => ToggleTrainerGear(captured), 12);
                var rt = (RectTransform)left.GetChild(left.childCount - 1);
                rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0, -y);
                rt.sizeDelta = new Vector2(0, 34);
                y += 38f;
            }

            // ---- right column: creatures & their gear ----
            creatureList = UIFactory.Box(window, "creatures", new Color(0, 0, 0, 0.15f),
                new Vector2(0.42f, 0.56f), new Vector2(1, 1), new Vector2(6, 8), new Vector2(-6, -44));

            detail = UIFactory.TextAt(window, "detail", "", 13,
                new Vector2(0.42f, 0), new Vector2(1, 0.56f), new Vector2(8, 8), new Vector2(-8, -6),
                TextAnchor.UpperLeft);

            BuildGearButtons(window);
        }

        /// <summary>Bottom-right cluster: equip weapon / equip armor / unequip.</summary>
        void BuildGearButtons(RectTransform window)
        {
            var eqW = UIFactory.Button(window, "equipW", "Equip weapon…", new Vector2(1, 0), new Vector2(160, 34),
                () => EquipFromPool(true), 13);
            var eqWRt = eqW.GetComponent<RectTransform>();
            eqWRt.anchoredPosition = new Vector2(-90f, 26f);

            var eqA = UIFactory.Button(window, "equipA", "Equip armor…", new Vector2(1, 0), new Vector2(160, 34),
                () => EquipFromPool(false), 13);
            var eqARt = eqA.GetComponent<RectTransform>();
            eqARt.anchoredPosition = new Vector2(-90f, 66f);

            var un = UIFactory.Button(window, "unequip", "Unequip", new Vector2(1, 0), new Vector2(160, 34),
                UnequipSelected, 13);
            var unRt = un.GetComponent<RectTransform>();
            unRt.anchoredPosition = new Vector2(-90f, 106f);
        }

        public void Open()
        {
            selectedIdx = -1;
            Refresh();
            Show(true);
        }

        void Refresh()
        {
            var s = Game.I.State;

            // trainer side
            trainerSlot.text = string.IsNullOrEmpty(s.equippedItem)
                ? "Equipped: nothing"
                : "Equipped: <color=#6FCF97>" + ItemDB.Name(s.equippedItem) + "</color>";

            var tg = GetComponentsInChildren<Button>();
            foreach (var b in tg)
            {
                if (b.name.StartsWith("tg_"))
                {
                    string id = b.name.Substring(3);
                    var lbl = b.GetComponentInChildren<Text>();
                    if (lbl == null) continue;
                    int owned = s.CountItem(id);
                    var def = ItemDB.Get(id);
                    bool worn = s.equippedItem == id;
                    lbl.text = (worn ? "[x] " : "[  ] ") + (def != null ? def.name : id) +
                               (owned > 0 ? "  x" + owned : "") + (worn ? "  (worn)" : "");
                    lbl.color = worn ? UiTheme.Accent : (owned > 0 ? UiTheme.Text : UiTheme.TextDim);
                }
            }

            // creature side
            foreach (var b in creatureButtons) if (b != null) Destroy(b.gameObject);
            creatureButtons.Clear();
            for (int i = 0; i < s.party.Count; i++)
            {
                var c = s.party[i];
                string gear = (c.weapon != "" ? ItemDB.Name(c.weapon) : "no weapon") + " / " +
                              (c.armor != "" ? ItemDB.Name(c.armor) : "no armor");
                string label = (i == selectedIdx ? "▸ " : "") + c.DisplayName + "  Lv" + c.level + "\n" + gear;
                int idx = i;
                var btn = UIFactory.Button(creatureList, "c" + i, label, new Vector2(0, 1), new Vector2(240, 46),
                    () => Select(idx), 12);
                btn.transform.SetParent(creatureList, false);
                var rt = btn.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0, -6 - i * 52f);
                rt.sizeDelta = new Vector2(0, 46);
                if (i == selectedIdx)
                {
                    var lbl = btn.GetComponentInChildren<Text>();
                    if (lbl != null) lbl.color = UiTheme.Accent;
                }
                creatureButtons.Add(btn);
            }
            if (s.party.Count == 0)
                AddCreatureNote("(no creatures yet — tame some first)");

            // detail block
            if (selectedIdx >= 0 && selectedIdx < s.party.Count)
            {
                var c = s.party[selectedIdx];
                detail.text = "<b>" + c.DisplayName + "</b>  Lv" + c.level + "\n" +
                    "ATK " + c.ATK + "  DEF " + c.DEF + "  SPD " + c.SPD + "  HP " + c.curHP + "/" + c.MaxHP + "\n" +
                    "Weapon: " + (c.weapon != "" ? ItemDB.Name(c.weapon) : "—") +
                    "   Armor: " + (c.armor != "" ? ItemDB.Name(c.armor) : "—") + "\n" +
                    "Gear bonuses: +" + c.GearAtk() + " ATK, +" + c.GearDef() + " DEF, +" +
                    c.GearSpd() + " SPD, +" + c.GearHp() + " HP";
            }
            else
            {
                detail.text = "Select a creature, then equip a weapon or armor from your bag.\n" +
                              "Swapped gear returns to the bag automatically.";
            }
        }

        void AddCreatureNote(string text)
        {
            var btn = UIFactory.Button(creatureList, "note", text, new Vector2(0, 1), new Vector2(240, 40), null, 12);
            btn.transform.SetParent(creatureList, false);
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -6);
            rt.sizeDelta = new Vector2(0, 40);
            var lbl = btn.GetComponentInChildren<Text>();
            if (lbl != null) lbl.color = UiTheme.TextDim;
            creatureButtons.Add(btn);
        }

        void Select(int idx)
        {
            selectedIdx = idx;
            Refresh();
        }

        void ToggleTrainerGear(string itemId)
        {
            var s = Game.I.State;
            if (s.CountItem(itemId) <= 0 && s.equippedItem != itemId)
            {
                Toast.Push("You don't own the " + ItemDB.Name(itemId) + " yet.");
                return;
            }
            s.equippedItem = s.equippedItem == itemId ? "" : itemId;
            Sfx.I.Play("ui");
            Refresh();
        }

        void EquipFromPool(bool weapon)
        {
            var s = Game.I.State;
            if (selectedIdx < 0 || selectedIdx >= s.party.Count)
            {
                Toast.Push("Select a creature first.");
                return;
            }
            var c = s.party[selectedIdx];
            string[] pool = weapon ? ItemDB.CreatureWeapons : ItemDB.CreatureArmors;
            string current = weapon ? c.weapon : c.armor;

            // pick the best owned item not already worn
            string best = "";
            int bestScore = -1;
            foreach (var id in pool)
            {
                if (id == current || s.CountItem(id) <= 0) continue;
                int score = GearScore(id);
                if (score > bestScore) { bestScore = score; best = id; }
            }
            if (best == "")
            {
                Toast.Push(weapon ? "No spare weapons in the bag." : "No spare armor in the bag.");
                return;
            }

            ApplyEquip(best, weapon);
        }

        static int GearScore(string id)
        {
            var d = ItemDB.Get(id);
            if (d == null || string.IsNullOrEmpty(d.equipBonus)) return 0;
            int score = 0;
            foreach (var seg in d.equipBonus.Split('|'))
            {
                int v;
                if (seg.StartsWith("atk+") && int.TryParse(seg.Substring(4), out v)) score += v * 3;
                else if (seg.StartsWith("def+") && int.TryParse(seg.Substring(4), out v)) score += v * 3;
                else if (seg.StartsWith("hp+") && int.TryParse(seg.Substring(3), out v)) score += v;
                else if (seg.StartsWith("spd+") && int.TryParse(seg.Substring(4), out v)) score += v;
            }
            return score;
        }

        void UnequipSelected()
        {
            var s = Game.I.State;
            if (selectedIdx < 0 || selectedIdx >= s.party.Count)
            {
                Toast.Push("Select a creature first.");
                return;
            }
            var c = s.party[selectedIdx];
            if (c.weapon == "" && c.armor == "") { Toast.Push(c.DisplayName + " wears no gear."); return; }

            if (c.weapon != "") { s.AddItem(c.weapon, 1); c.weapon = ""; }
            if (c.armor != "") { s.AddItem(c.armor, 1); c.armor = ""; }
            Sfx.I.Play("ui");
            Toast.Push("Gear returned to the bag.");
            Refresh();
        }

        void ApplyEquip(string itemId, bool weapon)
        {
            var s = Game.I.State;
            var c = s.party[selectedIdx];
            string prev = weapon ? c.weapon : c.armor;

            if (!s.RemoveItem(itemId, 1)) { Toast.Push("None left in the bag."); return; }
            if (prev != "") s.AddItem(prev, 1); // swapped gear returns to the bag
            if (weapon) c.weapon = itemId; else c.armor = itemId;
            if (c.curHP > c.MaxHP) c.curHP = c.MaxHP; // armor HP bonus may have changed
            Sfx.I.Play("craft");
            Toast.Push(c.DisplayName + " equipped " + ItemDB.Name(itemId) + ".");
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
