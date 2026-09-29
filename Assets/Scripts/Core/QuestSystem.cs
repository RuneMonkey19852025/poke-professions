using System;
using System.Collections.Generic;
using UnityEngine;
using PokeProfessions.UI;

namespace PokeProfessions.Core
{
    /// <summary>
    /// Static quest logic over StoryData quest defs and GameState flags.
    /// UI reads events for toasts and journal updates.
    /// </summary>
    public static class QuestSystem
    {
        public static event Action<QuestDef> QuestAccepted;
        public static event Action<QuestDef> QuestCompleted;

        static bool HasPrereqs(GameState s, QuestDef q)
        {
            if (q.prereqQuests != null)
                foreach (var id in q.prereqQuests) if (!s.doneQuests.Contains(id)) return false;
            if (q.requiresFlags != null)
                foreach (var f in q.requiresFlags) if (!s.HasFlag(f)) return false;
            if (q.forbidsFlags != null)
                foreach (var f in q.forbidsFlags) if (s.HasFlag(f)) return false;
            return true;
        }

        public static bool IsOffered(GameState s, QuestDef q)
        {
            if (s == null || q == null) return false;
            if (s.doneQuests.Contains(q.id) || s.activeQuests.Contains(q.id)) return false;
            return HasPrereqs(s, q);
        }

        public static bool IsActive(GameState s, string questId) => s != null && s.activeQuests.Contains(questId);

        public static bool Accept(GameState s, string questId)
        {
            var q = Get(questId);
            if (q == null || s == null || s.activeQuests.Contains(questId) || s.doneQuests.Contains(questId)) return false;
            s.activeQuests.Add(questId);
            foreach (var f in q.onAcceptFlags) s.SetFlag(f);
            foreach (var give in q.onAcceptGiveItems) ApplyGive(s, give);
            Toast.Push("Quest accepted: " + q.name);
            if (QuestAccepted != null) QuestAccepted(q);
            return true;
        }

        public static bool Complete(GameState s, string questId)
        {
            var q = Get(questId);
            if (q == null || s == null) return false;
            if (!s.activeQuests.Contains(questId)) return false;
            if (!IsComplete(s, q)) return false;

            s.activeQuests.Remove(questId);
            s.doneQuests.Add(questId);
            foreach (var f in q.onCompleteFlags) s.SetFlag(f);
            foreach (var give in q.onCompleteGiveItems) ApplyGive(s, give);
            if (q.rewardCoins > 0) s.coins += q.rewardCoins;

            Toast.Push("Quest complete: " + q.name + (q.rewardCoins > 0 ? "  +" + q.rewardCoins + " coins" : ""));
            if (QuestCompleted != null) QuestCompleted(q);
            return true;
        }

        static void ApplyGive(GameState s, string give)
        {
            var parts = give.Split(':');
            if (parts.Length < 1 || parts.Length > 2) return;
            int count = parts.Length == 2 && int.TryParse(parts[1], out int n) ? n : 1;
            s.AddItem(parts[0], count);
            Toast.Push("Received " + count + "x " + ItemDB.Name(parts[0]));
        }

        // ---------------- objectives ----------------
        // supported: "craft <itemId>", "catch_any", "defeat <trainerId>",
        //            "collect <itemId>", "prof3_lv2", "wake_sleeper"
        public static bool IsComplete(GameState s, QuestDef q)
        {
            if (q.objectives == null || q.objectives.Length == 0) return true;
            foreach (var obj in q.objectives)
            {
                if (!ObjectiveMet(s, obj)) return false;
            }
            return true;
        }

        public static bool IsObjectiveMet(GameState s, string obj)
        {
            return ObjectiveMet(s, obj);
        }

        static bool ObjectiveMet(GameState s, string obj)
        {
            var parts = obj.Split(' ');
            string verb = parts.Length > 0 ? parts[0] : "";
            string arg = parts.Length > 1 ? obj.Substring(obj.IndexOf(' ') + 1) : "";

            switch (verb)
            {
                case "craft": return s.HasFlag("craft_" + arg);
                case "catch_any": return s.HasFlag("caught_any");
                case "defeat": return s.defeatedTrainers.Contains(arg);
                case "collect": return s.CountItem(arg) > 0;
                case "prof3_lv2":
                    {
                        int count = 0;
                        foreach (var p in ProfessionSystem.All) if (s.ProfLevel(p) >= 2) count++;
                        return count >= 3;
                    }
                case "wake_sleeper": return s.HasFlag("sleeper_awake");
            }
            return false;
        }

        public static string ObjectiveText(GameState s, string obj)
        {
            SplitObjective(obj, out string verb, out string arg);
            switch (verb)
            {
                case "craft": return "Craft " + ItemDB.Name(arg);
                case "catch_any": return "Catch a wild creature";
                case "defeat": return "Defeat " + TrainerName(arg);
                case "collect": return "Obtain " + ItemDB.Name(arg);
                case "prof3_lv2": return "Reach Lv2 in 3 professions";
                case "wake_sleeper": return "Wake the Sleeper";
            }
            return obj;
        }

        static void SplitObjective(string obj, out string verb, out string arg)
        {
            var idx = obj.IndexOf(' ');
            if (idx < 0) { verb = obj; arg = ""; }
            else { verb = obj.Substring(0, idx); arg = obj.Substring(idx + 1); }
        }

        static string TrainerName(string trainerId)
        {
            TrainerDef t;
            if (StoryData.Trainers != null && StoryData.Trainers.TryGetValue(trainerId, out t)) return t.name;
            return trainerId;
        }

        /// <summary>Completes every active quest whose objectives are now met (data sets autoComplete).</summary>
        public static void AutoCompleteAll(GameState s)
        {
            if (s == null) return;
            for (int i = s.activeQuests.Count - 1; i >= 0; i--)
            {
                var q = Get(s.activeQuests[i]);
                if (q == null) continue;
                if (IsComplete(s, q)) Complete(s, q.id);
            }
        }

        public static QuestDef Get(string id)
        {
            QuestDef q;
            return StoryData.Quests != null && StoryData.Quests.TryGetValue(id, out q) ? q : null;
        }
    }
}
