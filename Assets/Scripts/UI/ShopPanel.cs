using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PokeProfessions.Core;
using PokeProfessions.World;
using PokeProfessions.Audio;

namespace PokeProfessions.UI
{
    /// <summary>Vale Post shop: buy listed stock, sell any sellable item.</summary>
    public class ShopPanel : MonoBehaviour
    {
        CanvasGroup cg;
        Text header, coins, detail;
        RectTransform list;
        readonly List<Button> rowButtons = new List<Button>();
        bool buying = true;
        string shopId = "";

        public static ShopPanel Build(Canvas canvas)
        {
            var go = new GameObject("ShopPanel", typeof(RectTransform), typeof(ShopPanel));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(canvas.transform, false);
            UIFactory.Stretch(rt);
            var p = go.GetComponent<ShopPanel>();
            p.Build();
            p.Show(false);
            return p;
        }

        void Build()
        {
            cg = gameObject.AddComponent<CanvasGroup>();

            var window = UIFactory.Box(transform, "window", UiTheme.Bg,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-380, -250), new Vector2(380, 250));

            header = UIFactory.TextAt(window, "header", "Shop", 19,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -34), new Vector2(-160, -8),
                TextAnchor.MiddleLeft);

            coins = UIFactory.TextAt(window, "coins", "", 15,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(-180, -32), new Vector2(-120, -8),
                TextAnchor.MiddleRight, UiTheme.Accent2);

            UIFactory.Button(window, "close", "X  [Esc]", new Vector2(1, 1), new Vector2(96, 30),
                () => { Show(false); if (UiRouter.I != null) UiRouter.I.ClosePanel(gameObject); }, 13, UiTheme.Panel);

            UIFactory.Button(window, "buyTab", "Buy", new Vector2(0.14f, 0), new Vector2(110, 34),
                () => { buying = true; Refresh(); }, 14);
            UIFactory.Button(window, "sellTab", "Sell", new Vector2(0.14f + 0.12f, 0), new Vector2(110, 34),
                () => { buying = false; Refresh(); }, 14);

            list = UIFactory.Box(window, "list", new Color(0, 0, 0, 0.15f),
                new Vector2(0, 0), new Vector2(0.45f, 1), new Vector2(10, 52), new Vector2(-6, -40));

            detail = UIFactory.TextAt(window, "detail", "", 14,
                new Vector2(0.45f, 0), new Vector2(1, 1), new Vector2(10, 56), new Vector2(-12, -40),
                TextAnchor.UpperLeft);
        }

        public void Open(string id)
        {
            shopId = id;
            ShopDef def;
            string name = "Shop";
            if (StoryData.Shops.TryGetValue(id, out def) && def != null) name = def.name;
            header.text = name;
            buying = true;
            Refresh();
            Show(true);
        }

        void Refresh()
        {
            foreach (var b in rowButtons) if (b != null) Destroy(b.gameObject);
            rowButtons.Clear();

            var s = Game.I.State;
            coins.text = s.coins + " coins";

            int row = 0;
            if (buying)
            {
                ShopDef def;
                if (StoryData.Shops.TryGetValue(shopId, out def) && def != null && def.stock != null)
                {
                    foreach (var entry in def.stock)
                    {
                        var parts = entry.Split(':');
                        string itemId = parts[0];
                        int count = parts.Length > 1 && int.TryParse(parts[1], out int c) ? c : -1;
                        var itemDef = ItemDB.Get(itemId);
                        if (itemDef == null) continue;

                        string label = itemDef.name + "  x" + (count < 0 ? "∞" : count.ToString()) + "  —  " + itemDef.basePrice + "c";
                        AddRow(label, () => Buy(itemId));
                        row++;
                    }
                }
            }
            else
            {
                foreach (var stack in s.inventory.ToArray())
                {
                    var def = ItemDB.Get(stack.itemId);
                    if (def == null || !def.sellable) continue;
                    string itemId = stack.itemId;
                    AddRow(def.name + " x" + stack.count + "  —  sells " + Mathf.Max(1, def.basePrice / 2) + "c",
                        () => Sell(itemId));
                    row++;
                }
            }

            if (row == 0) AddRow("(nothing here)", null);
            detail.text = buying
                ? "Click an item to buy.\nSelling pays half price.\nProfession gear and rare materials sell too."
                : "Click an item to sell one unit.\nKey items can't be sold.";
        }

        void AddRow(string label, System.Action onClick)
        {
            int i = rowButtons.Count;
            var btn = UIFactory.Button(list, "row" + i, label, new Vector2(0, 1), new Vector2(240, 34), onClick, 13);
            btn.transform.SetParent(list, false);
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -6 - i * 38f);
            rt.sizeDelta = new Vector2(0, 34);
            rowButtons.Add(btn);
        }

        void Buy(string itemId)
        {
            var s = Game.I.State;
            var def = ItemDB.Get(itemId);
            if (def == null) return;
            if (s.coins < def.basePrice) { Toast.Push("Not enough coins."); return; }
            s.coins -= def.basePrice;
            s.AddItem(itemId, 1);
            Sfx.I.Play("coin");
            Toast.Push("Bought " + def.name + ".");
            Refresh();
        }

        void Sell(string itemId)
        {
            var s = Game.I.State;
            var def = ItemDB.Get(itemId);
            if (def == null || !def.sellable) return;
            if (s.CountItem(itemId) <= 0) return;
            s.RemoveItem(itemId, 1);
            int gain = Mathf.Max(1, def.basePrice / 2);
            s.coins += gain;
            Sfx.I.Play("coin");
            Toast.Push("Sold " + def.name + " for " + gain + "c.");
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
