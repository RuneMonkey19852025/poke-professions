using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PokeProfessions.Core;
using PokeProfessions.World;
using PokeProfessions.Audio;

namespace PokeProfessions.UI
{
    /// <summary>
    /// "The Lucky Snorlax" — a coin-fed slot machine in Emberpost.
    /// 25c a spin, three reels of profession icons. Wins pay coins, and every
    /// winning spin also drops crafting materials; triple bars pay out rare
    /// materials. Built via UIFactory like every other panel.
    /// </summary>
    public class SlotsPanel : MonoBehaviour
    {
        // ---------------- pay table ----------------
        // symbol icons (drawn as text; glyphs chosen to render in Unity's legacy font)
        const string SymBerry = "o";   // berries — most common
        const string SymPick  = "/";   // pick — mine
        const string SymHammer = "H";  // hammer — smith
        const string SymStar  = "*";   // jackpot

        static readonly string[] ReelSymbols = { SymBerry, SymPick, SymHammer, SymStar };
        static readonly string[] SymNames = { "Berries", "Pick", "Hammer", "Star" };

        // weights per symbol on each reel (berries common, star rare)
        static readonly int[] Weights = { 6, 4, 3, 1 };

        // material payouts by symbol index
        static readonly string[] WinItems = { "fiber", "stone", "copper_bar", "moonshard" };
        static readonly int[] WinItemQty = { 2, 2, 1, 1 };

        // coin multipliers for pairs (two of a kind) by symbol index
        static readonly int[] PairPay = { 10, 15, 20, 40 };
        // coin multipliers for triples
        static readonly int[] TriplePay = { 60, 90, 150, 500 };

        const int SpinCost = 25;
        const int StartingPot = 150; // pot floor: reset value after any jackpot

        CanvasGroup cg;
        Text[] reels = new Text[3];
        Text resultText, coinsText, potText;
        Button spinButton;
        readonly int[] finalSymbols = new int[3];
        bool spinning;
        bool wonJackpot;

        public static SlotsPanel Build(Canvas canvas)
        {
            var go = new GameObject("SlotsPanel", typeof(RectTransform), typeof(SlotsPanel));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(canvas.transform, false);
            UIFactory.Stretch(rt);
            var p = go.GetComponent<SlotsPanel>();
            p.Build();
            p.Show(false);
            return p;
        }

        void Build()
        {
            cg = gameObject.AddComponent<CanvasGroup>();

            var window = UIFactory.Box(transform, "window", UiTheme.Bg,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-300, -250), new Vector2(300, 250));

            UIFactory.TextAt(window, "header", "The Lucky Snorlax", 19,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -36), new Vector2(-120, -8),
                TextAnchor.MiddleLeft, UiTheme.Accent2);

            coinsText = UIFactory.TextAt(window, "coins", "", 14,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(-180, -32), new Vector2(-128, -10),
                TextAnchor.MiddleRight, UiTheme.Accent2);

            UIFactory.Button(window, "close", "X  [Esc]", new Vector2(1, 1), new Vector2(96, 30),
                () => { Show(false); if (UiRouter.I != null) UiRouter.I.ClosePanel(gameObject); }, 13, UiTheme.Panel);

