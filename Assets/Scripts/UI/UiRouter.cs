using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using PokeProfessions.Core;
using PokeProfessions.World;
using PokeProfessions.Audio;

namespace PokeProfessions.UI
{
    /// <summary>
    /// Owns every UI panel and routes requests (talk, craft, shop, battle).
    /// Also handles global keys: J journal, C creatures, Esc back.
    /// </summary>
    public class UiRouter : MonoBehaviour
    {
        public static UiRouter I { get; private set; }

        public bool UiOpen => openPanels.Count > 0;

        Canvas canvas;
        DialoguePanel dialogue;
        BattlePanel battle;
        CraftingPanel crafting;
        ShopPanel shop;
        JournalPanel journal;
        PartyPanel party;
        TitlePanel title;
        HudPanel hud;

        readonly List<GameObject> openPanels = new List<GameObject>();
        DialogueRuntime runtime;
        float suppressInteractUntil;

        void Awake()
        {
            I = this;

            canvas = UIFactory.BuildCanvas("GameCanvas");

            hud = HudPanel.Build(canvas);
            journal = JournalPanel.Build(canvas);
            party = PartyPanel.Build(canvas);
            crafting = CraftingPanel.Build(canvas);
            shop = ShopPanel.Build(canvas);
            battle = BattlePanel.Build(canvas);
            dialogue = DialoguePanel.Build(canvas);
            title = TitlePanel.Build(canvas);

            title.OnStart += HandleStart;
            battle.OnBattleClosed += HandleBattleClosed;
        }

        void HandleStart()
        {
            // New Game: wipe happened in TitlePanel; reload the scene so every
            // system boots from a fresh GameState instead of the loaded save.
            // The static flag survives the reload; Start() consumes it.
            if (TitlePanel.PendingNewGame)
            {
                Toast.Clear();
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                return;
            }
            hud.Show(true);
            title.Show(false);
        }

        void Start()
        {
            // fresh scene after a New Game restart: skip the title entirely
            if (TitlePanel.PendingNewGame)
            {
                TitlePanel.PendingNewGame = false;
                hud.Show(true);
                title.Show(false);
            }
        }

        // ---------------- routing ----------------

        public void TalkTo(string npcId)
        {
            NpcDef def;
            if (!StoryData.Npcs.TryGetValue(npcId, out def)) return;

            runtime = new DialogueRuntime(Game.I.State);
            runtime.LineShown += OnDialogueLine;
            runtime.TrainerSpawn += OnTrainerSpawn;
            runtime.CraftRequested += OpenCrafting;
            runtime.ShopRequested += OpenShop;
            runtime.CreatureGift += OnCreatureGift;
            runtime.Ended += OnDialogueEnded;

            suppressInteractUntil = Time.time + 0.25f;
            runtime.Start(def);
            if (runtime.Active) Push(dialogue.gameObject);
            if (Game.I != null && Game.I.State != null) QuestSystem.AutoCompleteAll(Game.I.State);
        }

        void OnDialogueLine(DialogueLine line)
        {
            dialogue.ShowLine(line);
        }

        void OnTrainerSpawn(TrainerDef t)
        {
            runtime.End();
            var engine = new BattleEngine(new List<Creature>(Game.I.State.party), BuildTeam(t.team), t);
            StartBattle(engine);
        }

        static List<Creature> BuildTeam(string[] entries)
        {
            var team = new List<Creature>();
            foreach (var e in entries)
            {
                var c = MonsterFactory.FromTrainerEntry(e);
                if (c != null) team.Add(c);
            }
            return team;
        }

        void OnCreatureGift(Creature c)
        {
            if (c == null) return;
            if (Game.I.State.party.Count < 6) Game.I.State.party.Add(c);
            else Game.I.State.box.Add(c);
            if (!Game.I.State.caughtSpecies.Contains(c.speciesId)) Game.I.State.caughtSpecies.Add(c.speciesId);
            Toast.Push(c.DisplayName + " joined you!");
            Sfx.I.Play("catch");
        }

