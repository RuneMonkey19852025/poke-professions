using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PokeProfessions.Core;
using PokeProfessions.World;
using PokeProfessions.Audio;

namespace PokeProfessions.UI
{
    /// <summary>
    /// The bag: browse every item in the inventory, inspect it, and use it —
    /// on a party creature (heals/cures), on yourself (trainer gear), or as
    /// a capture core view. Key: I.
    /// </summary>
    public class InventoryPanel : MonoBehaviour
    {
        CanvasGroup cg;
        Text header, detail, coinsLabel;
        RectTransform list;
        readonly List<Button> rowButtons = new List<Button>();
        readonly List<ItemStack> shown = new List<ItemStack>();
        int selectedIdx = -1;

        // ---- "use on creature..." flow ----
        CanvasGroup pickerCg;
        RectTransform pickerList;
        readonly List<Button> pickerButtons = new List<Button>();
        string pendingItemId = "";

        public static InventoryPanel Build(Canvas canvas)
        {
            var go = new GameObject("InventoryPanel", typeof(RectTransform), typeof(InventoryPanel));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(canvas.transform, false);
            UIFactory.Stretch(rt);
            var p = go.GetComponent<InventoryPanel>();
            p.Build();
            p.Show(false);
            return p;
        }

        void Build()
        {
            cg = gameObject.AddComponent<CanvasGroup>();

            var window = UIFactory.Box(transform, "window", UiTheme.Bg,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-420, -260), new Vector2(420, 260));

            header = UIFactory.TextAt(window, "header", "Bag", 20,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -36), new Vector2(-160, -8),
                TextAnchor.MiddleLeft);

            coinsLabel = UIFactory.TextAt(window, "coins", "", 14,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(-230, -32), new Vector2(-128, -10),
                TextAnchor.MiddleRight, UiTheme.Accent2);

            UIFactory.Button(window, "close", "X  [Esc]", new Vector2(1, 1), new Vector2(96, 30),
                () => { Show(false); if (UiRouter.I != null) UiRouter.I.ClosePanel(gameObject); }, 13, UiTheme.Panel);

            list = UIFactory.Box(window, "list", new Color(0, 0, 0, 0.15f),
                new Vector2(0, 0), new Vector2(0.48f, 1), new Vector2(10, 10), new Vector2(-6, -44));

            detail = UIFactory.TextAt(window, "detail", "", 14,
                new Vector2(0.48f, 0), new Vector2(1, 1), new Vector2(10, 100), new Vector2(-12, -44),
                TextAnchor.UpperLeft);

