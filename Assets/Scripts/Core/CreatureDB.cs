using System.Collections.Generic;

namespace PokeProfessions.Core
{
    public static class MoveDB
    {
        public static Dictionary<string, MoveDef> All;

        public static void Init()
        {
            All = new Dictionary<string, MoveDef>();
            M("tackle", "Tackle", Element.Normal, 18, 100, 35, "A straightforward charge.", "");
            M("scratch", "Scratch", Element.Normal, 16, 100, 35, "Quick claws.", "");
            M("quickpeck", "Quick Peck", Element.Gale, 14, 100, 30, "A rapid jab of beak.", "");
            M("bodyslam", "Body Slam", Element.Normal, 34, 90, 20, "Full-weight slam.", "status", "paralyze", 20);
            M("emberbit", "Ember Bit", Element.Ember, 20, 100, 25, "A snapping spark.", "status", "burn", 10);
            M("flurrena", "Flurrena", Element.Ember, 42, 90, 15, "A whipping cone of flame.", "status", "burn", 15);
            M("tuftide", "Tidepuff", Element.Tide, 20, 100, 25, "A knockabout splash.", "");
            M("cinder", "Cinder Burst", Element.Ember, 44, 90, 12, "An erupting shell of embers.", "");
            M("ripple", "Riplash", Element.Tide, 40, 95, 15, "A coiling water lash.", "");
            M("leafdart", "Leaf Dart", Element.Verdant, 18, 100, 30, "A flicked razor leaf.", "");
            M("briar", "Briar Cage", Element.Verdant, 38, 90, 15, "Thorns lash out.", "status", "poison", 20);
            M("spark", "Spark", Element.Volt, 18, 100, 25, "A static nip.", "status", "paralyze", 20);
            M("arc", "Arc Bolt", Element.Volt, 44, 90, 12, "A snapping bolt.", "status", "paralyze", 10);
            M("pebble", "Pebble Toss", Element.Stone, 20, 95, 25, "Hurl loose stone.", "");
            M("landslide", "Landslide", Element.Stone, 46, 85, 10, "A crushing fall of rock.", "");
            M("gust", "Gust", Element.Gale, 18, 100, 30, "A puffing blast of air.", "");
            M("cyclone", "Cyclone Cut", Element.Gale, 42, 90, 12, "A whirling blade of wind.", "");
            M("frostbite", "Frostbite", Element.Frost, 20, 100, 25, "A biting chill.", "status", "burn", 10);
            M("glacier", "Glacier Crash", Element.Frost, 46, 85, 10, "A heavy rush of ice.", "");
            M("venomspit", "Venom Spit", Element.Toxic, 16, 100, 25, "Sputtering toxin.", "status", "poison", 35);
            M("miasma", "Miasma", Element.Toxic, 38, 90, 12, "A rolling fog of fumes.", "status", "poison", 30);
            M("mindnip", "Mind Nip", Element.Mind, 20, 100, 25, "A sting of pure thought.", "");
            M("lucence", "Lucence", Element.Mind, 44, 90, 12, "A lance of focused will.", "");
            M("shadeslip", "Shade Slip", Element.Shade, 20, 100, 25, "A strike from nowhere.", "");
            M("nightfall", "Nightfall", Element.Shade, 44, 90, 12, "Darkness bites deep.", "");
            M("gnaw", "Gnaw", Element.Beast, 22, 100, 25, "Savage bite.", "");
            M("ravage", "Ravage", Element.Beast, 46, 85, 10, "A wild flurry of fangs.", "");
            M("harden", "Harden", Element.Stone, 0, 0, 20, "Raises DEF.", "buff_def", "", 0);
            M("growl", "Growl", Element.Normal, 0, 0, 20, "Lowers foe's ATK.", "debuff_atk", "", 0);
            M("focus", "Focus", Element.Mind, 0, 0, 20, "Raises ATK.", "buff_atk", "", 0);
            M("mend", "Mend", Element.Verdant, 0, 0, 10, "Restores some HP.", "heal", "", 0);
            M("soothe", "Soothe", Element.Mind, 0, 0, 10, "Restores more HP.", "heal", "", 0);
            M("leech", "Sap Sip", Element.Verdant, 22, 95, 15, "Drains HP to heal itself.", "drain", "", 0);
            M("lullaby", "Lull Drone", Element.Mind, 0, 75, 10, "Puts the foe to sleep.", "status", "sleep", 100);
            M("flashstep", "Flash Step", Element.Gale, 26, 100, 20, "Always strikes first.", "");
            M("bulwark", "Bulwark", Element.Stone, 30, 95, 20, "A brace and counterweight.", "");
            M("springshoot", "Spring Shoot", Element.Verdant, 26, 100, 20, "A sudden rising stalk.", "");
            M("douse", "Douse", Element.Tide, 26, 100, 20, "A aimed jet of water.", "");
            M("mudsling", "Mud Sling", Element.Stone, 24, 100, 20, "Wet, heavy earth.", "");
            M("singsong", "Singsong", Element.Normal, 0, 80, 10, "A drowsy tune.", "status", "sleep", 100);
            M("fume", "Cinder Fume", Element.Ember, 26, 100, 20, "Acrid smoky breath.", "status", "burn", 20);
        }

