using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PokeProfessions.Core;
using PokeProfessions.World;
using PokeProfessions.Audio;

namespace PokeProfessions.UI
{
    /// <summary>
    /// Overlay battle UI. Spawns both creatures as procedural models in the
    /// world in front of the player, with classic-style HP cards and menus.
    /// </summary>
    public class BattlePanel : MonoBehaviour
    {
        public event System.Action OnBattleClosed;

        CanvasGroup cg;
        Text enemyName, enemyStatus, playerName, playerStatus, logText, titleText;
        RectTransform enemyHpBg, enemyHpFill, playerHpBg, playerHpFill, playerXpBg, playerXpFill;
        RectTransform menuBox;
        Button endButton;

        BattleEngine engine;
        Transform stage;
        GameObject enemyModel, playerModel;
        CreatureVisual enemyCv, playerCv;

        // Where each side stands on the stage, in stage-local space, plus the
        // creature the stage was built for — what RefreshStage compares against to
        // notice that the active creature changed underneath the models.
        Vector3 playerLocalPos, enemyLocalPos;
        Quaternion playerLocalRot = Quaternion.identity, enemyLocalRot = Quaternion.identity;
        Creature stagedPlayer, stagedEnemy;

        /// <summary>The instantiated bodies the stage is showing, or null between battles.</summary>
        public GameObject PlayerModel => playerModel;
        public GameObject EnemyModel => enemyModel;
        readonly List<string> logLines = new List<string>();
        bool ended;

        // ---- keyboard menu navigation (battle is fully playable without a mouse) ----
        struct MenuEntry
        {
            public Button btn; public Image img; public Text label;
            public Color bg, fg; public string text;
        }
        readonly List<MenuEntry> menuEntries = new List<MenuEntry>();
        int menuIndex;
        System.Action menuBack;   // submenu -> parent menu (Esc / Backspace)

        public static BattlePanel Build(Canvas canvas)
        {
            var go = new GameObject("BattlePanel", typeof(RectTransform), typeof(BattlePanel));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(canvas.transform, false);
            UIFactory.Stretch(rt);
            var p = go.GetComponent<BattlePanel>();
            p.Build();
            p.Show(false);
            return p;
        }

        void Build()
        {
            cg = gameObject.AddComponent<CanvasGroup>();

            var dim = UIFactory.Panel(transform, "dim", new Color(0f, 0f, 0f, 0.25f));

            // ---- enemy card (top left): Pokémon-style rounded card ----
            var enemyCard = UIFactory.BoxRounded(transform, "enemyCard", new Color(0.96f, 0.95f, 0.90f),
                new Color(0.16f, 0.20f, 0.35f), 4f,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -110), new Vector2(440, -26));
            titleText = UIFactory.TextAt(enemyCard, "title", "", 14,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(14, -22), new Vector2(-12, -6),
                TextAnchor.MiddleLeft, new Color(0.35f, 0.42f, 0.55f));
            enemyName = UIFactory.TextAt(enemyCard, "name", "", 19,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(14, -48), new Vector2(200, -24),
                TextAnchor.MiddleLeft, new Color(0.12f, 0.13f, 0.18f));
            enemyStatus = UIFactory.TextAt(enemyCard, "status", "", 14,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(-150, -48), new Vector2(-12, -24),
                TextAnchor.MiddleRight, new Color(0.85f, 0.45f, 0.25f));
            RectTransform b1, f1;
            UIFactory.FillBar(enemyCard, "hp", new Color(0.55f, 0.55f, 0.58f), new Color(0.30f, 0.80f, 0.35f), out b1, out f1);
            b1.anchorMin = new Vector2(0, 1); b1.anchorMax = new Vector2(1, 1);
            b1.offsetMin = new Vector2(14, -42); b1.offsetMax = new Vector2(-14, -30);
            // HP chip: small "HP" label left of the bar
            var hpChip1 = UIFactory.TextAt(enemyCard, "hpLabel", "HP", 12,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -58), new Vector2(40, -44),
                TextAnchor.MiddleRight, new Color(0.20f, 0.55f, 0.25f));
            enemyHpBg = b1; enemyHpFill = f1;