        void OnDialogueEnded()
        {
            dialogue.Show(false); // hide the box, not just the router stack
            Pop(dialogue.gameObject);
            runtime = null;
            // the E press that ended the talk must not instantly re-talk the NPC
            suppressInteractUntil = Time.time + 0.25f;
            QuestSystem.AutoCompleteAll(Game.I.State);
            Game.I.State.Save();
        }

        public void OpenCrafting(string stationId)
        {
            crafting.Open(stationId);
            Push(crafting.gameObject);
        }

        public void OpenShop(string shopId)
        {
            shop.Open(shopId);
            Push(shop.gameObject);
        }

        public void StartBattle(BattleEngine engine)
        {
            battle.Begin(engine);
            Push(battle.gameObject);
        }

        public void AdvanceDialogue()
        {
            if (runtime != null) runtime.Advance();
        }

        public void ChooseDialogue(bool yes)
        {
            if (runtime != null) runtime.Choose(yes);
        }

        void HandleBattleClosed()
        {
            Pop(battle.gameObject);
            Game.I.MarkBattleFinished();
            // quest completion for defeat objectives
            QuestSystem.AutoCompleteAll(Game.I.State);
        }

        // ---------------- panel stack ----------------

        void Push(GameObject panel)
        {
            if (!openPanels.Contains(panel)) openPanels.Add(panel);
        }

        void Pop(GameObject panel)
        {
            openPanels.Remove(panel);
        }

        /// <summary>Called by panels' close buttons after they hide themselves.</summary>
        public void ClosePanel(GameObject panel)
        {
            Pop(panel);
        }

        /// <summary>World interaction input is ignored until this time (prevents double-activation).</summary>
        public float InteractSuppressUntil { get { return suppressInteractUntil; } }

        public void ToggleJournal()
        {
            if (openPanels.Contains(journal.gameObject)) { Pop(journal.gameObject); journal.Show(false); }
            else if (!UiOpen || openPanels.Count == 1 && openPanels[0] == journal.gameObject)
            { journal.Open(); Push(journal.gameObject); }
        }

        public void ToggleParty()
        {
            if (openPanels.Contains(party.gameObject)) { Pop(party.gameObject); party.Show(false); }
            else if (!UiOpen || openPanels.Count == 1 && openPanels[0] == party.gameObject)
            { party.Open(); Push(party.gameObject); }
        }

        void Update()
        {
            if (Game.I == null) return;

            if (Input.GetKeyDown(KeyCode.J) && CanTogglePanels()) ToggleJournal();
            if (Input.GetKeyDown(KeyCode.C) && CanTogglePanels()) ToggleParty();
            if (Input.GetKeyDown(KeyCode.Escape)) CloseTop();
            if (Input.GetKeyDown(KeyCode.F5)) { Game.I.State.Save(); Toast.Push("Game saved."); }
        }

        bool CanTogglePanels()
        {
            // allowed when nothing is open except the panel being toggled
            if (!UiOpen) return true;
            return openPanels.Count == 1 &&
                   (openPanels[0] == journal.gameObject || openPanels[0] == party.gameObject);
        }

        void CloseTop()
        {
            if (!UiOpen) return;
            var top = openPanels[openPanels.Count - 1];
            if (top == battle.gameObject) return; // battle has its own exit rules
            if (top == dialogue.gameObject) { runtime?.Advance(); return; }

            if (top == journal.gameObject) { journal.Show(false); Pop(top); }
            else if (top == party.gameObject) { party.Show(false); Pop(top); }
            else if (top == crafting.gameObject) { crafting.Show(false); Pop(top); }
            else if (top == shop.gameObject) { shop.Show(false); Pop(top); }
            else if (top == title.gameObject) { /* title stays */ }
        }
    }
}
