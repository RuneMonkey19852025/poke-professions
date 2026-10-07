using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PokeProfessions.Core;
using PokeProfessions.World;
using PokeProfessions.Audio;

namespace PokeProfessions.UI
{
    /// <summary>Crafting menu. Shows recipes available at the opened station.</summary>
    public class CraftingPanel : MonoBehaviour
    {
        CanvasGroup cg;
        Text header, detail;
        RectTransform list;      // the visible frame
        RectTransform content;   // the scrollable row column inside it
        ScrollRect scroll;
        readonly List<Button> rowButtons = new List<Button>();
        readonly List<Recipe> shown = new List<Recipe>();
        Recipe selected;
        string station = "";

        const float RowHeight = 34f, RowGap = 38f;

        public static CraftingPanel Build(Canvas canvas)
        {
            var go = new GameObject("CraftingPanel", typeof(RectTransform), typeof(CraftingPanel));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(canvas.transform, false);
            UIFactory.Stretch(rt);
            var p = go.GetComponent<CraftingPanel>();
            p.Build();
            p.Show(false);
            return p;
        }

        void Build()
        {
            cg = gameObject.AddComponent<CanvasGroup>();

            var window = UIFactory.Box(transform, "window", UiTheme.Bg,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-380, -250), new Vector2(380, 250));

            header = UIFactory.TextAt(window, "header", "Crafting", 19,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -34), new Vector2(-120, -8),
                TextAnchor.MiddleLeft);

            UIFactory.Button(window, "close", "X  [Esc]", new Vector2(1, 1), new Vector2(96, 30),
                () => { Show(false); if (UiRouter.I != null) UiRouter.I.ClosePanel(gameObject); }, 13, UiTheme.Panel);

            // A station's list can run past the window — the Weaponsmith alone
            // carries four kinds across five tiers — so the rows live in a
            // scroll view rather than being dropped off the bottom unreachable.
            list = UIFactory.Box(window, "list", new Color(0, 0, 0, 0.15f),
                new Vector2(0, 0), new Vector2(0.45f, 1), new Vector2(10, 10), new Vector2(-6, -40));
            list.gameObject.AddComponent<RectMask2D>();

            content = UIFactory.Box(list, "content", new Color(0, 0, 0, 0),
                new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0, 0);

            scroll = list.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = list;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;

            detail = UIFactory.TextAt(window, "detail", "", 14,
                new Vector2(0.45f, 0), new Vector2(1, 1), new Vector2(10, 46), new Vector2(-12, -40),
                TextAnchor.UpperLeft);

            UIFactory.Button(window, "craft", "Craft", new Vector2(0.73f, 0), new Vector2(170, 40),
                DoCraft, 15, UiTheme.Accent);
        }

        public void Open(string stationId)
        {
            station = stationId ?? "";
            header.text = "Crafting — " + StationTitle(station);
            selected = null;
            Refresh();
            Show(true);
        }

        static string StationTitle(string s)
        {
            if (s == "forge") return "Forge";
            if (s == "campfire") return "Campfire";
            if (s == "workbench") return "Workbench";
            if (s == "weaponsmith") return "Weaponsmith's Bench";
            if (s == "armorer") return "Armourer's Stand";
            if (s == "gear_table") return "Gearwright's Table";
            return "Field Crafting";
        }

        void Refresh()
        {
            var s = Game.I.State;
            shown.Clear();

            foreach (var r in ItemDB.Recipes)
            {
                bool stationOk = ProfessionSystem.StationAccepts(r.station, station);
                if (stationOk) shown.Add(r);
            }

            foreach (var b in rowButtons) if (b != null) Destroy(b.gameObject);
            rowButtons.Clear();

            for (int i = 0; i < shown.Count; i++)
            {
                var r = shown[i];
                var def = ItemDB.Get(r.resultItemId);
                string label = (def != null ? def.name : r.resultItemId) +
                               (def != null && def.rarity != Rarity.Normal ? "  [" + ItemRarity.Name(def.rarity) + "]" : "") +
                               "  " + ProfessionSystem.Name(r.profession) + " Lv" + r.requiredLevel;

                var btn = UIFactory.Button(content, "r" + i, label,
                    new Vector2(0, 1), new Vector2(240, RowHeight),
                    () => Select(r), 13);
                btn.transform.SetParent(content, false);
                var rt = btn.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0, -6 - i * RowGap);
                rt.sizeDelta = new Vector2(0, RowHeight);

                // tier colour, so the ladder is legible while scanning the list
                var lbl = btn.GetComponentInChildren<Text>();
                if (lbl != null)
                {
                    lbl.color = def != null ? UiTheme.Hex(ItemRarity.ColorHex(def.rarity)) : UiTheme.Text;
                    lbl.alignment = TextAnchor.MiddleLeft;
                }
                rowButtons.Add(btn);
            }

            content.sizeDelta = new Vector2(0, 12f + shown.Count * RowGap);
            if (scroll != null) scroll.verticalNormalizedPosition = 1f; // start at the top

            detail.text = selected != null ? DetailText(s, selected) : "Select a recipe.";
        }

        void Select(Recipe r)
        {
            selected = r;
            detail.text = DetailText(Game.I.State, r);
        }

        string DetailText(GameState s, Recipe r)
        {
            var def = ItemDB.Get(r.resultItemId);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<b>" + (def != null ? ItemRarity.RichName(def.id) : r.resultItemId) + " x" + r.resultCount + "</b>");
            if (def != null) sb.AppendLine(def.desc);
            if (def != null && def.rarity != Rarity.Normal)
                sb.AppendLine("<color=" + ItemRarity.ColorHex(def.rarity) + ">" + ItemRarity.Name(def.rarity) + " quality</color>");
            sb.AppendLine();
            sb.AppendLine("Profession: " + ProfessionSystem.Name(r.profession) + "  (you: Lv" + s.ProfLevel(r.profession) + ", needs Lv" + r.requiredLevel + ")");
            sb.AppendLine("Station: " + (string.IsNullOrEmpty(r.station) ? "anywhere" : StationTitle(r.station)));
            sb.AppendLine();
            sb.AppendLine("Inputs:");
            foreach (var input in r.inputs)
            {
                int have = s.CountItem(input.itemId);
                sb.AppendLine("  " + ItemDB.Name(input.itemId) + "  " + have + "/" + input.count);
            }
            sb.AppendLine();
            sb.AppendLine("Yields +" + r.xpReward + " " + ProfessionSystem.Name(r.profession) + " XP");
            sb.AppendLine();
            string blocker = ProfessionSystem.CraftBlocker(s, r, station);
            sb.AppendLine(string.IsNullOrEmpty(blocker) ? "<color=#6FCF97>Ready to craft</color>" : blocker);
            return sb.ToString();
        }

        void DoCraft()
        {
            if (selected == null) return;
            var s = Game.I.State;
            if (!ProfessionSystem.CanCraft(s, selected, station))
            {
                Toast.Push(ProfessionSystem.CraftBlocker(s, selected, station));
                return;
            }

            foreach (var input in selected.inputs) s.RemoveItem(input.itemId, input.count);
            s.AddItem(selected.resultItemId, selected.resultCount);
            ProfessionSystem.AddXp(s, selected.profession, selected.xpReward);
            s.SetFlag("craft_" + selected.resultItemId);
            if (Sfx.I != null) Sfx.I.Play("craft");
            Toast.Push("Crafted " + selected.resultCount + "x " + ItemDB.Name(selected.resultItemId));

            QuestSystem.AutoCompleteAll(s);
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