            // ---- player card (bottom right) ----
            var playerCard = UIFactory.BoxRounded(transform, "playerCard", new Color(0.96f, 0.95f, 0.90f),
                new Color(0.16f, 0.20f, 0.35f), 4f,
                new Vector2(1, 0), new Vector2(1, 0), new Vector2(-440, 150), new Vector2(-24, 240));
            playerName = UIFactory.TextAt(playerCard, "name", "", 19,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(14, -30), new Vector2(200, -8),
                TextAnchor.MiddleLeft, new Color(0.12f, 0.13f, 0.18f));
            playerStatus = UIFactory.TextAt(playerCard, "status", "", 14,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(-150, -30), new Vector2(-12, -8),
                TextAnchor.MiddleRight, new Color(0.85f, 0.45f, 0.25f));
            RectTransform b2, f2, b3, f3;
            UIFactory.FillBar(playerCard, "hp", new Color(0.55f, 0.55f, 0.58f), new Color(0.30f, 0.80f, 0.35f), out b2, out f2);
            b2.anchorMin = new Vector2(0, 1); b2.anchorMax = new Vector2(1, 1);
            b2.offsetMin = new Vector2(14, -48); b2.offsetMax = new Vector2(-14, -36);
            var hpChip2 = UIFactory.TextAt(playerCard, "hpLabel", "HP", 12,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -64), new Vector2(40, -50),
                TextAnchor.MiddleRight, new Color(0.20f, 0.55f, 0.25f));
            playerHpBg = b2; playerHpFill = f2;
            UIFactory.FillBar(playerCard, "xp", new Color(0.55f, 0.55f, 0.58f), new Color(0.25f, 0.55f, 0.95f), out b3, out f3);
            b3.anchorMin = new Vector2(0, 1); b3.anchorMax = new Vector2(1, 1);
            b3.offsetMin = new Vector2(14, -64); b3.offsetMax = new Vector2(-14, -58);
            playerXpBg = b3; playerXpFill = f3;

            // ---- log (bottom left) ----
            // ---- log (bottom left): Pokémon text box ----
            var logBox = UIFactory.BoxRounded(transform, "logBox", new Color(0.96f, 0.95f, 0.90f),
                new Color(0.16f, 0.20f, 0.35f), 4f,
                new Vector2(0, 0), new Vector2(0, 0), new Vector2(24, 26), new Vector2(560, 216));
            logText = UIFactory.TextAt(logBox, "log", "", 15,
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(14, 10), new Vector2(-14, -10),
                TextAnchor.LowerLeft, new Color(0.12f, 0.13f, 0.18f));

            // ---- menu (bottom right) ----
            menuBox = UIFactory.BoxRounded(transform, "menuBox", new Color(0.90f, 0.93f, 0.98f),
                new Color(0.16f, 0.20f, 0.35f), 4f,
                new Vector2(1, 0), new Vector2(1, 0), new Vector2(-440, 24), new Vector2(-24, 240));

            endButton = UIFactory.Button(menuBox, "end", "Continue  [E]", new Vector2(0.5f, 0.35f),
                new Vector2(280, 44), () => Finish(), 17, UiTheme.Accent);
            endButton.gameObject.SetActive(false);
        }

        // ---------------- lifecycle ----------------

        public void Begin(BattleEngine eng)
        {
            engine = eng.WithState(Game.I.State); // enable weapon mastery bonuses/XP
            ended = false;
            logLines.Clear();
            logText.text = "";

            engine.Log += OnLog;
            engine.SfxRequest += id => { if (Sfx.I != null) Sfx.I.Play(id); };
            engine.Evolved += c => Toast.Push(c.DisplayName + " evolved into " + c.Species.name + "!");

            BuildStage();

            string intro = engine.IsTrainerBattle && engine.Trainer != null
                ? engine.Trainer.title + " " + engine.Trainer.name + " wants to battle!"
                : "A wild " + (engine.EnemyMon != null ? engine.EnemyMon.DisplayName : "creature") + " appeared!";
            OnLog(intro);
            if (engine.IsTrainerBattle && engine.Trainer != null && engine.Trainer.preBattle.Length > 0)
                OnLog(engine.Trainer.preBattle[0]);

            Show(true);
            ShowMenu();
            RefreshCards();
        }

        void BuildStage()
        {
            ClearStage();
            var pc = FindObjectOfType<PlayerController>();
            if (pc == null || engine == null || engine.EnemyMon == null) return;

            Vector3 fwd = pc.transform.forward; fwd.y = 0; fwd.Normalize();
            Vector3 anchor = pc.transform.position + fwd * 5.5f;
            anchor.y = WorldBuilder.GroundHeight(anchor.x, anchor.z);

            stage = new GameObject("BattleStage").transform;
            stage.position = anchor;

            var em = engine.EnemyMon;
            if (em != null && em.Species != null)
            {
                enemyModel = new GameObject("enemyModel");
                enemyModel.transform.SetParent(stage, false);
                enemyModel.transform.position = anchor + new Vector3(0, 0, 0);
                AddBattlePlatform(stage, anchor, 2.6f);
                var vis = CreatureVisual.Build(em.Species, em.shiny, enemyModel.transform);
                enemyCv = vis;
                vis.transform.localPosition = Vector3.zero;
                enemyModel.transform.rotation = Quaternion.LookRotation(-fwd);

                enemyLocalPos = enemyModel.transform.localPosition;
                enemyLocalRot = enemyModel.transform.localRotation;
                stagedEnemy = em;
                PlaySendOut(enemyCv);
            }

            var pm = engine.PlayerMon;
            if (pm != null && pm.Species != null)
            {
                Vector3 right = Vector3.Cross(Vector3.up, fwd);
                playerModel = new GameObject("playerModel");
                playerModel.transform.SetParent(stage, false);
                playerModel.transform.position = pc.transform.position + right * 2.2f;
                playerModel.transform.position = new Vector3(playerModel.transform.position.x,
                    WorldBuilder.GroundHeight(playerModel.transform.position.x, playerModel.transform.position.z),
                    playerModel.transform.position.z);
                AddBattlePlatform(stage, playerModel.transform.position, 3.2f);
                var vis = CreatureVisual.Build(pm.Species, pm.shiny, playerModel.transform);
                playerCv = vis;
                vis.transform.localPosition = Vector3.zero;
                playerModel.transform.rotation = Quaternion.LookRotation(anchor - playerModel.transform.position);

                playerLocalPos = playerModel.transform.localPosition;
                playerLocalRot = playerModel.transform.localRotation;
                stagedPlayer = pm;
                PlaySendOut(playerCv);
            }
        }

        /// <summary>
        /// Put the active creature's body on the stage for whichever side has changed.
        /// </summary>
        /// <remarks>
        /// A SWITCH sets the engine's active index outright, and a faint advances it to
        /// the next creature that can fight (BattleEngine.HandleFaint), so the fight
        /// has to be able to show someone other than the creature the stage was built
        /// for. Without this the incoming creature attacks, flinches and faints as a
        /// body it no longer is, and a trainer's next send-out never appears at all.
        ///
        /// Only the side that changed is rebuilt: the other keeps its pose, its facing
        /// and its platform, so a swap reads as one creature leaving rather than the
        /// whole arena popping.
        /// </remarks>
        public void RefreshStage()
        {
            if (stage == null || engine == null) return;   // no battle on stage

            var pm = engine.PlayerMon;
            var em = engine.EnemyMon;

            if (!ReferenceEquals(pm, stagedPlayer))
            {
                RebuildSide(pm, true);
                stagedPlayer = pm;
            }
            if (!ReferenceEquals(em, stagedEnemy))
            {
                RebuildSide(em, false);
                stagedEnemy = em;
            }
        }

        /// <summary>Replace one side's body in place, keeping its spot on the stage.</summary>
        void RebuildSide(Creature c, bool isPlayer)
        {
            // Nothing to put there (that side is out of creatures) — keep the body
            // already standing, so the last thing that happened stays on screen.
            if (c == null || c.Species == null) return;

            var old = isPlayer ? playerModel : enemyModel;
            if (old != null) Destroy(old);

            var go = new GameObject(isPlayer ? "playerModel" : "enemyModel");
            go.transform.SetParent(stage, false);
            go.transform.localPosition = isPlayer ? playerLocalPos : enemyLocalPos;
            go.transform.localRotation = isPlayer ? playerLocalRot : enemyLocalRot;

            var vis = CreatureVisual.Build(c.Species, c.shiny, go.transform);
            if (vis != null) vis.transform.localPosition = Vector3.zero;

            if (isPlayer) { playerModel = go; playerCv = vis; }
            else { enemyModel = go; enemyCv = vis; }

            // A replacement takes the field rather than blinking into existence.
            PlaySendOut(vis);
        }

        /// <summary>
        /// How long a battle beat holds before the creatures drop back to idle. The
        /// packs' attack clips run about 1.0–1.6 s, so this sits just past the
        /// shortest wind-up and still lets the long ones finish.
        /// </summary>
        public const float BeatSeconds = 1.2f;

        /// <summary>
        /// How long a follow-up act holds. The first act of a turn gets the full
        /// window — a swing needs its wind-up on screen — but an answer can be read
        /// faster, so an exchange is not simply twice as slow as a lone attack.
        /// </summary>
        public const float FollowUpSeconds = 0.8f;

        /// <summary>
        /// Release a beat's pose back to idle. The pose version captured up front
        /// is what makes this safe: if the creature has moved on since (a new swing
        /// started while this was waiting), the stale idle is dropped instead of
        /// landing on top of the new swing and cutting it short.
        /// </summary>
        System.Collections.IEnumerator ReturnToIdle(CreatureVisual cv)
        {
            return ReturnToIdle(cv, BeatSeconds);
        }

        /// <summary>Release a pose back to idle after a given hold, same guard.</summary>
        System.Collections.IEnumerator ReturnToIdle(CreatureVisual cv, float seconds)
        {
            if (cv == null) yield break;
            int version = cv.PoseVersion;
            yield return new WaitForSeconds(Mathf.Max(0.05f, seconds));
            if (cv != null && cv.PoseVersion == version) cv.PlayState("idle");
        }

        /// <summary>
        /// Bring a creature on with its pack's send-out pose, then let it settle.
        /// </summary>
        /// <remarks>
        /// The KayKit characters have a real send-out (Spawn_Ground / the skeletons'
        /// Skeletons_Spawn_Ground), and the creature packs arrive on their leap-into-idle
        /// clip. The hold comes from the clip itself because these range from 1.3 s
        /// to 3.6 s, and a fixed hold would cut the long ones off mid-landing.
        ///
        /// CreatureVisual.EnterField is what decides which of those a given creature
        /// gets, and it answers with the seconds to hold either way — so the packs the
        /// marketplace left with no arrival clip at all (the fish, the dragon, the
        /// wasps) take the field on a code-driven leap instead of standing there and
        /// blinking into existence.
        /// </remarks>
        void PlaySendOut(CreatureVisual cv)
        {
            if (cv == null) return;
            float len = cv.EnterField();
            if (len > 0.01f) StartCoroutine(ReturnToIdle(cv, len));
        }

        /// <summary>
        /// One battle beat: the attacker swings, the target reacts, and both are
        /// released back to idle when it is over.
        /// </summary>
        /// <remarks>
        /// The swing is keyed off the move being used, not off whether it hurt
        /// anything, because a miss, a KO and a mastery technique that deals no
        /// damage at all (Aegis Guard is a pure buff) are all still swings. Keying
        /// it — and the return to idle — off "did the target lose HP" is what used
        /// to leave the attacker clamped mid-swing and the target frozen in its hit
        /// pose for the rest of the fight, since nothing else ever asked for
        /// another state. ReplayState, not PlayState, so a second swing is a second
        /// swing rather than a no-op against the pose already showing.
        ///
        /// Public so the playtest can drive it with visuals of its own: the rule
        /// that matters here — a beat always ends on idle, even with no reaction —
        /// needs no battle to check.
        /// </remarks>
        public void StrikeBeat(CreatureVisual attacker, CreatureVisual target, string reaction)
        {
            if (attacker != null) attacker.ReplayState("attack");
            if (target != null && !string.IsNullOrEmpty(reaction)) target.ReplayState(reaction);

            if (attacker != null) StartCoroutine(ReturnToIdle(attacker));

            // A fainted creature holds its death pose: standing it back up 1.2 s
            // later would read as the KO being undone.
            if (target != null && !string.IsNullOrEmpty(reaction) && reaction != "death")
                StartCoroutine(ReturnToIdle(target));
        }

        /// <summary>
        /// How a target answers a beat: a death pose when the blow finished it, a
        /// flinch when it took damage, and nothing when it was untouched — a miss or
        /// a mastery buff like Aegis Guard deals no damage, and a creature does not
        /// flinch at a move that never reached it.
        /// </summary>
        public static string ReactionFor(int damage, bool fainted)
        {
            if (fainted) return "death";
            return damage > 0 ? "hit" : null;
        }

        /// <summary>
        /// Play out the turn the engine just resolved, then hand control back.
        /// </summary>
        /// <remarks>
        /// The engine records a Beat per executed action (see BattleEngine.Beat), and
        /// that is what makes a real exchange possible here: the player's swing and
        /// the enemy's answer are two beats, in the order they actually happened —
        /// the enemy's counterattack gets its own lunge and the player's creature
        /// flinches at it, instead of the whole turn collapsing into one pose on the
        /// player's side. Neither can be worked out from the HP bars afterwards,
        /// because the engine decides who moves first by speed and priority.
        ///
        /// Input is already barred for the duration: the menus were cleared before
        /// the turn, and Update ignores the keyboard while there are no entries.
        /// </remarks>
        System.Collections.IEnumerator PlayTurn()
        {
            var beats = engine != null ? engine.Beats : null;
            for (int i = 0; beats != null && i < beats.Count; i++)
            {
                var b = beats[i];
                var attacker = b.byPlayer ? playerCv : enemyCv;
                var target = b.byPlayer ? enemyCv : playerCv;

                StrikeBeat(attacker, target, ReactionFor(b.damage, b.targetFainted));

                // the bars catch up as the blow lands rather than all at once at the
                // end of the exchange
                RefreshCards();
                yield return new WaitForSeconds(i == 0 ? BeatSeconds : FollowUpSeconds);
            }

            // Only after the beats: a creature that fainted on this turn holds its
            // death pose through the blow that dropped it, and its replacement walks
            // on once the exchange is over rather than mid-swing.
            RefreshStage();
            AfterEnemyPhase();
        }

        /// <summary>Animate the resolved turn, then show the menus (or end it).</summary>
        void ResolveTurn()
        {
            StartCoroutine(PlayTurn());
        }

        /// <summary>Flat battle platform under a creature (classic arena look).</summary>
        void AddBattlePlatform(Transform parent, Vector3 pos, float radius)
        {
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "platform";
            disc.transform.SetParent(parent, false);
            disc.transform.position = pos + new Vector3(0, 0.03f, 0);
            disc.transform.localScale = new Vector3(radius, 0.04f, radius);
            disc.GetComponent<MeshRenderer>().material = WorldBuilder.ToonMat("#E8E0C8");
            var col = disc.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rim.name = "platform_rim";
            rim.transform.SetParent(parent, false);
            rim.transform.position = pos + new Vector3(0, 0.02f, 0);
            rim.transform.localScale = new Vector3(radius * 1.12f, 0.03f, radius * 1.12f);
            rim.GetComponent<MeshRenderer>().material = WorldBuilder.ToonMat("#5A7FBF");
            var rc = rim.GetComponent<Collider>();
            if (rc != null) Destroy(rc);
        }

        void ClearStage()
        {
            stagedPlayer = null;
            stagedEnemy = null;
            if (enemyModel != null) { Destroy(enemyModel); enemyModel = null; }
            if (playerModel != null) { Destroy(playerModel); playerModel = null; }
            enemyCv = null; playerCv = null;
            if (stage != null) { Destroy(stage.gameObject); stage = null; }
        }

        // ---------------- log & cards ----------------

        void OnLog(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return;
            logLines.Add(msg);
            if (logLines.Count > 6) logLines.RemoveAt(0);
            logText.text = string.Join("\n", logLines.ToArray());
        }

        void RefreshCards()
        {
            if (engine == null) return;
            var em = engine.EnemyMon;
            var pm = engine.PlayerMon;

            if (em != null && em.Species != null)
            {
                enemyName.text = em.DisplayName + "  Lv" + em.level;
                enemyStatus.text = em.status != "" ? "[" + em.status + "]" : "";
                float f = Mathf.Clamp01((float)em.curHP / Mathf.Max(1, em.MaxHP));
                UIFactory.SetFill(enemyHpBg, enemyHpFill, f);
                var img = enemyHpFill.GetComponent<Image>();
                if (img != null) img.color = f < 0.2f ? new Color(0.95f, 0.30f, 0.25f)
                    : f < 0.5f ? new Color(0.95f, 0.75f, 0.25f) : new Color(0.30f, 0.80f, 0.35f);
                if (em.Species != null) titleText.text = "wild";
            }
            if (pm != null && pm.Species != null)
            {
                playerName.text = pm.DisplayName + "  Lv" + pm.level;
                playerStatus.text = pm.status != "" ? "[" + pm.status + "]" : "";
                float f = Mathf.Clamp01((float)pm.curHP / Mathf.Max(1, pm.MaxHP));
                UIFactory.SetFill(playerHpBg, playerHpFill, f);
                var img = playerHpFill.GetComponent<Image>();
                if (img != null) img.color = f < 0.2f ? new Color(0.95f, 0.30f, 0.25f)
                    : f < 0.5f ? new Color(0.95f, 0.75f, 0.25f) : new Color(0.30f, 0.80f, 0.35f);
                float xp = Mathf.Clamp01((float)pm.xp / BattleEngine.XpToNext(pm.level));
                UIFactory.SetFill(playerXpBg, playerXpFill, xp);
            }
        }

        // ---------------- menus ----------------

        void ClearMenu()
        {
            for (int i = menuBox.childCount - 1; i >= 0; i--)
            {
                var child = menuBox.GetChild(i).gameObject;
                if (child == endButton.gameObject) continue;
                Destroy(child);
            }
            menuEntries.Clear();
            menuIndex = 0;
            menuBack = null;
        }

        Button MenuButton(string label, Vector2 anchor, Vector2 size, System.Action onClick, int size15 = 15)
        {
            var btn = UIFactory.Button(menuBox, "btn_" + label, label, anchor, size, onClick, size15);
            if (onClick != null)
            {
                var lbl = btn.GetComponentInChildren<Text>();
                menuEntries.Add(new MenuEntry
                {
                    btn = btn,
                    img = btn.GetComponent<Image>(),
                    label = lbl,
                    bg = btn.image.color,
                    fg = lbl != null ? lbl.color : UiTheme.Text,
                    text = label,
                });
            }
            return btn;
        }

        /// <summary>Show which option the keyboard cursor is on (accent bg + ▶ marker).</summary>
        void RefreshHighlight()
        {
            for (int i = 0; i < menuEntries.Count; i++)
            {
                var e = menuEntries[i];
                bool on = i == menuIndex;
                if (e.img != null) e.img.color = on ? UiTheme.Accent2 : e.bg;
                if (e.label != null)
                {
                    e.label.color = on ? new Color(0.10f, 0.12f, 0.16f) : e.fg;
                    e.label.text = on ? "▶ " + e.text : e.text;
                }
            }
        }

        /// <summary>Finish building a menu: put the cursor on the first option.</summary>
        void MenuReady(System.Action back)
        {
            menuBack = back;
            menuIndex = 0;
            RefreshHighlight();
        }

        void ShowMenu()
        {
            if (ended) return;
            ClearMenu();
            MenuButton("FIGHT", new Vector2(0.28f, 0.72f), new Vector2(150, 44), ShowMoves);
            MenuButton("BAG", new Vector2(0.72f, 0.72f), new Vector2(150, 44), ShowBag);
            MenuButton("SWITCH", new Vector2(0.28f, 0.28f), new Vector2(150, 44), OnSwitch);
            MenuButton("RUN", new Vector2(0.72f, 0.28f), new Vector2(150, 44), OnRun);
            MenuReady(null);
        }

        void ShowMoves()
        {
            ClearMenu();
            var pm = engine.PlayerMon;
            var save = Game.I.State;
            if (pm == null) return;

            // four regular moves (left column + top right)
            int row = 0;
            for (int i = 0; i < pm.moves.Count && i < 4; i++)
            {
                string moveId = pm.moves[i];
                var mv = MoveDB.Get(moveId);
                if (mv == null) continue;
                int pp = pm.CurrentPP(moveId);
                string label = mv.name + "  " + Mathf.Max(0, pp) + "/" + mv.pp;
                Vector2 anchor = new Vector2(i % 2 == 0 ? 0.3f : 0.72f, i < 2 ? 0.72f : 0.3f);
                MenuButton(label, anchor, new Vector2(215, 42), () => OnMove(moveId), 14);
            }

            // mastery techniques (right column, under the second move):
            // unlocked by weapon/armor mastery of the gear this creature wears
            var active = new List<(string id, string note)>();
            foreach (var t in WeaponMastery.Techniques)
            {
                var wc = WeaponMastery.ClassOfTechnique(t.moveId);
                int lvl = WeaponMastery.Level(save, wc);
                if (lvl < t.unlockLevel) continue;
                if (WeaponMastery.ClassOf(pm.weapon) != wc && WeaponMastery.ClassOf(pm.armor) != wc) continue;
                int ppLeft = WeaponMastery.TechPP(save, wc, t.moveId);
                var mv = MoveDB.Get(t.moveId);
                string name = mv != null ? mv.name : t.moveId;
                active.Add((t.moveId, name + "  " + ppLeft + "/" + t.basePP + "  [" + WeaponMastery.Name(wc) + " Lv" + lvl + "]"));
            }
            for (int i = 0; i < active.Count && i < 2; i++)
            {
                var (id, note) = active[i];
                bool usable = WeaponMastery.TechPP(save, WeaponMastery.ClassOfTechnique(id), id) > 0;
                var btn = MenuButton(note, new Vector2(0.72f, i == 0 ? 0.08f : -0.08f), new Vector2(215, 42),
                    usable ? (System.Action)(() => OnMove(id)) : null, 12);
                var lbl = btn != null ? btn.GetComponentInChildren<Text>() : null;
                if (lbl != null && !usable) lbl.color = UiTheme.TextDim;
            }
            if (active.Count > 2)
                OnLog("More techniques ready than fit on the wheel — master faster classes first.");

            MenuButton("BACK", new Vector2(0.5f, 0.05f), new Vector2(120, 30), ShowMenu, 13);
            MenuReady(ShowMenu);
        }

        void ShowBag()
        {
            ClearMenu();
            var s = Game.I.State;

            int slot = 0;
            foreach (var stack in s.inventory.ToArray())
            {
                var def = ItemDB.Get(stack.itemId);
                if (def == null) continue;
                bool usable = def.kind == ItemKind.Food || def.kind == ItemKind.Consumable || def.kind == ItemKind.CaptureCore;
                if (!usable) continue;
                if (slot >= 6) break;

                string itemId = stack.itemId;
                string label = def.name + " x" + stack.count;
                Vector2 anchor = new Vector2(slot % 2 == 0 ? 0.3f : 0.72f, slot < 3 ? 0.72f : 0.3f);
                MenuButton(label, anchor, new Vector2(215, 38), () => OnUseItem(itemId), 13);
                slot++;
            }

            if (slot == 0) OnLog("Your bag holds nothing usable.");
            MenuButton("BACK", new Vector2(0.5f, 0.05f), new Vector2(120, 30), ShowMenu, 13);
            MenuReady(ShowMenu);
        }

        // ---------------- actions ----------------

        void OnMove(string moveId)
        {
            if (ended) return;
            ClearMenu();

            engine.PlayerTurn(moveId);
            ResolveTurn();
        }

        void OnUseItem(string itemId)
        {
            if (ended) return;
            var s = Game.I.State;
            var def = ItemDB.Get(itemId);
            if (def == null) return;

            ClearMenu();

            if (def.kind == ItemKind.CaptureCore)
            {
                bool ok = engine.TryCatch(itemId);
                if (ok)
                {
                    engine.ApplyRewards(s);
                    SetEnded();
                    return;
                }
                engine.EnemyOnlyTurn();
                ResolveTurn();
                return;
            }

            var target = engine.PlayerMon;
            if (target != null)
            {
                bool used = false;
                if (def.healAmount > 0 && target.curHP > 0 && target.curHP < target.MaxHP)
                {
                    int healed = def.healAmount >= 999 ? target.MaxHP / 2 : def.healAmount;
                    target.curHP = Mathf.Min(target.MaxHP, target.curHP + healed);
                    OnLog(target.DisplayName + " recovered " + healed + " HP.");
                    used = true;
                }
                if (def.conditionCure > 0 && !string.IsNullOrEmpty(target.status))
                {
                    OnLog(target.DisplayName + " is cured of " + target.status + ".");
                    target.status = "";
                    target.statusTurns = 0;
                    used = true;
                }
                if (!used) OnLog("It would have no effect.");
                if (used) { s.RemoveItem(itemId, 1); Sfx.I.Play("gather"); }
            }

            engine.EnemyOnlyTurn();
            ResolveTurn();
        }

        void OnSwitch()
        {
            if (ended) return;
            ClearMenu();
            var s = Game.I.State;
            int cur = engine.ActivePlayerIdx;
            int slot = 0;
            for (int i = 0; i < s.party.Count; i++)
            {
                var c = s.party[i];
                if (c == null || c.curHP <= 0 || i == cur) continue;
                int idx = i;
                string cname = c.DisplayName;
                Vector2 anchor = new Vector2(slot % 2 == 0 ? 0.3f : 0.72f, slot < 2 ? 0.65f : 0.3f);
                MenuButton(cname, anchor, new Vector2(215, 40),
                    () => DoSwitch(idx), 14);
                slot++;
                if (slot >= 4) break;
            }
            if (slot == 0) OnLog("No one healthy to switch to!");
            MenuButton("BACK", new Vector2(0.5f, 0.05f), new Vector2(120, 30), ShowMenu, 13);
            MenuReady(ShowMenu);
        }

        void DoSwitch(int idx)
        {
            engine.ActivePlayerIdx = idx;
            var c = engine.PlayerMon;
            OnLog("Go, " + (c != null ? c.DisplayName : "?") + "!");

            // The incoming creature is on the field now, before the enemy answers —
            // a chosen switch is immediate, unlike a replacement after a faint.
            RefreshStage();

            engine.EnemyOnlyTurn();
            ResolveTurn();
        }

        void OnRun()
        {
            if (ended) return;
            ClearMenu();
            bool ok = engine.TryFlee();
            if (ok) { SetEnded(); return; }
            engine.EnemyOnlyTurn();
            ResolveTurn();
        }

        void AfterEnemyPhase()
        {
            RefreshCards();
            if (engine.Result == BattleResult.PlayerWon || engine.Result == BattleResult.Caught)
            {
                engine.ApplyRewards(Game.I.State);
                SetEnded();
            }
            else if (engine.Result == BattleResult.PlayerLost)
            {
                SetEnded();
            }
            else if (engine.Result == BattleResult.Fled)
            {
                SetEnded();
            }
            else
            {
                ShowMenu();
            }
        }

        void SetEnded()
        {
            ended = true;
            ClearMenu();
            endButton.gameObject.SetActive(true);

            if (engine.Result == BattleResult.PlayerLost)
            {
                OnLog("Your creatures are exhausted... You hurry back to the ranch.");
                if (Game.I != null) Game.I.NotifyPlayerDefeat();
            }
            else if (engine.Result == BattleResult.PlayerWon && !engine.IsTrainerBattle)
            {
                // wild creatures shed crafting materials
                var drops = WildLoot.Roll(engine.EnemyMon);
                var st = Game.I != null ? Game.I.State : null;
                foreach (var d in drops)
                {
                    if (st != null) st.AddItem(d.id, d.n);
                    OnLog("You gathered " + d.n + "x " + ItemDB.Name(d.id) + ".");
                }
            }
            else if (engine.Result == BattleResult.PlayerWon && engine.IsTrainerBattle && engine.Trainer != null)
            {
                foreach (var line in engine.Trainer.winLines) OnLog(line);
            }
            else if (engine.Result == BattleResult.Caught)
            {
                Sfx.I.Play("catch");
            }
        }

        void Finish()
        {
            if (!ended) return;
            engine.Log -= OnLog;
            ClearStage();
            Show(false);
            if (OnBattleClosed != null) OnBattleClosed();
        }

        void Update()
        {
            if (cg == null || cg.alpha < 0.5f) return;
            if (ended)
            {
                if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return) ||
                    Input.GetKeyDown(KeyCode.KeypadEnter)) Finish();
                return;
            }
            if (menuEntries.Count == 0) return;

            // ---- move cursor (2-column grid: up/down jump a row) ----
            int idx = menuIndex;
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) idx -= 2;
            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) idx += 2;
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) idx -= 1;
            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) idx += 1;
            idx = Mathf.Clamp(idx, 0, menuEntries.Count - 1);
            if (idx != menuIndex)
            {
                menuIndex = idx;
                RefreshHighlight();
                if (Sfx.I != null) Sfx.I.Play("ui");
            }

            // ---- confirm ----
            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                var entry = menuEntries[menuIndex];
                if (entry.btn != null && entry.btn.interactable)
                    entry.btn.onClick.Invoke();
            }
            // ---- back out of a submenu ----
            else if ((Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace)) && menuBack != null)
            {
                if (Sfx.I != null) Sfx.I.Play("ui");
                menuBack();
            }
        }

        public void Show(bool on)
        {
            cg.alpha = on ? 1f : 0f;
            cg.blocksRaycasts = on;
            cg.interactable = on;
        }
    }
}