        static void M(string id, string name, Element el, int pow, int acc, int pp, string desc,
                      string effect = "", string status = "", int statusChance = 0)
        {
            All[id] = new MoveDef
            {
                id = id, name = name, element = el, power = pow, accuracy = acc, pp = pp,
                desc = desc, Effect = effect, statusId = status, statusChance = statusChance
            };
        }

        public static MoveDef Get(string id)
        {
            MoveDef m;
            return All != null && All.TryGetValue(id, out m) ? m : null;
        }
    }

    public static class CreatureDB
    {
        public static Dictionary<string, SpeciesDef> All;
        public static List<string> Order;

        public static void Init()
        {
            All = new Dictionary<string, SpeciesDef>();
            Order = new List<string>();

            // ---------- Meadowfall (starting zone) ----------
            S("sprigling", "Sprigling", Element.Verdant, 12, 11, 10, 9,
              "A sprouting seed-fox that naps in sunbeams.",
              "thornpaw", 14, new[] { "tackle", "leafdart", "growl", "springshoot", "leech", "briar" },
              "#7FC46B", "#3E7A3A", "quad", 1f, 60, 22, "meadow");
            S("thornpaw", "Thornpaw", Element.Verdant, 16, 15, 13, 12,
              "Its bramble tail sweeps trails clean behind it.",
              "briarwolf", 30, new[] { "scratch", "leafdart", "springshoot", "leech", "briar", "focus" },
              "#5FA84F", "#2C5E2A", "quad", 1.25f, 45, 42, "meadow");
            S("briarwolf", "Briarwolf", Element.Verdant, 22, 22, 18, 15,
              "A guardian of overgrown ruins, patient and proud.",
              "", 0, new[] { "scratch", "leafdart", "briar", "focus", "ravage", "mend" },
              "#4A8A3E", "#1F4A1E", "quad", 1.55f, 30, 66, "meadow");

            S("puddlepup", "Puddlepup", Element.Tide, 13, 10, 11, 10,
              "Chases raindrops and puddles with equal joy.",
              "tidehound", 15, new[] { "tackle", "tuftide", "growl", "douse", "ripple", "bodyslam" },
              "#6FA8DC", "#33628F", "quad", 1f, 60, 22, "meadow");
            S("tidehound", "Tidehound", Element.Tide, 18, 14, 14, 14,
              "Its bark sounds like a wave clapping stone.",
              "maelmoor", 30, new[] { "tackle", "tuftide", "douse", "ripple", "bodyslam", "harden" },
              "#5B94C8", "#28527E", "quad", 1.3f, 45, 42, "meadow");
            S("maelmoor", "Maelmoor", Element.Tide, 24, 21, 19, 16,
              "Old riverfolk say it herds the currents themselves.",
              "", 0, new[] { "douse", "ripple", "bodyslam", "harden", "glacier", "soothe" },
              "#477FB3", "#1E4066", "quad", 1.6f, 30, 66, "meadow");

            S("cindercub", "Cindercub", Element.Ember, 12, 12, 9, 11,
              "Sleeps curled around its own warm tail.",
              "pyrelynx", 14, new[] { "scratch", "emberbit", "growl", "fume", "flurrena", "focus" },
              "#E8824A", "#A34A22", "quad", 1f, 60, 22, "meadow");
            S("pyrelynx", "Pyrelynx", Element.Ember, 17, 17, 12, 15,
              "Twin tail-flames flicker with its mood.",
              "solarmane", 30, new[] { "scratch", "emberbit", "fume", "flurrena", "focus", "quickpeck" },
              "#D96F35", "#8C3B18", "quad", 1.3f, 45, 42, "meadow");
            S("solarmane", "Solarmane", Element.Ember, 23, 24, 17, 19,
              "Its mane burns brightest at noon.",
              "", 0, new[] { "emberbit", "fume", "flurrena", "focus", "cinder", "ravage" },
              "#C25C28", "#6E2D12", "quad", 1.6f, 30, 66, "meadow");

            S("flitwing", "Flitwing", Element.Gale, 11, 11, 8, 13,
              "A pocket-sized storm in feathers.",
              "galedart", 13, new[] { "quickpeck", "gust", "growl", "flashstep", "cyclone", "bodyslam" },
              "#9FB6E8", "#5A6FA8", "avian", 0.9f, 65, 20, "meadow");
            S("galedart", "Galedart", Element.Gale, 15, 16, 11, 18,
              "Rides thermals over the whole valley in a morning.",
              "", 0, new[] { "quickpeck", "gust", "flashstep", "cyclone", "bodyslam", "focus" },
              "#8AA5DE", "#4A5F98", "avian", 1.2f, 45, 44, "meadow");

            S("burrowmouse", "Burrowmouse", Element.Normal, 12, 10, 10, 11,
              "Its cheeks are bigger than its ambitions.",
              "vaultmole", 12, new[] { "tackle", "scratch", "growl", "mudsling", "bulwark", "bodyslam" },
              "#C4A882", "#8A7050", "quad", 0.9f, 70, 20, "meadow");
            S("vaultmole", "Vaultmole", Element.Stone, 17, 14, 15, 8,
              "Digs counting-holes: one for every coin it finds.",
              "", 0, new[] { "scratch", "mudsling", "bulwark", "pebble", "landslide", "harden" },
              "#A88F6C", "#6E5A40", "quad", 1.15f, 50, 44, "meadow");

            // ---------- Emberrock Ridge ----------
            S("magmite", "Magmite", Element.Ember, 14, 13, 12, 6,
              "A slow blob of cooling lava. Warm to hold.",
              "cragma", 24, new[] { "emberbit", "pebble", "harden", "fume", "landslide", "flurrena" },
              "#D06838", "#7A3418", "rock", 1f, 55, 26, "ridge");
            S("cragma", "Cragma", Element.Stone, 20, 18, 20, 6,
              "What the lava left behind grew a temper.",
              "", 0, new[] { "pebble", "harden", "landslide", "bulwark", "emberbit", "fume" },
              "#9A6A4A", "#5A3A24", "rock", 1.4f, 40, 50, "ridge");
            S("ashkit", "Ashkit", Element.Ember, 13, 14, 9, 13,
              "Leaves gray footprints that glow briefly.",
              "smoulder", 22, new[] { "scratch", "emberbit", "flashstep", "fume", "flurrena", "growl" },
              "#B85A50", "#70302C", "quad", 1f, 55, 26, "ridge");
            S("smoulder", "Smoulder", Element.Ember, 19, 20, 13, 17,
              "Its purr is a low banked fire.",
              "", 0, new[] { "emberbit", "fume", "flurrena", "flashstep", "focus", "scratch" },
              "#A04840", "#5C2622", "quad", 1.3f, 40, 50, "ridge");

            // ---------- Mirrorlake / fishing ----------
            S("glimmfish", "Glimmfish", Element.Tide, 12, 9, 10, 12,
              "Schools sparkle like spilled coins.",
              "luminkeel", 16, new[] { "tuftide", "douse", "growl", "ripple", "soothe", "bodyslam" },
              "#8AC8D8", "#4A8898", "aquatic", 0.9f, 65, 22, "lake");
            S("luminkeel", "Luminkeel", Element.Tide, 18, 14, 14, 15,
              "Its fin-fronds glow where the water is deepest.",
              "", 0, new[] { "tuftide", "douse", "ripple", "soothe", "bodyslam", "glacier" },
              "#6FB0C4", "#3A7080", "aquatic", 1.25f, 45, 46, "lake");
            S("lilypad", "Lilypad", Element.Verdant, 14, 9, 13, 8,
              "A patient floater. Quite content. Do not disturb.",
              "bloomdrift", 18, new[] { "leafdart", "leech", "harden", "springshoot", "mend", "briar" },
              "#7AB86A", "#4A7A40", "aquatic", 1f, 60, 24, "lake");
            S("bloomdrift", "Bloomdrift", Element.Verdant, 20, 14, 18, 10,
              "It blooms once a year, magnificently, then naps.",
              "", 0, new[] { "leafdart", "leech", "springshoot", "mend", "briar", "soothe" },
              "#68A85A", "#3A6A32", "aquatic", 1.3f, 45, 48, "lake");

            // ---------- Hushpine Hollow ----------
            S("mothlumen", "Mothlumen", Element.Mind, 12, 11, 9, 12,
              "Wings dusted with dream-pollen.",
              "dreamora", 20, new[] { "mindnip", "lullaby", "soothe", "lucence", "gust", "growl" },
              "#C8A8E0", "#8068A8", "bug", 0.9f, 60, 24, "hollow");
            S("dreamora", "Dreamora", Element.Mind, 18, 17, 13, 16,
              "It hums the thoughts you haven't said aloud.",
              "", 0, new[] { "mindnip", "lullaby", "soothe", "lucence", "nightfall", "focus" },
              "#B090CC", "#68508C", "bug", 1.25f, 45, 48, "hollow");
            S("gloomcap", "Gloomcap", Element.Toxic, 14, 12, 12, 7,
              "A grumpy mushroom with opinions about weather.",
              "sporesage", 22, new[] { "venomspit", "harden", "leech", "miasma", "springshoot", "growl" },
              "#9A78B0", "#5C4874", "bug", 1f, 60, 25, "hollow");
            S("sporesage", "Sporesage", Element.Toxic, 20, 18, 17, 9,
              "Speaks in spore-clouds. The trees listen.",
              "", 0, new[] { "venomspit", "miasma", "leech", "springshoot", "mend", "harden" },
              "#8668A0", "#4C3860", "bug", 1.35f, 45, 50, "hollow");
            S("umbrafox", "Umbrafox", Element.Shade, 15, 15, 10, 14,
              "Seen only as a smile, and then not even that.",
              "nocturne", 24, new[] { "shadeslip", "growl", "nightfall", "lullaby", "ravage", "mindnip" },
              "#6A5A8A", "#3A3054", "quad", 1f, 55, 28, "hollow");
            S("nocturne", "Nocturne", Element.Shade, 21, 22, 14, 18,
              "The Hollow's own shadow, walking politely abroad.",
              "", 0, new[] { "shadeslip", "nightfall", "lullaby", "ravage", "mindnip", "focus" },
              "#564873", "#2A2240", "quad", 1.3f, 40, 54, "hollow");

            // ---------- The Hollow (rare) ----------
            S("sleeperling", "Sleeperling", Element.Mind, 16, 14, 14, 12,
              "A fragment of a Hollow Sleeper, dreaming itself awake.",
              "", 0, new[] { "mindnip", "lullaby", "soothe", "lucence", "nightfall", "focus" },
              "#B8C8F0", "#6878A8", "spirit", 1.1f, 25, 40, "hollow_deep");
            S("frostwisp", "Frostwisp", Element.Frost, 15, 14, 11, 16,
              "A cold breath the mountain forgot to exhale.",
              "", 0, new[] { "frostbite", "gust", "soothe", "glacier", "cyclone", "lullaby" },
              "#BFE4E8", "#6AA8B0", "spirit", 1f, 28, 38, "hollow_deep");
            S("gildra", "Gildra", Element.Beast, 22, 20, 16, 18,
              "A golden-maned guardian beast from valley legend.",
              "", 0, new[] { "gnaw", "ravage", "focus", "bulwark", "cyclone", "nightfall" },
              "#E0B84A", "#A87C28", "quad", 1.5f, 15, 60, "hollow_deep");
        }