            // marquee
            UIFactory.TextAt(window, "marquee", "* 25c a spin · every win drops materials *", 12,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -58), new Vector2(-16, -38),
                TextAnchor.MiddleCenter, UiTheme.TextDim);

            // progressive jackpot readout (shared across both machines)
            potText = UIFactory.TextAt(window, "jackpot", "", 15,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-140, -62), new Vector2(140, -40),
                TextAnchor.MiddleCenter, UiTheme.Accent2);

            // three reels
            for (int i = 0; i < 3; i++)
            {
                var frame = UIFactory.BoxRounded(window, "reelFrame" + i, UiTheme.BgSoft, UiTheme.Line, 2f,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-190f + i * 130f, 10f), new Vector2(-70f + i * 130f, 120f));
                reels[i] = UIFactory.TextAt(frame, "reel" + i, "?", 40,
                    new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0), new Vector2(0, 0),
                    TextAnchor.MiddleCenter, UiTheme.Text);
                reels[i].raycastTarget = false;
            }

            // lever / spin button
            spinButton = UIFactory.Button(window, "spin", "SPIN  (25c)", new Vector2(0.5f, 0), new Vector2(190, 44),
                OnSpinClicked, 16, UiTheme.Accent);
            var spinRt = spinButton.GetComponent<RectTransform>();
            spinRt.anchoredPosition = new Vector2(0f, -66f);

            resultText = UIFactory.TextAt(window, "result", "Pull the lever!", 13,
                new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-280, -130), new Vector2(280, -116),
                TextAnchor.MiddleCenter);

            // pay table
            var pay = UIFactory.TextAt(window, "paytable", "", 11,
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(14, 8), new Vector2(-14, 66),
                TextAnchor.UpperLeft, UiTheme.TextDim);
            pay.text =
                "ooo  60c + fiber      ///  90c + stone\n" +
                "HHH  150c + copper bar   ***  500c + THE JACKPOT\n" +
                "any pair: 10–40c + a material · losses feed the pot";
        }

        public void Open()
        {
            spinning = false;
            Refresh(true);
            Show(true);
        }

        void Refresh(bool resetDisplay)
        {
            var s = Game.I.State;
            coinsText.text = s.coins + " coins";
            potText.text = "JACKPOT  " + s.jackpotPot + "c";
            spinButton.interactable = !spinning && s.coins >= SpinCost;
            if (resetDisplay)
            {
                for (int i = 0; i < 3; i++) reels[i].text = "?";
                resultText.text = "Pull the lever!";
            }
        }

        void OnSpinClicked()
        {
            if (spinning) return;
            var s = Game.I.State;
            if (s.coins < SpinCost) { Toast.Push("Not enough coins — win some at the Vale Post."); return; }

            s.coins -= SpinCost;
            wonJackpot = false;
            Sfx.I.Play("coin");
            spinning = true;

            // roll the outcome up front (weights bias toward berries; star is rare)
            for (int i = 0; i < 3; i++) finalSymbols[i] = RollSymbol();

            StartCoroutine(SpinRoutine());
        }

        static int RollSymbol()
        {
            int total = 0;
            foreach (var w in Weights) total += w;
            int roll = Random.Range(0, total);
            for (int i = 0; i < Weights.Length; i++)
            {
                roll -= Weights[i];
                if (roll < 0) return i;
            }
            return 0;
        }

        IEnumerator SpinRoutine()
        {
            Refresh(false);
            resultText.text = "…";

            // spin animation: symbols flicker, reels settle left to right
            float[] stopAt = { 0.9f, 1.4f, 1.9f };
            float t = 0f;
            while (t < stopAt[2])
            {
                t += Time.unscaledDeltaTime;
                for (int i = 0; i < 3; i++)
                {
                    reels[i].text = t >= stopAt[i]
                        ? ReelSymbols[finalSymbols[i]]
                        : ReelSymbols[Random.Range(0, ReelSymbols.Length)];
                }
                if (Random.value < 0.35f) Sfx.I.Play("ui");
                yield return null;
            }

            for (int i = 0; i < 3; i++) reels[i].text = ReelSymbols[finalSymbols[i]];
            Resolve();
        }

        void Resolve()
        {
            spinning = false;
            var s = Game.I.State;

            // ---- evaluate ----
            bool triple = finalSymbols[0] == finalSymbols[1] && finalSymbols[1] == finalSymbols[2];
            int matchSym = -1;
            if (finalSymbols[0] == finalSymbols[1] || finalSymbols[1] == finalSymbols[2] || finalSymbols[0] == finalSymbols[2])
            {
                // the symbol appearing twice wins the pair
                if (finalSymbols[0] == finalSymbols[1] || finalSymbols[0] == finalSymbols[2]) matchSym = finalSymbols[0];
                else matchSym = finalSymbols[1];
            }

            int coinsWon = 0;
            string itemWon = null;
            int itemQty = 0;

            if (triple)
            {
                coinsWon = TriplePay[finalSymbols[0]];
                itemWon = WinItems[finalSymbols[0]];
                itemQty = finalSymbols[0] == 3 ? 1 : WinItemQty[finalSymbols[0]] + 1;
            }
            else if (matchSym >= 0)
            {
                coinsWon = PairPay[matchSym];
                itemWon = WinItems[matchSym];
                itemQty = WinItemQty[matchSym];
            }

            // ---- pay out ----
            if (coinsWon > 0)
            {
                // the triple-star jackpot also sweeps the whole shared pot
                if (triple && finalSymbols[0] == 3)
                {
                    wonJackpot = true;
                    coinsWon += s.jackpotPot;
                    s.jackpotPot = StartingPot;
                }

                s.coins += coinsWon;
                if (itemWon != null) s.AddItem(itemWon, itemQty);
                Sfx.I.Play(wonJackpot ? "evolve" : triple ? "levelup" : "coin");

                if (wonJackpot)
                {
                    resultText.text = "<b>★ JACKPOT! ★</b>\n" +
                        "<color=#F2994A>+" + coinsWon + " coins</color>" +
                        (itemWon != null ? "  <color=#6FCF97>+" + itemQty + " " + ItemDB.Name(itemWon) + "</color>" : "") +
                        "\nThe pot resets to " + s.jackpotPot + "c — good luck rebuilding it.";
                }
                else
                {
                    resultText.text = (triple ? "<b>TRIPLE " + SymNames[finalSymbols[0]].ToUpper() + "!</b>\n" : "Pair of " + SymNames[matchSym] + "!\n") +
                        "<color=#F2994A>+" + coinsWon + " coins</color>" +
                        (itemWon != null ? "  <color=#6FCF97>+" + itemQty + " " + ItemDB.Name(itemWon) + "</color>" : "");
                }
            }
            else
            {
                // every losing spin feeds the shared progressive pot
                int feed = SpinCost - 5;
                s.jackpotPot += feed;
                resultText.text = "No luck — " + feed + "c rolls into the jackpot.";
                Sfx.I.Play("hit");
            }

            Refresh(false);
        }

        public void Show(bool on)
        {
            cg.alpha = on ? 1f : 0f;
            cg.blocksRaycasts = on;
            cg.interactable = on;
        }
    }
}
