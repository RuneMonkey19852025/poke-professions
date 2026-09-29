using System.Collections.Generic;

namespace PokeProfessions.Core
{
    // ---------------------------------------------------------------- npcs
    [System.Serializable]
    public class NpcDef
    {
        public string id;
        public string name;
        public string role;
        public string colorHex;
        public string zone;
        public string[] dialogueNodes;
    }

    // ---------------------------------------------------------------- quests
    [System.Serializable]
    public class QuestDef
    {
        public string id;
        public string name;
        public string summary;
        public string giverNpcId;
        public string act;
        public string[] prereqQuests = new string[0];
        public string[] requiresFlags = new string[0];
        public string[] forbidsFlags = new string[0];
        public string[] objectives = new string[0];
        public string[] onAcceptFlags = new string[0];
        public string[] onCompleteFlags = new string[0];
        public string[] onAcceptGiveItems = new string[0];
        public string[] onCompleteGiveItems = new string[0];
        public string[] onAcceptTeach = new string[0];
        public string[] onCompleteTeach = new string[0];
        public int rewardCoins = 0;
    }

    // ---------------------------------------------------------------- dialogue
    [System.Serializable]
    public class DialogueLine
    {
        public string speaker;
        public string text;
        public string questId;       // offers this quest (accept/decline UI)
        public string setFlag;       // unconditional flag on this line
        public string giveItem;      // "itemId:count"
        public string teach;         // "profId:levels"
        public string spawnCreature; // "speciesId:level"
        public string spawnTrainer;  // trainerId
        public bool healParty;       // free full party heal (Mira at the well)
        public string openShop;      // shopId
        public string openCraft;     // station id ("forge", "campfire", "workbench")
        public string endRule;       // "" = advance to next line, "end", "choice"
        public string yesNode;
        public string noNode;
    }

    [System.Serializable]
    public class DialogueNode
    {
        public string id;
        public List<DialogueLine> lines = new List<DialogueLine>();
    }

    // ---------------------------------------------------------------- trainer battles
    [System.Serializable]
    public class TrainerDef
    {
        public string id;
        public string name;
        public string title;
        public string[] team = new string[0];       // "species:level"
        public string[] preBattle = new string[0];
        public string[] winLines = new string[0];
        public string[] loseLines = new string[0];
        public string[] onDefeatFlags = new string[0];
        public string[] onDefeatGiveItems = new string[0];
        public int rewardCoins = 80;
    }

    // ---------------------------------------------------------------- shops
    [System.Serializable]
    public class ShopDef
    {
        public string id;
        public string name;
        public string[] stock = new string[0]; // "itemId:count" (-1 = infinite)
    }

    /// <summary>
    /// The authored narrative of Emberwild Vale — acts, quests, dialogue,
    /// trainers and shops as one readable data table.
    ///
    /// STORY OUTLINE (3 acts):
    ///   Act 1 - "Smoke and Small Debts": arrive, learn a profession, craft a
    ///           first capture core, catch a first friend, beat Tobin.
    ///   Act 2 - "The Sigil of Root, Tide and Ember": the ridge opens; Korrin
    ///           holds a sigil fragment; Sela sends you for all three.
    ///   Act 3 - "The Hollow Sleepers": Zephy guards the descent; wake the
    ///           Sleeper with the assembled Verdant Sigil.
    /// </summary>
    public static class StoryData
    {
        public static Dictionary<string, QuestDef> Quests;
        public static Dictionary<string, DialogueNode> Dialogue;
        public static Dictionary<string, TrainerDef> Trainers;
        public static Dictionary<string, ShopDef> Shops;
        public static Dictionary<string, NpcDef> Npcs;

