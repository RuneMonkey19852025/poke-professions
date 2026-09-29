using UnityEngine;
using UnityEngine.UI;
using PokeProfessions.Core;

namespace PokeProfessions.UI
{
    /// <summary>Title screen with intro, continue / new game.</summary>
    public class TitlePanel : MonoBehaviour
    {
        public event System.Action OnStart;

        /// <summary>Set when the player chose New Game; UiRouter restarts the app fresh.</summary>
        public static bool PendingNewGame;

        CanvasGroup cg;
        Button continueBtn;

        public static TitlePanel Build(Canvas canvas)
        {
            var go = new GameObject("TitlePanel", typeof(RectTransform), typeof(TitlePanel));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(canvas.transform, false);
            UIFactory.Stretch(rt);
            var p = go.GetComponent<TitlePanel>();
            p.Build();
            p.Show(true);
            // @codebuff TEMP: env-var autostart for verification — REMOVE AFTER USE
            if (System.Environment.GetEnvironmentVariable("EMBERWILD_AUTOSTART") == "1")
                p.Invoke(nameof(p.StartGame), 1.5f);
            return p;
        }

        void Build()
        {
            cg = gameObject.AddComponent<CanvasGroup>();

            var bg = UIFactory.Panel(transform, "bg", new Color(0.06f, 0.09f, 0.13f, 0.96f));

            UIFactory.TextAt(bg, "title", "EMBERWILD VALE", 54,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-400, -180), new Vector2(400, -90),
                TextAnchor.MiddleCenter, UiTheme.Accent);

            UIFactory.TextAt(bg, "sub", "a creature-taming tale of professions & crafting", 20,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-400, -236), new Vector2(400, -196),
                TextAnchor.MiddleCenter, UiTheme.TextDim);

            UIFactory.TextAt(bg, "intro",
                "The Vale sleeps under a slow enchantment. Smoke has risen from Emberrock Ridge for three days, " +
                "old Pell hears the Hollow humming of nights, and a lantern in the ranger post refuses to go out — " +
                "it has chosen you. Learn the trades of the Vale, craft capture cores, befriend its creatures, " +
                "and wake what dreams beneath.",
                15,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-330, 40), new Vector2(330, 170),
                TextAnchor.UpperLeft, UiTheme.TextDim);

            bool hasSave = GameState.SaveExists();
            continueBtn = UIFactory.Button(bg, "continue", "Continue",
                new Vector2(0.5f, 0.5f), new Vector2(300, 48), () => StartGame(), 17, UiTheme.Accent);
            continueBtn.transform.SetParent(bg.transform, false);
            var crt = continueBtn.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 0.5f); crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = new Vector2(0, -60);
            continueBtn.gameObject.SetActive(hasSave);

            var newBtn = UIFactory.Button(bg, "new", hasSave ? "New Game (overwrites save)" : "Begin",
                new Vector2(0.5f, 0.5f), new Vector2(300, 48), () => StartNew(), 17, UiTheme.Panel);
            newBtn.transform.SetParent(bg.transform, false);
            var nrt = newBtn.GetComponent<RectTransform>();
            nrt.anchorMin = new Vector2(0.5f, 0.5f); nrt.anchorMax = new Vector2(0.5f, 0.5f);
            nrt.anchoredPosition = new Vector2(0, -120);

            UIFactory.TextAt(bg, "controls",
                "WASD move · E interact · J journal · C creatures · F5 save · Esc close menus",
                14,
                new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-400, 60), new Vector2(400, 90),
                TextAnchor.MiddleCenter, UiTheme.TextDim);
        }

        void StartGame()
        {
            if (OnStart != null) OnStart();
        }

        void StartNew()
        {
            GameState.DeleteSave();
            PendingNewGame = true;
            if (OnStart != null) OnStart();
        }

        public void Show(bool on)
        {
            cg.alpha = on ? 1f : 0f;
            cg.blocksRaycasts = on;
            cg.interactable = on;
        }
    }
}
