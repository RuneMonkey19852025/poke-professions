using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PokeProfessions.Core;
using PokeProfessions.World;
using PokeProfessions.Audio;

namespace PokeProfessions.UI
{
    /// <summary>Quest journal + profession summary + bestiary stats.</summary>
    public class JournalPanel : MonoBehaviour
    {
        CanvasGroup cg;
        Text questsText, profText, bestText;

        public static JournalPanel Build(Canvas canvas)
        {
            var go = new GameObject("JournalPanel", typeof(RectTransform), typeof(JournalPanel));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(canvas.transform, false);
            UIFactory.Stretch(rt);
            var p = go.GetComponent<JournalPanel>();
            p.Build();
            p.Show(false);
            return p;
        }

        void Build()
        {
            cg = gameObject.AddComponent<CanvasGroup>();

            var window = UIFactory.Box(transform, "window", UiTheme.Bg,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-420, -260), new Vector2(420, 260));

            UIFactory.TextAt(window, "header", "Journal", 20,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -36), new Vector2(-120, -8),
                TextAnchor.MiddleLeft);

            UIFactory.Button(window, "close", "X  [Esc]", new Vector2(1, 1), new Vector2(96, 30),
                () => { Show(false); if (UiRouter.I != null) UiRouter.I.ClosePanel(gameObject); }, 13, UiTheme.Panel);

            questsText = UIFactory.TextAt(window, "quests", "", 14,
                new Vector2(0, 0), new Vector2(0.62f, 1), new Vector2(14, 14), new Vector2(-8, -44),
                TextAnchor.UpperLeft);

            profText = UIFactory.TextAt(window, "professions", "", 14,
                new Vector2(0.62f, 0), new Vector2(1, 1), new Vector2(10, 200), new Vector2(-14, -44),
                TextAnchor.UpperLeft);

            bestText = UIFactory.TextAt(window, "bestiary", "", 14,
                new Vector2(0.62f, 0), new Vector2(1, 1), new Vector2(10, 60), new Vector2(-14, -180),
                TextAnchor.UpperLeft, UiTheme.TextDim);
        }

        public void Open()
        {
            Refresh();
            Show(true);
        }

        void Refresh()
        {
            var s = Game.I.State;
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("<b>— Active quests —</b>");
            if (s.activeQuests.Count == 0) sb.AppendLine("None. Talk to the folks of the Vale.");
            foreach (var qid in s.activeQuests)
            {
                var q = QuestSystem.Get(qid);
                if (q == null) continue;
                sb.AppendLine();
                sb.AppendLine("<b>" + q.name + "</b>  (Act " + q.act + ")");
                sb.AppendLine(q.summary);
                foreach (var obj in q.objectives)
                {
                    string mark = QuestSystem.IsObjectiveMet(s, obj) ? "[x]" : "[ ]";
                    sb.AppendLine("  " + mark + " " + QuestSystem.ObjectiveText(s, obj));
                }
            }

            sb.AppendLine();
            sb.AppendLine("<b>— Completed —</b>");
            if (s.doneQuests.Count == 0) sb.AppendLine("None yet.");
            foreach (var qid in s.doneQuests)
            {
                var q = QuestSystem.Get(qid);
                if (q != null) sb.AppendLine("✔ " + q.name);
            }

            questsText.text = sb.ToString();

            var pb = new System.Text.StringBuilder();
            pb.AppendLine("<b>— Professions —</b>");
            foreach (var p in ProfessionSystem.All)
            {
                int lvl = s.ProfLevel(p);
                int xp = s.ProfXp(p);
                int need = ProfessionSystem.XpToNext(lvl);
                pb.AppendLine(ProfessionSystem.Icon(p) + " " + ProfessionSystem.Name(p) +
                    (lvl >= ProfessionSystem.MaxLevel ? "  Lv MAX" : "  Lv " + lvl + "  (" + xp + "/" + need + ")"));
            }
            if (!string.IsNullOrEmpty(s.equippedItem))
                pb.AppendLine();
            if (!string.IsNullOrEmpty(s.equippedItem))
                pb.AppendLine("Equipped: " + ItemDB.Name(s.equippedItem));
            profText.text = pb.ToString();

            bestText.text = "Creatures caught: " + s.catchCount +
                            " · Battles won: " + s.battleWins +
                            "\nSpecies recorded: " + s.caughtSpecies.Count + " / " + CreatureDB.Order.Count +
                            "\n\n<b>— Weapon mastery —</b>\n" + MasteryLines(s);
        }

        public void Show(bool on)
        {
            cg.alpha = on ? 1f : 0f;
            cg.blocksRaycasts = on;
            cg.interactable = on;
        }

        static string MasteryLines(GameState s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (WeaponClass wc in System.Enum.GetValues(typeof(WeaponClass)))
            {
                if (wc == WeaponClass.None) continue;
                int lvl = WeaponMastery.Level(s, wc);
                int need = WeaponMastery.XpToNext(lvl);
                bool isArmor = wc == WeaponClass.Plates || wc == WeaponClass.Charms;
                string effect = isArmor
                    ? "−" + WeaponMastery.DefenseReducePercent(s, wc) + "% dmg taken"
                    : "+" + Mathf.RoundToInt((WeaponMastery.AttackMultiplier(s, wc) - 1f) * 100f) + "% ATK";
                sb.AppendLine(WeaponMastery.Name(wc) + ": Lv" + lvl +
                    (lvl >= WeaponMastery.MaxLevel ? " MAX" : "  (" + WeaponMastery.Xp(s, wc) + "/" + need + ")") +
                    "  " + effect);
            }
            return sb.ToString();
        }
    }
}
