using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PokeProfessions.Core;
using PokeProfessions.World;

namespace PokeProfessions.UI
{
    /// <summary>Always-on HUD: party bars, coins, zone, prompts, toasts.</summary>
    public class HudPanel : MonoBehaviour
    {
        CanvasGroup cg;
        Text coinsText, zoneText, promptText;
        RectTransform toasts;
        readonly List<Text> toastLines = new List<Text>();
        readonly List<float> toastTimes = new List<float>();
        const float ToastLife = 4f;
        const float ToastBand = 26f;

        Text[] hpTexts = new Text[6];
        RectTransform[] hpFills = new RectTransform[6];
        RectTransform[] hpBgs = new RectTransform[6];
        Text[] xpTexts = new Text[6];
        RectTransform[] xpFills = new RectTransform[6];
        RectTransform[] xpBgs = new RectTransform[6];

        public static HudPanel Build(Canvas canvas)
        {
            var go = new GameObject("HUD", typeof(RectTransform), typeof(HudPanel));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(canvas.transform, false);
            UIFactory.Stretch(rt);

            var p = go.GetComponent<HudPanel>();
            p.BuildTopLeft();
            p.BuildParty();
            p.BuildPrompt();
            p.BuildToasts();
            p.BuildControls();
            p.Show(true);
            return p;
        }

        public void Show(bool on)
        {
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
            cg.alpha = on ? 1f : 0f;
            cg.blocksRaycasts = on;
            cg.interactable = on;
        }

        // ---------------- build ----------------

        void BuildTopLeft()
        {
            var box = UIFactory.BoxRounded(transform, "TopLeft", new Color(0.96f, 0.95f, 0.90f),
                new Color(0.16f, 0.20f, 0.35f), 4f,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -14), new Vector2(320, -110));