        static void S(string id, string name, Element el, int hp, int atk, int def, int spd,
                      string desc, string evo, int evoLvl, string[] learnset,
                      string body, string accent, string shape, float scale,
                      int catchRate, int xpYield, string zone)
        {
            All[id] = new SpeciesDef
            {
                id = id, name = name, element = el, element2 = el,
                baseHP = hp, baseATK = atk, baseDEF = def, baseSPD = spd,
                desc = desc, evolvesInto = evo, evolveLevel = evoLvl,
                learnset = new List<string>(learnset),
                paletteHex = body, accentHex = accent, shape = shape, scale = scale,
                catchRate = catchRate, xpYield = xpYield, habitatZone = zone
            };
            Order.Add(id);
        }

        public static SpeciesDef Get(string id)
        {
            SpeciesDef s;
            return All != null && All.TryGetValue(id, out s) ? s : null;
        }

        public static List<string> WildPoolFor(string zone)
        {
            var list = new List<string>();
            foreach (var kv in All)
                if (kv.Value.habitatZone == zone || (zone == "meadow" && kv.Value.habitatZone == "lake"))
                    list.Add(kv.Key);
            return list;
        }

        // Moves referenced above that live in a later tier — safety net so
        // any typo'd move id resolves to tackle instead of a null crash.
        public static string SafeMove(string id) => Get(id) != null ? id : "tackle";
    }
}