            var useBtn = UIFactory.Button(window, "use", "Use", new Vector2(0.48f, 0), new Vector2(120, 36),
                OnUseClick, 14, UiTheme.Accent);
            var useRt = useBtn.GetComponent<RectTransform>();
            useRt.anchoredPosition = new Vector2(70f, 70f);
        }

        public void Open()
        {
            selectedIdx = -1;
            pendingItemId = "";
            HidePicker();
            Refresh();
            Show(true);
        }

        void Refresh()
        {
            var s = Game.I.State;
            coinsLabel.text = s.coins + " coins";

            shown.Clear();
            foreach (var stack in s.inventory)
                if (stack != null && !string.IsNullOrEmpty(stack.itemId) && stack.count > 0 && ItemDB.Get(stack.itemId) != null)
                    shown.Add(stack);

            // favorite items first, then by kind
            shown.Sort((a, b) =>
            {
                int ka = KindRank(ItemDB.Get(a.itemId)), kb = KindRank(ItemDB.Get(b.itemId));
                if (ka != kb) return ka.CompareTo(kb);
                int na = string.CompareOrdinal(ItemDB.Get(a.itemId).name, ItemDB.Get(b.itemId).name);
                return na;
            });

            foreach (var b in rowButtons) if (b != null) Destroy(b.gameObject);
            rowButtons.Clear();

            if (shown.Count == 0)
                AddRow("(bag is empty — gather & craft to fill it)", null, UiTheme.TextDim);

            for (int i = 0; i < shown.Count; i++)
            {
                var def = ItemDB.Get(shown[i].itemId);
                string tag = KindTag(def);
                var btn = AddRow((tag != "" ? "[" + tag + "] " : "") + def.name + "  x" + shown[i].count,
                    () => Select(i));
                // highlight the selected row's label
                if (i == selectedIdx)
                {
                    var lbl = btn.GetComponentInChildren<Text>();
                    if (lbl != null) lbl.color = UiTheme.Accent;
                }
            }

            detail.text = selectedIdx >= 0 && selectedIdx < shown.Count
                ? DetailText(s, shown[selectedIdx])
                : "Select an item.";
        }

        static int KindRank(ItemDef d)
        {
            if (d == null) return 9;
            switch (d.kind)
            {
                case ItemKind.Consumable: return 0; // heals & cures
                case ItemKind.Food: return 0;
                case ItemKind.CaptureCore: return 1;
                case ItemKind.Equipment: return 2;
                case ItemKind.KeyItem: return 3;
                default: return 4; // materials
            }
        }

        static string KindTag(ItemDef d)
        {
            if (d == null) return "";
            switch (d.kind)
            {
                case ItemKind.Consumable: return "use";
                case ItemKind.Food: return "use";
                case ItemKind.CaptureCore: return "core";
                case ItemKind.Equipment: return "gear";
                case ItemKind.KeyItem: return "key";
                default: return "";
            }
        }

        Button AddRow(string label, System.Action onClick, Color? color = null)
        {
            int i = rowButtons.Count;
            var btn = UIFactory.Button(list, "i" + i, label, new Vector2(0, 1), new Vector2(240, 32), onClick, 13);
            btn.transform.SetParent(list, false);
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -4 - i * 36f);
            rt.sizeDelta = new Vector2(0, 32);
            if (color.HasValue)
            {
                var lbl = btn.GetComponentInChildren<Text>();
                if (lbl != null) lbl.color = color.Value;
            }
            rowButtons.Add(btn);
            return btn;
        }

        string DetailText(GameState s, ItemStack stack)
        {
            var d = ItemDB.Get(stack.itemId);
            if (d == null) return "";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<b>" + d.name + "  x" + stack.count + "</b>");
            sb.AppendLine(d.desc);
            sb.AppendLine();

            switch (d.kind)
            {
                case ItemKind.Consumable:
                case ItemKind.Food:
                    sb.AppendLine(d.healAmount >= 999 ? "Revives a fainted creature."
                        : "Restores " + d.healAmount + " HP." + (d.conditionCure > 0 ? " Cures status." : ""));
                    sb.AppendLine("<color=#9FB0C0>Use: pick a party creature.</color>");
                    break;
                case ItemKind.CaptureCore:
                    sb.AppendLine("Throw it in battle (BAG → this core) to weaken-and-catch.");
                    sb.AppendLine("Catch bonus x" + d.catchBonus + ".");
                    break;
                case ItemKind.Equipment:
                    sb.AppendLine(IsCreatureGear(stack.itemId)
                        ? "Creature gear — equip it from the Creatures panel (C), or the Gear panel (G)."
                        : "Trainer gear — press Use to equip it (one at a time).");
                    break;
                case ItemKind.KeyItem:
                    sb.AppendLine("Story item. Cannot be used here.");
                    break;
                default:
                    sb.AppendLine("Crafting material. Used at stations and recipes.");
                    break;
            }
            return sb.ToString();
        }

        static bool IsCreatureGear(string id)
        {
            foreach (var g in ItemDB.CreatureWeapons) if (g == id) return true;
            foreach (var g in ItemDB.CreatureArmors) if (g == id) return true;
            return false;
        }

        void Select(int idx)
        {
            selectedIdx = idx;
            Refresh();
        }

        // ---------------- usage ----------------

        void OnUseClick()
        {
            if (selectedIdx < 0 || selectedIdx >= shown.Count) { Toast.Push("Select an item first."); return; }
            var d = ItemDB.Get(shown[selectedIdx].itemId);
            if (d == null) return;

            switch (d.kind)
            {
                case ItemKind.Consumable:
                case ItemKind.Food:
                    OpenTargetPicker(d.id);
                    break;
                case ItemKind.Equipment:
                    UseEquipment(d);
                    break;
                case ItemKind.CaptureCore:
                    Toast.Push("Cores are thrown from the battle BAG menu.");
                    break;
                case ItemKind.KeyItem:
                    Toast.Push("The " + d.name + " hums quietly. Not now.");
                    break;
                default:
                    Toast.Push(d.name + " is a material — use it in a recipe.");
                    break;
            }
        }

        void UseEquipment(ItemDef d)
        {
            var s = Game.I.State;
            if (IsCreatureGear(d.id))
            {
                Toast.Push("Creature gear: open Creatures (C) or Gear (G) to equip it.");
                return;
            }

            // trainer gear toggle via GameState.equippedItem
            s.equippedItem = s.equippedItem == d.id ? "" : d.id;
            Sfx.I.Play("ui");
            Toast.Push(s.equippedItem == d.id ? "Equipped " + d.name + "." : "Unequipped " + d.name + ".");
            Refresh();
        }

        // ---------------- creature target picker ----------------

        void OpenTargetPicker(string itemId)
        {
            var s = Game.I.State;
            if (s.party.Count == 0) { Toast.Push("You have no creatures yet."); return; }

            pendingItemId = itemId;
            BuildPicker();
            pickerCg.alpha = 1f;
            pickerCg.blocksRaycasts = true;
            pickerCg.interactable = true;
        }

        void EnsurePicker()
        {
            if (pickerCg != null) return;
            var win = UIFactory.Box(transform, "targetPicker", UiTheme.Bg,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-190, -160), new Vector2(190, 160));
            pickerCg = win.gameObject.AddComponent<CanvasGroup>();

            UIFactory.TextAt(win, "pickerHeader", "Use on…", 16,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -28), new Vector2(-52, -4),
                TextAnchor.MiddleLeft);
            UIFactory.Button(win, "pickerClose", "X", new Vector2(1, 1), new Vector2(40, 26),
                HidePicker, 12, UiTheme.Panel);

            pickerList = UIFactory.Box(win, "pickerList", new Color(0, 0, 0, 0.12f),
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(8, 8), new Vector2(-8, -36));
        }

        void BuildPicker()
        {
            EnsurePicker();
            foreach (var b in pickerButtons) if (b != null) Destroy(b.gameObject);
            pickerButtons.Clear();

            var s = Game.I.State;
            for (int i = 0; i < s.party.Count; i++)
            {
                var c = s.party[i];
                string label = c.DisplayName + "  " + c.curHP + "/" + c.MaxHP +
                               (c.status != "" ? "  [" + c.status + "]" : "");
                int idx = i;
                AddPickerRow(label, () => UseOnTarget(idx));
            }
        }

        void AddPickerRow(string label, System.Action onClick)
        {
            int i = pickerButtons.Count;
            var btn = UIFactory.Button(pickerList, "t" + i, label, new Vector2(0, 1), new Vector2(330, 34),
                onClick, 13);
            btn.transform.SetParent(pickerList, false);
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -4 - i * 38f);
            rt.sizeDelta = new Vector2(0, 34);
            pickerButtons.Add(btn);
        }

        void UseOnTarget(int partyIdx)
        {
            var s = Game.I.State;
            var d = ItemDB.Get(pendingItemId);
            var c = s.party[partyIdx];
            if (d == null || c == null) { HidePicker(); return; }

            // revive: only on fainted
            if (d.healAmount >= 999)
            {
                if (c.curHP > 0) { Toast.Push(c.DisplayName + " isn't fainted."); return; }
                c.curHP = c.MaxHP / 2;
            }
            else if (d.healAmount > 0)
            {
                if (c.curHP <= 0) { Toast.Push(c.DisplayName + " has fainted — use a Revive Tonic."); return; }
                if (c.curHP >= c.MaxHP && d.conditionCure == 0)
                { Toast.Push(c.DisplayName + " is already at full HP."); return; }
                c.curHP = Mathf.Min(c.MaxHP, c.curHP + d.healAmount);
            }

            if (d.conditionCure > 0) { c.status = ""; c.statusTurns = 0; }

            s.RemoveItem(d.id, 1);
            Sfx.I.Play("gather");
            Toast.Push("Used " + d.name + " on " + c.DisplayName + ".");
            HidePicker();
            Refresh();
        }

        void HidePicker()
        {
            if (pickerCg == null) return;
            pickerCg.alpha = 0f;
            pickerCg.blocksRaycasts = false;
            pickerCg.interactable = false;
        }

        public void Show(bool on)
        {
            if (!on) HidePicker();
            cg.alpha = on ? 1f : 0f;
            cg.blocksRaycasts = on;
            cg.interactable = on;
        }
    }
}