        public static void Init()
        {
            Quests = new Dictionary<string, QuestDef>();
            Dialogue = new Dictionary<string, DialogueNode>();
            Trainers = new Dictionary<string, TrainerDef>();
            Shops = new Dictionary<string, ShopDef>();
            Npcs = new Dictionary<string, NpcDef>();

            // ================= NPCs =================
            Npc("maren", "Maren", "Rancher", "#D9884A", "meadow", new[] { "maren_idle", "maren_yes", "maren_no" });
            Npc("pell", "Old Pell", "Ferryman", "#7A9E7E", "meadow", new[] { "pell_intro", "pell_loop" });
            Npc("mira", "Mira", "Well Tender", "#8FD0A8", "meadow", new[] { "mira_intro", "mira_loop" });
            Npc("juna", "Juna", "Professions Warden", "#C9A0DC", "meadow", new[] { "juna_intro", "juna_loop" });
            Npc("brann", "Brann", "Smith", "#B0653A", "meadow", new[] { "brann_intro", "brann_accept", "brann_decline", "brann_loop" });
            Npc("odessa", "Odessa", "Ranger Captain", "#5A7FBF", "meadow", new[] { "odessa_intro", "odessa_yes", "odessa_no", "odessa_loop" });
            Npc("tobin", "Tobin", "Young Trainer", "#E0C060", "meadow", new[] { "tobin_fight", "tobin_loop" });
            Npc("sela", "Sela", "Herbalist", "#7FBF9E", "ridge", new[] { "sela_intro", "sela_yes", "sela_no", "sela_loop" });
            Npc("korrin", "Korrin", "Prospector", "#8A6F5A", "ridge", new[] { "korrin_fight", "korrin_loop" });
            Npc("vesna", "Vesna", "Angler", "#6FA8B8", "lake", new[] { "vesna_intro", "vesna_loop" });
            Npc("hollis", "Hollis", "Wandering Bard", "#B87FB8", "lake", new[] { "hollis_fight", "hollis_loop" });
            Npc("zephy", "Zephy", "Hollow Herald", "#8878B8", "hollow", new[] { "zephy_fight", "zephy_loop" });
            Npc("sleepers", "The Sleeper", "Hollow Dreamer", "#9AA8D8", "hollow_deep", new[] { "sleeper_final", "sleeper_done" });

            // ================= SHOPS =================
            Shop("general", "Vale Post Traders", new[] {
                "core_wood:6", "core_iron:3", "trail_snack:8", "salve:6", "purge_draught:4",
                "rope:5", "fiber:-1", "bitterleaf:-1", "stone:-1",
                "gear_claw:2", "gear_band:2", "gear_fang:1", "gear_hide:1" });

            // ================= QUESTS =================
            Q(new QuestDef
            {
                id = "q_firstcore",
                name = "First Core",
                summary = "Brann: gather fiber and stone, braid rope, and craft a Timber Core at his forge.",
                giverNpcId = "brann",
                act = "1",
                objectives = new[] { "craft core_wood" },
                onCompleteFlags = new[] { "made_first_core" },
                onCompleteGiveItems = new[] { "trail_snack:2" },
                rewardCoins = 60
            });
            Q(new QuestDef
            {
                id = "q_firstcatch",
                name = "First Friend",
                summary = "Maren: weaken a wild creature and catch it with a capture core.",
                giverNpcId = "maren",
                act = "1",
                prereqQuests = new[] { "q_firstcore" },
                objectives = new[] { "catch_any" },
                onCompleteFlags = new[] { "caught_first" },
                onCompleteGiveItems = new[] { "core_wood:2" },
                rewardCoins = 40
            });
            Q(new QuestDef
            {
                id = "q_tobin",
                name = "Rival Winds",
                summary = "Tobin waits at the ranch gate for a battle. Show him the meadow chose well.",
                giverNpcId = "tobin",
                act = "1",
                prereqQuests = new[] { "q_firstcatch" },
                objectives = new[] { "defeat trainer_tobin" },
                onCompleteFlags = new[] { "beat_tobin" },
                rewardCoins = 80
            });
            Q(new QuestDef
            {
                id = "q_professions",
                name = "Hands for the Vale",
                summary = "Juna: reach level 2 in any three professions and be sworn into the Vale rolls.",
                giverNpcId = "juna",
                act = "2",
                prereqQuests = new[] { "q_firstcore" },
                objectives = new[] { "prof3_lv2" },
                onCompleteFlags = new[] { "prof_sworn" },
                onCompleteGiveItems = new[] { "forager_gloves:1" },
                rewardCoins = 100
            });
            Q(new QuestDef
            {
                id = "q_emberrock",
                name = "Smoke over Emberrock",
                summary = "Odessa: cross the western bridge and deal with Korrin on Emberrock Ridge.",
                giverNpcId = "odessa",
                act = "2",
                prereqQuests = new[] { "q_tobin" },
                objectives = new[] { "defeat trainer_korrin" },
                onCompleteFlags = new[] { "ridge_opened" },
                onCompleteGiveItems = new[] { "miner_pick:1" },
                rewardCoins = 120
            });
            Q(new QuestDef
            {
                id = "q_sigil",
                name = "The Verdant Sigil",
                summary = "Sela: recover the three sigil fragments. Root - beat Korrin. Tide - beat Hollis. Ember - rarely surfaces when mining the ridge veins.",
                giverNpcId = "sela",
                act = "2",
                prereqQuests = new[] { "q_emberrock" },
                objectives = new[] { "collect sigil_piece_a", "collect sigil_piece_b", "collect sigil_piece_c" },
                onCompleteFlags = new[] { "has_sigil" },
                onCompleteGiveItems = new[] { "verdant_sigil:1" },
                rewardCoins = 150
            });
            Q(new QuestDef
            {
                id = "q_herald",
                name = "The Hollow Herald",
                summary = "Zephy bars the descent into the Hollow. Defeat the herald of the Sleepers.",
                giverNpcId = "zephy",
                act = "3",
                prereqQuests = new[] { "q_sigil" },
                objectives = new[] { "defeat trainer_zephy" },
                onCompleteFlags = new[] { "herald_down" },
                onCompleteGiveItems = new[] { "hearty_stew:2", "core_moon:1" },
                rewardCoins = 200
            });
            Q(new QuestDef
            {
                id = "q_sleeper",
                name = "The Hollow Sleepers",
                summary = "Descend to the Sleeper with the Verdant Sigil and the lantern. Wake what dreams below.",
                giverNpcId = "sleepers",
                act = "3",
                prereqQuests = new[] { "q_herald" },
                objectives = new[] { "wake_sleeper" },
                onCompleteFlags = new[] { "sleeper_awake" },
                onCompleteGiveItems = new[] { "revive_tonic:1", "core_moon:2" },
                rewardCoins = 500
            });

            // ================= DIALOGUE =================
            // --- Brann, smith ---
            Node("brann_intro",
                new DialogueLine { speaker = "Brann", text = "New face. Good. The Vale needs hands that work, not watchers.", endRule = "", },
                new DialogueLine { speaker = "Brann", text = "First lesson: everything starts with a capture core. I can teach you the smithing for it. Interested?", endRule = "choice", yesNode = "brann_accept", noNode = "brann_decline" });
            Node("brann_accept",
                new DialogueLine { speaker = "Brann", text = "Then it's settled. I'll show you the basics of the forge.", teach = "Smith:1", questId = "q_firstcore", endRule = "" },
                new DialogueLine { speaker = "Brann", text = "And here's the tailor's trick for braiding rope - fiber and knuckle grease. Gather fiber and stone, braid rope, and craft a Timber Core at my forge.", teach = "Tailor:1", endRule = "end" });
            Node("brann_decline",
                new DialogueLine { speaker = "Brann", text = "Suit yourself. The forge isn't going anywhere. Neither is the smoke on the ridge.", endRule = "end" });
            Node("brann_loop",
                new DialogueLine { speaker = "Brann", text = "Forge is lit. Craft away, and mind your fingers.", openCraft = "forge", endRule = "end" });

            // --- Maren, rancher ---
            Node("maren_idle",
                new DialogueLine { speaker = "Maren", text = "You've that look. Caught yourself a creature yet?", endRule = "choice", yesNode = "maren_yes", noNode = "maren_no" });
            Node("maren_yes",
                new DialogueLine { speaker = "Maren", text = "Good on you. Keep them fed and they'll walk through fire for you. Here - ranch-style camp cooking, so you can feed them right.", teach = "Chef:1", questId = "q_firstcatch", endRule = "end" });
            Node("maren_no",
                new DialogueLine { speaker = "Maren", text = "Weaken one first - fight it near-death - then throw a core. The meadow's patient. So am I.", questId = "q_firstcatch", endRule = "end" });

            // --- Old Pell, ferryman ---
            Node("pell_intro",
                new DialogueLine { speaker = "Old Pell", text = "Ah. The lantern chose you. It did me, sixty years back. I still hear it humming of nights.", endRule = "end" });
            Node("pell_loop",
                new DialogueLine { speaker = "Old Pell", text = "Sixty years I've ferried folk to the Hollow mouth. None came back the same. Most didn't come back at all.", endRule = "end" });

            // --- Mira, well tender — heals your party for free ---
            Node("mira_intro",
                new DialogueLine { speaker = "Mira", text = "Mind the stones, traveler — the well water remembers every stumble. Your creatures look weary. Here, let the water work.", healParty = true, endRule = "" },
                new DialogueLine { speaker = "Mira", text = "There. Good as spring-water fresh. Rest is the only medicine I can't draw from a rope — anything ails them, come find me.", endRule = "end" });
            Node("mira_loop",
                new DialogueLine { speaker = "Mira", text = "Need the water's kindness again? Hold still a moment.", healParty = true, endRule = "end" });

            // --- Juna, professions warden ---
            Node("juna_intro",
                new DialogueLine { speaker = "Juna", text = "I keep the Vale's professions: Forager, Miner, Fisher, Chef, Smith, Tailor. A person of three trades is worth ten of none. Reach level 2 in three trades and I'll swear you in.", questId = "q_professions", endRule = "end" });
            Node("juna_loop",
                new DialogueLine { speaker = "Juna", text = "The rolls remember every hand that fed the Vale. Keep working.", endRule = "end" });

            // --- Odessa, ranger captain ---
            Node("odessa_intro",
                new DialogueLine { speaker = "Odessa", text = "Smoke over Emberrock Ridge, three days running. My prospectors should've been back by now. Will you go look?", endRule = "choice", yesNode = "odessa_yes", noNode = "odessa_no" });
            Node("odessa_yes",
                new DialogueLine { speaker = "Odessa", text = "The bridge is west of here. Korrin runs that ridge like a private kingdom. Talk sense into him - however much sense takes.", questId = "q_emberrock", endRule = "end" });
            Node("odessa_no",
                new DialogueLine { speaker = "Odessa", text = "The smoke won't wait forever. Neither will I.", endRule = "end" });
            Node("odessa_loop",
                new DialogueLine { speaker = "Odessa", text = "The Hollow... my grandmother spoke of it like weather. Something that just happens to the Vale, when it happens.", endRule = "end" });

            // --- Tobin, rival ---
            Node("tobin_fight",
                new DialogueLine { speaker = "Tobin", text = "Heard you caught something! Ranch rules: we battle to see whose meadow education stuck. Let's go!", spawnTrainer = "trainer_tobin", endRule = "end" });
            Node("tobin_loop",
                new DialogueLine { speaker = "Tobin", text = "Rematch anytime. I'm building a team that'll make the ridge look tame.", endRule = "end" });

            // --- Sela, herbalist ---
            Node("sela_intro",
                new DialogueLine { speaker = "Sela", text = "You reek faintly of the Hollow, lantern-bearer. Good. I have work for exactly your kind of trouble. Will you hear it?", endRule = "choice", yesNode = "sela_yes", noNode = "sela_no" });
            Node("sela_yes",
                new DialogueLine { speaker = "Sela", text = "The Verdant Sigil, in three fragments. Root sleeps under meadow stones, Tide sank with the lake traffic, Ember burned into the ridge fires. Bring me all three.", questId = "q_sigil", endRule = "end" });
            Node("sela_no",
                new DialogueLine { speaker = "Sela", text = "The fragments will keep. Unlike the Vale.", endRule = "end" });
            Node("sela_loop",
                new DialogueLine { speaker = "Sela", text = "Grind bitterleaf, not your teeth. The Hollow feeds on hurry.", endRule = "end" });

            // --- Korrin, prospector ---
            Node("korrin_fight",
                new DialogueLine { speaker = "Korrin", text = "Odessa sent you? The ridge is MINE by right of finding it. Show me your creatures are worth more than my claim!", spawnTrainer = "trainer_korrin", endRule = "end" });
            Node("korrin_loop",
                new DialogueLine { speaker = "Korrin", text = "Fine, fine, the smoke's from the Hollow monolith. My digging woke it. Take your pick and leave me my claim.", openCraft = "forge", endRule = "end" });

            // --- Vesna, angler --- (teaches fishing basics)
            Node("vesna_intro",
                new DialogueLine { speaker = "Vesna", text = "Mirrorlake's gone quiet. Even the gildfish are nervous, and gildfish don't know enough to be nervous.", endRule = "" },
                new DialogueLine { speaker = "Vesna", text = "Here - how to cast so the line doesn't snap. Go on, the shoals are free.", teach = "Fisher:1", endRule = "end" });
            Node("vesna_loop",
                new DialogueLine { speaker = "Vesna", text = "Cast a line. The lake calms when it's fished. Always has.", endRule = "end" });

            // --- Hollis, bard ---
            Node("hollis_fight",
                new DialogueLine { speaker = "Hollis", text = "A duel, then! Every saga needs one at the lakeside. Try to leave me a good verse for my defeat!", spawnTrainer = "trainer_hollis", endRule = "end" });
            Node("hollis_loop",
                new DialogueLine { speaker = "Hollis", text = "'The lantern-bearer came, saw, and humbled me.' - I'm still workshopping it.", endRule = "end" });

            // --- Zephy, herald ---
            Node("zephy_fight",
                new DialogueLine { speaker = "Zephy", text = "The Vale forgets. The Sleepers remember. I am the bell before the quiet. Ring, little light.", spawnTrainer = "trainer_zephy", endRule = "end" });
            Node("zephy_loop",
                new DialogueLine { speaker = "Zephy", text = "So the Vale keeps its noise a while longer. Go down. Wake it gently.", endRule = "end" });

            // --- The Sleeper (finale) ---
            Node("sleeper_final",
                new DialogueLine { speaker = "The Sleeper", text = "Little light. You climbed down carrying a dead god's sigil and a living flame.", endRule = "choice", yesNode = "sleeper_wake", noNode = "sleeper_wait" });
            Node("sleeper_wake",
                new DialogueLine { speaker = "The Sleeper", text = "Then wake me. The Vale dreamed itself a guardian and forgot to tell it the war ended. I will hold. Take Gildra - my last gift to your small fires.", setFlag = "sleeper_awake", spawnCreature = "gildra:20", endRule = "end" });
            Node("sleeper_wait",
                new DialogueLine { speaker = "The Sleeper", text = "Then sit. The dark is gentler than its reputation.", endRule = "end" });
            Node("sleeper_done",
                new DialogueLine { speaker = "The Sleeper", text = "The Vale holds. Go tend your professions, little light. Grow things.", endRule = "end" });

            // ================= TRAINERS =================
            T(new TrainerDef
            {
                id = "trainer_tobin",
                name = "Tobin",
                title = "Ranch hand",
                team = new[] { "burrowmouse:5", "flitwing:6" },
                preBattle = new[] { "Meadow rules - no crying!" },
                winLines = new[] { "Ha! The meadow likes you better. Fine - you've earned the gate road." },
                loseLines = new[] { "Told you! Ranch hands rule the meadow!" },
                onDefeatFlags = new[] { "beat_tobin" },
                onDefeatGiveItems = new[] { "trail_snack:2" },
                rewardCoins = 80
            });
            T(new TrainerDef
            {
                id = "trainer_korrin",
                name = "Korrin",
                title = "Ridge prospector",
                team = new[] { "magmite:9", "cragma:10", "ashkit:9" },
                preBattle = new[] { "The ridge is mine by right of finding it!" },
                winLines = new[] { "Fine! FINE. The smoke's from the Hollow monolith - my deep digging woke it. Take the Root fragment I pried loose." },
                loseLines = new[] { "Come back when your creatures can lift a pick." },
                onDefeatFlags = new[] { "ridge_opened" },
                onDefeatGiveItems = new[] { "sigil_piece_a:1", "copper_ore:3" },
                rewardCoins = 150
            });
            T(new TrainerDef
            {
                id = "trainer_hollis",
                name = "Hollis",
                title = "Wandering bard",
                team = new[] { "mothlumen:12", "dreamora:13" },
                preBattle = new[] { "Every saga needs a lakeside duel!" },
                winLines = new[] { "A verse for the victor! Take these gildfish - a bard pays in dinner." },
                loseLines = new[] { "And THAT is how you write a defeat ballad!" },
                onDefeatFlags = new[] { "beat_hollis" },
                onDefeatGiveItems = new[] { "sigil_piece_b:1", "gildfish:2" },
                rewardCoins = 160
            });
            T(new TrainerDef
            {
                id = "trainer_zephy",
                name = "Zephy",
                title = "Hollow Herald",
                team = new[] { "sleeperling:16", "frostwisp:16", "nocturne:18" },
                preBattle = new[] { "The bell before the quiet. Ring, little light." },
                winLines = new[] { "So the Vale gets to keep its noise a while longer. The Ember fragment still smolders in the ridge ores - mine it out. Then go down and wake it gently." },
                loseLines = new[] { "The quiet comes either way. I merely hurry it." },
                onDefeatFlags = new[] { "herald_down" },
                onDefeatGiveItems = new[] { "hearty_stew:2" },
                rewardCoins = 300
            });
        }

        // ---------------- builders ----------------
        static void Npc(string id, string name, string role, string colorHex, string zone, string[] nodes)
        {
            Npcs[id] = new NpcDef { id = id, name = name, role = role, colorHex = colorHex, zone = zone, dialogueNodes = nodes };
        }

        static void Shop(string id, string name, string[] stock)
        {
            Shops[id] = new ShopDef { id = id, name = name, stock = stock };
        }

        static void Q(QuestDef q) => Quests[q.id] = q;

        static void T(TrainerDef t) => Trainers[t.id] = t;

        static void Node(string id, params DialogueLine[] lines)
        {
            var n = new DialogueNode { id = id };
            n.lines.AddRange(lines);
            Dialogue[id] = n;
        }

        public static DialogueNode GetNode(string id)
        {
            DialogueNode n;
            return Dialogue != null && Dialogue.TryGetValue(id, out n) ? n : null;
        }
    }
}
