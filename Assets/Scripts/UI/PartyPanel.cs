using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PokeProfessions.Core;
using PokeProfessions.World;
using PokeProfessions.Audio;

namespace PokeProfessions.UI
{
    /// <summary>Party & box management: details, items, equipment.</summary>
    public class PartyPanel : MonoBehaviour
    {
        CanvasGroup cg;
        Text detail;
        RectTransform list;
        readonly List<Button> rowButtons = new List<Button>();
        int selectedIdx = -1;
        bool pendingEquip;
        string pendingItemId = "";

        public static PartyPanel Build(Canvas canvas)
        {
            var go = new GameObject("PartyPanel", typeof(RectTransform), typeof(PartyPanel));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(canvas.transform, false);
            UIFactory.Stretch(rt);
            var p = go.GetComponent<PartyPanel>();
            p.Build();
            p.Show(false);
            return p;
        }

        void Build()
        {
            cg = gameObject.AddComponent<CanvasGroup>();

            var window = UIFactory.Box(transform, "window", UiTheme.Bg,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-420, -260), new Vector2(420, 260));

            UIFactory.TextAt(window, "header", "Creatures", 20,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -36), new Vector2(-120, -8),
                TextAnchor.MiddleLeft);

            UIFactory.Button(window, "close", "X  [Esc]", new Vector2(1, 1), new Vector2(96, 30),
                () => { Show(false); if (UiRouter.I != null) UiRouter.I.ClosePanel(gameObject); }, 13, UiTheme.Panel);

            list = UIFactory.Box(window, "list", new Color(0, 0, 0, 0.15f),
                new Vector2(0, 0), new Vector2(0.48f, 1), new Vector2(10, 10), new Vector2(-6, -44));

            detail = UIFactory.TextAt(window, "detail", "", 14,
                new Vector2(0.48f, 0), new Vector2(1, 1), new Vector2(10, 100), new Vector2(-12, -44),
                TextAnchor.UpperLeft);

            UIFactory.Button(window, "useItem", "Use item", new Vector2(0.48f, 0), new Vector2(150, 36),
                OnUseItemClick, 14);
            UIFactory.Button(window, "heal", "Rest (free heal)", new Vector2(0.48f + 0.26f, 0), new Vector2(150, 36),
                () =>
                {
                    Game.I.State.HealAll();
                    Toast.Push("Your creatures rest and recover.");
                    Refresh();
                }, 14);
        }

        public void Open()
        {
            selectedIdx = -1;
            pendingEquip = false;
            pendingItemId = "";
            Refresh();
            Show(true);
        }

        void Refresh()
        {
            foreach (var b in rowButtons) if (b != null) Destroy(b.gameObject);
            rowButtons.Clear();

            var s = Game.I.State;
            for (int i = 0; i < Mathf.Max(s.party.Count, 1); i++)
            {
                string label;
                if (i < s.party.Count && s.party[i] != null)
                {
                    var c = s.party[i];
                    label = c.DisplayName + "  Lv" + c.level + "  " + c.curHP + "/" + c.MaxHP +
                            (c.shiny ? " ✨" : "") + (c.status != "" ? " [" + c.status + "]" : "");
                }
                else label = "(empty)";

                int idx = i;
                AddRow(label, () => Select(idx));
            }

            detail.text = selectedIdx >= 0 && selectedIdx < s.party.Count
                ? DetailText(s.party[selectedIdx])
                : "Select a creature.";

            pendingEquip = false;
        }

        void AddRow(string label, System.Action onClick)
        {
            int i = rowButtons.Count;
            var btn = UIFactory.Button(list, "p" + i, label, new Vector2(0, 1), new Vector2(240, 40), onClick, 13);
            btn.transform.SetParent(list, false);
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -6 - i * 44f);
            rt.sizeDelta = new Vector2(0, 40);
            rowButtons.Add(btn);
        }

        string DetailText(Creature c)
        {
            if (c == null) return "";
            var sb = new System.Text.StringBuilder();
            var sp = c.Species;
            sb.AppendLine("<b>" + c.DisplayName + (c.shiny ? " ✨ (shiny)" : "") + "</b>" + (sp != null ? "  —  " + sp.name : ""));
            if (sp != null) sb.AppendLine(sp.desc);
            sb.AppendLine("Level " + c.level + "   XP " + c.xp + "/" + BattleEngine.XpToNext(c.level));
            sb.AppendLine("HP " + c.curHP + "/" + c.MaxHP + "   ATK " + c.ATK + "   DEF " + c.DEF + "   SPD " + c.SPD);
            if (c.status != "") sb.AppendLine("Condition: " + c.status);
            sb.AppendLine();
            sb.AppendLine("Moves:");
            foreach (var m in c.moves)
            {
                var md = MoveDB.Get(m);
                int pp; c.movePP.TryGetValue(m, out pp);
                if (md != null)
                    sb.AppendLine("  " + md.name + " (" + md.element + ")" +
                        (md.power > 0 ? "  pow " + md.power : "") + "  " + Mathf.Max(0, pp) + "/" + md.pp);
            }
            return sb.ToString();
        }

        void Select(int idx)
        {
            selectedIdx = idx;
            Refresh();
        }

        void OnUseItemClick()
        {
            var s = Game.I.State;
            if (selectedIdx < 0 || selectedIdx >= s.party.Count)
            {
                Toast.Push("Select a creature first.");
                return;
            }

            // simple priority: best healing item available; equipment goes to pending state
            string[] healOrder = { "revive_tonic", "hearty_stew", "spice_bun", "salve", "trail_snack", "purge_draught" };
            foreach (var id in healOrder)
            {
                if (s.CountItem(id) > 0)
                {
                    var def = ItemDB.Get(id);
                    var c = s.party[selectedIdx];
                    if (def.healAmount >= 999 && c.curHP > 0) continue; // revive only on fainted
                    if (def.healAmount < 999 && (c.curHP <= 0 || c.curHP >= c.MaxHP) && def.conditionCure == 0) continue;

                    int healed = def.healAmount >= 999 ? c.MaxHP / 2 : def.healAmount;
                    if (c.curHP > 0) c.curHP = Mathf.Min(c.MaxHP, c.curHP + healed);
                    else if (def.healAmount >= 999) c.curHP = healed;
                    if (def.conditionCure > 0) { c.status = ""; c.statusTurns = 0; }
                    s.RemoveItem(id, 1);
                    Toast.Push("Used " + def.name + " on " + c.DisplayName + ".");
                    Refresh();
                    return;
                }
            }

            // equipment?
            string[] gear = { "forager_gloves", "miner_pick", "angler_rod", "lure_charm", "scout_band" };
            foreach (var id in gear)
            {
                if (s.CountItem(id) > 0)
                {
                    var def = ItemDB.Get(id);
                    s.equippedItem = s.equippedItem == id ? "" : id;
                    Toast.Push(s.equippedItem == id ? "Equipped " + def.name + "." : "Unequipped " + def.name + ".");
                    Refresh();
                    return;
                }
            }

            Toast.Push("Nothing usable in the bag.");
        }

        public void Show(bool on)
        {
            cg.alpha = on ? 1f : 0f;
            cg.blocksRaycasts = on;
            cg.interactable = on;
        }
    }
}
