using UnityEngine;
using UnityEngine.UI;
using PokeProfessions.Core;

namespace PokeProfessions.UI
{
    /// <summary>Bottom dialogue box; Advance with E/Space/Enter, choices with Y/N.</summary>
    public class DialoguePanel : MonoBehaviour
    {
        CanvasGroup cg;
        Text speaker, body, hint;
        Transform choices;
        int shownFrame = -1; // frame the current line was displayed on

        public static DialoguePanel Build(Canvas canvas)
        {
            var go = new GameObject("DialoguePanel", typeof(RectTransform), typeof(DialoguePanel));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(canvas.transform, false);
            UIFactory.Stretch(rt);

            var p = go.GetComponent<DialoguePanel>();
            p.Build();
            p.Show(false);
            return p;
        }

        void Build()
        {
            if (cg == null) { cg = gameObject.AddComponent<CanvasGroup>(); cg.blocksRaycasts = false; }

            // classic Pokémon frame: cream box, dark blue border, 2px white gap
            var box = UIFactory.BoxRounded(transform, "box", new Color(0.96f, 0.95f, 0.90f),
                new Color(0.16f, 0.20f, 0.35f), 5f,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-500, 22), new Vector2(500, 232));

            var gap = UIFactory.BoxRounded(box, "inner_gap", new Color(0.96f, 0.95f, 0.90f),
                new Color(0.45f, 0.55f, 0.75f), 3f,
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(10, 10), new Vector2(-10, -10));

            // nameplate tab overlapping the top edge
            var nameplate = UIFactory.BoxRounded(gap, "nameplate", new Color(0.35f, 0.55f, 0.85f),
                new Color(0.16f, 0.20f, 0.35f), 3f,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -6), new Vector2(216, 30));
            nameplate.offsetMin -= new Vector2(0, 0); // keep as tab

            speaker = UIFactory.TextAt(nameplate, "speaker", "", 16,
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(10, 2), new Vector2(-6, -2),
                TextAnchor.MiddleCenter, Color.white);

            body = UIFactory.TextAt(gap, "body", "", 17,
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(18, 12), new Vector2(-18, -34),
                TextAnchor.UpperLeft, new Color(0.12f, 0.13f, 0.18f));

            hint = UIFactory.TextAt(gap, "hint", "", 13,
                new Vector2(1, 0), new Vector2(1, 0), new Vector2(-190, 6), new Vector2(-14, 26),
                TextAnchor.MiddleRight, new Color(0.35f, 0.42f, 0.55f));

            choices = UIFactory.BoxRounded(gap, "choices", new Color(0, 0, 0, 0), new Color(0, 0, 0, 0), 0f,
                new Vector2(1, 0), new Vector2(1, 0), new Vector2(-280, 10), new Vector2(-12, 44));

            UIFactory.Button(choices, "yes", "YES",
                new Vector2(0, 0.5f), new Vector2(120, 32), () => Choose(true), 14, new Color(0.55f, 0.85f, 0.55f));

            UIFactory.Button(choices, "no", "NO",
                new Vector2(1, 0.5f), new Vector2(120, 32), () => Choose(false), 14, new Color(0.90f, 0.55f, 0.50f));
        }

        /// <summary>Defensive: rebuild any UI piece that failed to create at boot,
        /// so a partial Build can never leave dialogue permanently dead.</summary>
        void EnsureBuilt()
        {
            if (cg != null && speaker != null && body != null && hint != null && choices != null) return;

            // drop any partial widgets, then build fresh
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);
            Build();
        }

        public void ShowLine(DialogueLine line)
        {
            if (line == null) { Show(false); return; }
            EnsureBuilt();
            Show(true);
            shownFrame = Time.frameCount;
            speaker.text = line.speaker;
            body.text = line.text;

            bool choice = line.endRule == "choice";
            choices.gameObject.SetActive(choice);
            hint.text = choice ? "Y / N" : "E — continue";
        }

        public void Show(bool on)
        {
            cg.alpha = on ? 1f : 0f;
            cg.blocksRaycasts = on;
            cg.interactable = on;
        }

        void Update()
        {
            if (cg == null || cg.alpha < 0.5f) return;
            // ignore the same-frame key press that opened this line
            // (otherwise starting a talk with E instantly skips a line)
            if (Time.frameCount == shownFrame) return;
            var rt = UiRouter.I;
            if (rt == null) return;

            bool choiceMode = choices != null && choices.gameObject.activeSelf;
            if (choiceMode)
            {
                if (Input.GetKeyDown(KeyCode.Y)) Choose(true);
                if (Input.GetKeyDown(KeyCode.N)) Choose(false);
            }
            else if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                rt.AdvanceDialogue();
            }
        }

        void Choose(bool yes)
        {
            var rt = UiRouter.I;
            if (rt != null) rt.ChooseDialogue(yes);
        }
    }
}
