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
        readonly List<string> logLines = new List<string>();
        bool ended;

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
            engine = eng;
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
            }
        }

        System.Collections.IEnumerator ReturnToIdle(CreatureVisual cv)
        {
            yield return new WaitForSeconds(1.2f);
            if (cv != null) cv.PlayState("idle");
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
        }

        void MenuButton(string label, Vector2 anchor, Vector2 size, System.Action onClick, int size15 = 15)
        {
            UIFactory.Button(menuBox, "btn_" + label, label, anchor, size, onClick, size15);
        }

        void ShowMenu()
        {
            if (ended) return;
            ClearMenu();
            MenuButton("FIGHT", new Vector2(0.28f, 0.72f), new Vector2(150, 44), ShowMoves);
            MenuButton("BAG", new Vector2(0.72f, 0.72f), new Vector2(150, 44), ShowBag);
            MenuButton("SWITCH", new Vector2(0.28f, 0.28f), new Vector2(150, 44), OnSwitch);
            MenuButton("RUN", new Vector2(0.72f, 0.28f), new Vector2(150, 44), OnRun);
        }

        void ShowMoves()
        {
            ClearMenu();
            var pm = engine.PlayerMon;
            if (pm == null) return;

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
            MenuButton("BACK", new Vector2(0.5f, 0.05f), new Vector2(120, 30), ShowMenu, 13);
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
        }

        // ---------------- actions ----------------

        void OnMove(string moveId)
        {
            if (ended) return;
            ClearMenu();
            if (playerCv != null) playerCv.PlayState("attack");
            engine.PlayerTurn(moveId);
            if (engine.EnemyMon != null && engine.EnemyMon.curHP > 0 && engine.EnemyMon.curHP < engine.EnemyMon.MaxHP)
            {
                if (enemyCv != null) enemyCv.PlayState("hit");
                if (playerCv != null) StartCoroutine(ReturnToIdle(playerCv));
            }
            AfterEnemyPhase();
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
                AfterEnemyPhase();
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
            AfterEnemyPhase();
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
        }

        void DoSwitch(int idx)
        {
            engine.ActivePlayerIdx = idx;
            var c = engine.PlayerMon;
            OnLog("Go, " + (c != null ? c.DisplayName : "?") + "!");
            engine.EnemyOnlyTurn();
            AfterEnemyPhase();
        }

        void OnRun()
        {
            if (ended) return;
            ClearMenu();
            bool ok = engine.TryFlee();
            if (ok) { SetEnded(); return; }
            engine.EnemyOnlyTurn();
            AfterEnemyPhase();
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
            if (ended && Input.GetKeyDown(KeyCode.E)) Finish();
        }

        public void Show(bool on)
        {
            cg.alpha = on ? 1f : 0f;
            cg.blocksRaycasts = on;
            cg.interactable = on;
        }
    }
}