            coinsText = UIFactory.TextAt(box, "coins", "0 coins", 17,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -32), new Vector2(-10, -10),
                TextAnchor.MiddleLeft, new Color(0.12f, 0.13f, 0.18f));
            zoneText = UIFactory.TextAt(box, "zone", "Meadowfall", 15,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -58), new Vector2(-10, -36),
                TextAnchor.MiddleLeft, new Color(0.35f, 0.42f, 0.55f));
        }

        void BuildParty()
        {
            var box = UIFactory.Box(transform, "Party", new Color(0f, 0f, 0f, 0f),
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -116), new Vector2(240, -400));

            for (int i = 0; i < 6; i++)
            {
                float y = -i * 47f;
                var row = UIFactory.Box(box, "mon" + i, new Color(0f, 0f, 0f, 0f),
                    new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, y - 44), new Vector2(0, y));

                var nameT = UIFactory.TextAt(row, "name", "", 14,
                    new Vector2(0, 1), new Vector2(1, 1), new Vector2(2, -18), new Vector2(-4, -2),
                    TextAnchor.MiddleLeft);

                RectTransform hb, hf, xb, xf;
                UIFactory.FillBar(row, "hp", new Color(0, 0, 0, 0.55f), UiTheme.Hp, out hb, out hf);
                hb.anchorMin = new Vector2(0, 1); hb.anchorMax = new Vector2(1, 1);
                hb.offsetMin = new Vector2(2, -32); hb.offsetMax = new Vector2(-4, -20);
                RectTransform copyHb = hb; RectTransform copyHf = hf;

                UIFactory.FillBar(row, "xp", new Color(0, 0, 0, 0.45f), UiTheme.Xp, out xb, out xf);
                xb.anchorMin = new Vector2(0, 1); xb.anchorMax = new Vector2(1, 1);
                xb.offsetMin = new Vector2(2, -44); xb.offsetMax = new Vector2(-4, -34);

                hpTexts[i] = nameT;
                hpBgs[i] = hb; hpFills[i] = hf;
                xpBgs[i] = xb; xpFills[i] = xf;
            }
        }

        void BuildPrompt()
        {
            promptText = UIFactory.TextAt(transform, "prompt", "", 17,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-320, 64), new Vector2(320, 100),
                TextAnchor.MiddleCenter, UiTheme.Accent);
        }

        void BuildToasts()
        {
            toasts = UIFactory.Box(transform, "toasts", new Color(0, 0, 0, 0),
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(-460, -260), new Vector2(-14, -14));
            // container laid out bottom-up manually in RefreshToasts
        }

        void BuildControls()
        {
            var t = UIFactory.TextAt(transform, "controls",
                "WASD move · E interact · I bag · G gear · J journal · C creatures · F5 save · Esc close",
                13,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-420, 8), new Vector2(420, 26),
                TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.55f));
        }

        // ---------------- refresh ----------------

        void Update()
        {
            if (Game.I == null || Game.I.State == null) return;
            var s = Game.I.State;

            coinsText.text = s.coins + " coins";
            zoneText.text = ZoneLabel(s.curZone);

            // party bars
            for (int i = 0; i < 6; i++)
            {
                if (i < s.party.Count && s.party[i] != null)
                {
                    var c = s.party[i];
                    hpTexts[i].text = c.DisplayName + "  Lv" + c.level +
                        (c.shiny ? " ✨" : "") + (c.status != "" ? " [" + c.status + "]" : "");
                    float hpf = Mathf.Clamp01((float)c.curHP / Mathf.Max(1, c.MaxHP));
                    UIFactory.SetFill(hpBgs[i], hpFills[i], hpf);
                    var fillImg = hpFills[i].GetComponent<Image>();
                    if (fillImg != null) fillImg.color = hpf < 0.3f ? UiTheme.HpLow : UiTheme.Hp;

                    float xpf = Mathf.Clamp01((float)c.xp / BattleEngine.XpToNext(c.level));
                    UIFactory.SetFill(xpBgs[i], xpFills[i], xpf);
                }
                else
                {
                    hpTexts[i].text = "";
                    UIFactory.SetFill(hpBgs[i], hpFills[i], 0f);
                    UIFactory.SetFill(xpBgs[i], xpFills[i], 0f);
                }
            }

            RefreshPrompt();
            RefreshToasts();
        }

        void RefreshPrompt()
        {
            string text = "";
            var pc = FindObjectOfType<PlayerController>();
            if (pc != null && pc.NearestInteractable != null)
                text = "[E] " + pc.NearestInteractable.Prompt();

            if (promptText.text != text) promptText.text = text;
        }

        void RefreshToasts()
        {
            string msg;
            while (Toast.TryPop(out msg))
            {
                int slot = toastLines.Count;
                // each toast gets its own horizontal band, stacked downward
                var t = UIFactory.TextAt(toasts, "toast" + Time.frameCount, msg, 15,
                    new Vector2(0, 1), new Vector2(1, 1),
                    new Vector2(0, -ToastBand * (slot + 1)), new Vector2(0, -ToastBand * slot),
                    TextAnchor.MiddleRight, UiTheme.Text);
                toastLines.Add(t);
                toastTimes.Add(Time.time);
                if (toastLines.Count > 6)
                {
                    Destroy(toastLines[0].gameObject);
                    toastLines.RemoveAt(0);
                    toastTimes.RemoveAt(0);
                }
            }

            // expire the oldest once its lifetime is up
            while (toastLines.Count > 0 && Time.time - toastTimes[0] >= ToastLife)
            {
                Destroy(toastLines[0].gameObject);
                toastLines.RemoveAt(0);
                toastTimes.RemoveAt(0);
            }

            // reflow remaining toasts after any removal
            for (int i = 0; i < toastLines.Count; i++)
            {
                var rt = toastLines[i].rectTransform;
                rt.offsetMin = new Vector2(0, -ToastBand * (i + 1));
                rt.offsetMax = new Vector2(0, -ToastBand * i);
            }
        }

        static string ZoneLabel(string z)
        {
            switch (z)
            {
                case "meadow": return "Meadowfall";
                case "ridge": return "Emberrock Ridge";
                case "lake": return "Mirrorlake";
                case "hollow": return "Hushpine Hollow";
                case "hollow_deep": return "The Hollow (deep)";
            }
            return z;
        }
    }
}
