using System;
using System.Collections.Generic;
using PokeProfessions.UI;

namespace PokeProfessions.Core
{
    /// <summary>
    /// Plays a DialogueNode line by line. Line effects (flags, items, quest
    /// offers, spawns) apply when the line is displayed. "choice" lines wait
    /// for Choose(); "end" lines finish the conversation on Advance().
    /// </summary>
    public class DialogueRuntime
    {
        public DialogueLine Current { get; private set; }
        public bool Active { get; private set; }
        public string NpcId { get; private set; }

        public event Action<DialogueLine> LineShown;
        public event Action<TrainerDef> TrainerSpawn;
        public event Action<string> ShopRequested;     // shopId
        public event Action<string> CraftRequested;    // station id
        public event Action<Creature> CreatureGift;    // story creature
        public event Action Ended;

        readonly GameState state;
        List<DialogueLine> queue = new List<DialogueLine>();
        int idx;
        string pendingNodeId;

        public DialogueRuntime(GameState state) { this.state = state; }

        public void Start(NpcDef npc)
        {
            if (npc == null || npc.dialogueNodes == null || npc.dialogueNodes.Length == 0) return;
            NpcId = npc.id;
            Jump(PickNode(npc));
        }

        void Jump(string nodeId)
        {
            var node = StoryData.GetNode(nodeId);
            if (node == null || node.lines.Count == 0) { End(); return; }
            queue = node.lines;
            idx = 0;
            Active = true;
            ShowCurrent();
        }

        void ShowCurrent()
        {
            Current = queue[idx];
            ApplyLine(Current);
            if (LineShown != null) LineShown(Current);
        }

        void ApplyLine(DialogueLine line)
        {
            if (!string.IsNullOrEmpty(line.setFlag)) state.SetFlag(line.setFlag);

            if (!string.IsNullOrEmpty(line.giveItem))
            {
                var parts = line.giveItem.Split(':');
                int n = parts.Length > 1 && int.TryParse(parts[1], out int c) ? c : 1;
                state.AddItem(parts[0], n);
                Toast.Push("Received " + n + "x " + ItemDB.Name(parts[0]));
            }

            if (!string.IsNullOrEmpty(line.teach))
            {
                var parts = line.teach.Split(':');
                if (parts.Length == 2 && Enum.TryParse(parts[0], out ProfessionId p) && int.TryParse(parts[1], out int lv))
                {
                    // teach = "Prof:level" — raise the trade TO that level
                    // (story lessons certify competence; skills already start at 1)
                    int target = Math.Clamp(lv, 1, ProfessionSystem.MaxLevel);
                    if (state.ProfLevel(p) < target)
                    {
                        state.SetProfLevel(p, target);
                        Toast.Push(ProfessionSystem.Name(p) + " trained to level " + target);
                    }
                }
            }

            if (line.healParty)
            {
                state.HealAll();
                Toast.Push("Your creatures are fully rested!");
            }

            if (!string.IsNullOrEmpty(line.questId))
            {
                var q = QuestSystem.Get(line.questId);
                if (q != null && QuestSystem.IsOffered(state, q))
                    QuestSystem.Accept(state, q.id);
            }

            if (!string.IsNullOrEmpty(line.spawnCreature))
            {
                var parts = line.spawnCreature.Split(':');
                int lvl = parts.Length > 1 && int.TryParse(parts[1], out int l) ? l : 5;
                var c = MonsterFactory.Create(parts[0], lvl);
                if (c != null && CreatureGift != null) CreatureGift(c);
            }

            if (!string.IsNullOrEmpty(line.spawnTrainer))
            {
                TrainerDef t;
                if (StoryData.Trainers.TryGetValue(line.spawnTrainer, out t) && TrainerSpawn != null)
                    TrainerSpawn(t);
            }
        }

        /// <summary>Player pressed the continue key.</summary>
        public void Advance()
        {
            if (!Active) return;
            var line = Current;

            if (!string.IsNullOrEmpty(line.openShop)) { End(); if (ShopRequested != null) ShopRequested(line.openShop); return; }
            if (!string.IsNullOrEmpty(line.openCraft)) { End(); if (CraftRequested != null) CraftRequested(line.openCraft); return; }

            if (line.endRule == "choice") return; // must Choose()

            if (line.endRule == "end" || idx >= queue.Count - 1) { End(); return; }
            idx++;
            ShowCurrent();
        }

        /// <summary>Player answered a choice line.</summary>
        public void Choose(bool yes)
        {
            if (!Active || Current == null || Current.endRule != "choice") return;
            string target = yes ? Current.yesNode : Current.noNode;
            if (string.IsNullOrEmpty(target)) { End(); return; }
            Jump(target);
        }

        public void End()
        {
            if (!Active) return;
            Active = false;
            Current = null;
            if (!string.IsNullOrEmpty(NpcId)) state.SetFlag("met_" + NpcId);
            if (Ended != null) Ended();
        }

        // ---------------- node selection ----------------
        string PickNode(NpcDef npc)
        {
            bool activeOrDone(string qid) => state.activeQuests.Contains(qid) || state.doneQuests.Contains(qid);

            switch (npc.id)
            {
                case "brann": return activeOrDone("q_firstcore") ? "brann_loop" : "brann_intro";
                case "maren":
                    if (state.HasFlag("caught_first")) return "maren_yes";
                    return activeOrDone("q_firstcatch") ? "maren_no" : "maren_idle";
                case "pell": return state.HasFlag("met_pell") ? "pell_loop" : "pell_intro";
                case "mira": return state.HasFlag("met_mira") ? "mira_loop" : "mira_intro";
                case "juna": return activeOrDone("q_professions") ? "juna_loop" : "juna_intro";
                case "odessa": return state.HasFlag("ridge_opened") ? "odessa_loop" : "odessa_intro";
                case "tobin": return state.defeatedTrainers.Contains("trainer_tobin") ? "tobin_loop" : "tobin_fight";
                case "sela": return state.HasFlag("has_sigil") ? "sela_loop" : "sela_intro";
                case "korrin": return state.defeatedTrainers.Contains("trainer_korrin") ? "korrin_loop" : "korrin_fight";
                case "vesna": return state.HasFlag("met_vesna") ? "vesna_loop" : "vesna_intro";
                case "hollis": return state.defeatedTrainers.Contains("trainer_hollis") ? "hollis_loop" : "hollis_fight";
                case "zephy": return state.HasFlag("herald_down") ? "zephy_loop" : "zephy_fight";
                case "sleepers": return state.HasFlag("sleeper_awake") ? "sleeper_done" : "sleeper_final";
            }
            return npc.dialogueNodes[0];
        }
    }
}
