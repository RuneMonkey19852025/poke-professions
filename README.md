# Emberwild Vale — a creature-taming RPG with professions & crafting

A complete, single-scene **Unity 6** game: tame 24 procedurally-built creatures, master
6 professions, craft gear and capture cores, and follow a 3-act story to wake what
dreams beneath the Hollow. All visuals, sounds and UI are generated from code —
no external assets, no network, no API keys.

---

## Quick start

1. Install **Unity Hub** (https://unity.com/download) and through it the editor
   **6000.0.84f1 (LTS)** — or any Unity 6000.0.x.
2. In Hub: **Add project →** select this folder (`UnityProjects/PokeProfessions`).
3. Open it. First import takes a minute; the scene `Assets/Scenes/GameScene.unity`
   auto-creates on first open (via `Assets/Editor/EditorSceneBootstrap.cs`).
4. If the scene is ever empty: create an empty scene, add a GameObject with the
   `GameBootstrap` component, save as `Assets/Scenes/GameScene.unity`.
5. Press **Play**.

## Controls

| Key | Action |
|-----|--------|
| WASD / arrows | Move (camera-relative) |
| Shift (hold) | Run (2.85 u/s) — walking (0.78 u/s) is the default gear |
| Mouse wheel | Zoom |
| E | Interact / advance dialogue |
| Space / Left click | Swing weapon (overworld melee) |
| Q | Weapons (trainer melee weapons) |
| Y / N | Answer dialogue choices |
| I | Bag (browse & use items) |
| G | Gear (trainer & creature equipment) |
| J | Journal (quests, professions, bestiary) |
| C | Creatures (party, items, equip, rest) |
| Q | Weapons & armour (your own gear) |
| F5 | Save (also autosaves every 25s) |
| Esc | Close top panel |
| Arrows / WASD in battle | Move cursor in battle menus · E/Enter confirm · Esc/Backspace back |

## The loop

- **Professions** — Forager, Miner, Fisher, Chef, Smith, Tailor. Press E at
  bushes / veins / fishing spots to gather; XP accumulates automatically.
  Levels raise yields (Lv4 +1, Lv8 +2) and unlock recipes. The start area has
  its own **Lv2 copper/iron outcrops** ringing Emberpost so a new Miner can
  work the town instead of walking out to the ridge — the ridge veins are the
  richer ones, and only they drop the Ember sigil fragment.
- **Crafting** — Forge / Campfire / Workbench / Weaponsmith's Bench /
  Armourer's Stand / Gearwright's Table / anywhere-recipes. Craft capture cores
  (Timber → Ironbound → Moonlit), healing food, profession gear (gloves, pick,
  rod, lure charm, scout band), and the character gear ladder below. The
  Gearwright's Table is the generalist bench — the one that works *both* gear
  ladders — set on the plaza's north row for a trainer who just wants gear.
- **Weapon mastery** — KOs with an equipped weapon level its class (claws,
  fangs, spines: +0.6% ATK/level, +60% at Lv100); victories while wearing
  armor level its class (plates, charms: −0.5% damage taken/level, −50% at
  Lv100). See the Gear panel and the journal.
- **Taming** — Fight wild creatures in zones (meadow lv2-6, ridge lv8-12, lake
  lv6-10, hollow lv10-14, deep hollow lv14-18). Weaken them, then BAG → throw a
  core. Caught creatures join your party (6) or box.
- **Battles** — Turn-based: FIGHT / BAG / SWITCH / RUN. 12 elements with full
  effectiveness chart, STAB bonus, statuses (burn/poison/sleep/paralysis), buffs,
  draining moves. Enemies level with their zone. A turn plays out on screen
  rather than resolving silently: the engine records what happened as an ordered
  list of beats, so both creatures act in the order they really resolved — the
  player's swing, then the enemy's answer, each with its own lunge and a flinch
  (or a death pose) on whoever was hit. A whiffed or no-damage move still swings;
  it just leaves the target unbothered. The bodies on the stage track the active
  creatures, so a SWITCH brings the new one on immediately and a faint's
  replacement leaps in once the exchange is over — and every creature enters
  rather than appearing: the 31 of 44 species whose packs ship an arrival clip
  play it, and the other 13 take the field on a code-driven leap
  (`CreatureVisual.EnterField` falls back to a synthetic send-out).
- **Trainer melee combat** — wild creatures roam the world, and they **fight
  back**: close in or land a hit and one turns on you, swinging on a timer and
  taking health off your own pool. Run out and you go down — you wake up on
  Emberpost's plaza, healed, and whatever put you down shakes the fight off.
  Swing at them with Space or left click: bare hands poke for 2, or equip a
  weapon from the Q panel. The body swings with the weapon — the attack clip is
  time-scaled to the weapon's cooldown, so a blade is quick and an axe is not —
  and flinches when a bite lands. Landing hits and KOs levels your weapon skill (max
  +50% damage at Lv50); KOs drop the same wild loot and coins a battle win
  would.
- **Weapons & armour, Normal → Legendary** — four weapon kinds (club, blade,
  spear, axe) and a body-armour slot, five tiers each. Weapon damage is the
  kind's base scaled by the tier, so a Legendary club (20) hits exactly like a
  Normal axe; the tier also sets the price and the colour. Armour soaks a flat
  amount off every wild hit (2 → 16) and widens your health pool (60 → 112), so
  it is what you stand behind. Craft the whole ladder at the **Weaponsmith's
  Bench** and the **Armourer's Stand** in Emberpost (or the **Gearwright's
  Table**, which works both): each tier costs two more profession levels
  (Lv2 → Lv10) and climbs from fibre-and-stone to moonshard-and-gildfish, so
  the top rungs are genuinely late-game. In a hurry, **Hask the Quartermaster**
  — the trader by Emberpost's market row — sells the ladder outright, but his
  rack grows with the story: he opens on the two starter tiers (Normal +
  Uncommon) and gains one more as the Vale does — Rare once the ridge opens,
  Epic once the Verdant Sigil is assembled, and the Legendary rungs only after
  the Sleeper is woken. His shop spells that out — it lists the rungs he can't
  deal yet and what opens each, so the rack never reads as a short ladder.
  The Q panel shows the whole ladder the same way: the rungs you have are the
  clickable ones, and the ones still ahead stay on the list greyed, each saying
  whether to buy it from Hask or forge it and at what profession level. Buying
  costs full price, so forging stays the cheaper route to the same gear.
- **The Lucky Snorlax** — slot machines in Emberpost (one at the spawn plaza,
  one by the Vale Post stall), 25c a spin. Wins pay coins and drop crafting
  materials; every losing spin feeds the shared progressive jackpot, and
  triple stars pay 500c + a Moonshard **plus the whole pot**. Back-to-back
  wins build a hot hand: +25% coins per streak level (max +100%) and bonus
  material drops from streak 3.
- **Story** — 3 acts, 8 quests. See walkthrough below.

## Quest walkthrough (spoilers)

1. **First Core** (Brann): gather fiber + stone, craft rope (Tailor 1), then a
   Timber Core at Brann's forge (Smith 1). 
2. **First Friend** (Maren): weaken and catch any wild creature.
3. **Rival Winds** (Tobin): beat the ranch hand (lv5-6 team).
4. **Hands for the Vale** (Juna): get three professions to Lv2. Reward: Forager Gloves.
5. **Smoke over Emberrock** (Odessa): cross the west bridge, defeat Korrin
   (lv9-10). Reward: Reinforced Pick + **Sigil Fragment: Root**.
6. **The Verdant Sigil** (Sela): 
   - Root — from Korrin
   - Tide — defeat Hollis the bard at the lake (lv12-13)
   - Ember — rare drop from ridge veins (Copper/Iron), ~1 in 8 rocks
7. **The Hollow Herald** (Zephy): defeat her team (lv16-18).
8. **The Hollow Sleepers** (The Sleeper): wake it to complete the story and
   receive **Gildra**, the legendary golden beast.

## Project layout

```
Assets/Scripts/Core/      data + systems (DBs, battle, quests, professions, save)
Assets/Scripts/Story/     the authored narrative (quests, dialogue, trainers, shops)
Assets/Scripts/World/     terrain, player, camera, interactables, slots machine
Assets/Scripts/UI/        all panels (built in code via UIFactory)
Assets/Scripts/Audio/     procedural SFX synthesizer
Assets/Shaders/           vertex-color terrain shader
Assets/Editor/            one-time scene bootstrap
```

## Notes

- Saves live in PlayerPrefs (`pokeprofessions_save_v1`). "New Game" on the title
  screen wipes them.
- The scene bootstrap creates the scene on first open; you can delete
  `Assets/Editor/` afterwards.
- To add content: extend `StoryData` (quests/dialogue/trainers), `CreatureDB`,
  `ItemDB` recipes, or `NodeDB` gathering nodes — all plain data tables.
- **Offline typecheck** without the editor open (requires one prior editor
  import so `Library/ScriptAssemblies/UnityEngine.UI.dll` exists):

  ```bash
  Tools/typecheck.sh
  ```

  Compiles all game scripts against the real Unity managed DLLs with the
  Roslyn compiler bundled with the editor. Set `UNITY_EDITOR=...` to use a
  different editor install.

  (The editor scripts under `Assets/Editor/` are excluded from that script;
  check them with the same command after adding `Assets/Editor` to the file
  list, dropping the extra `UnityEditor.dll` reference.)
- **Automated playtest** — drop the request markers and open the project; the
  editor opens `GameScene`, presses through the title, equips every trainer
  weapon, swings a wild target to a KO, opens the weapon panel and inspects the
  nature props, then writes `Logs/playtest.txt` and exits 0/1:

  ```bash
  printf go > Assets/Playtest.request && printf go > Assets/Playtest.exit
  ALSA_CONFIG_PATH="$PWD/Tools/alsa-null.conf" "$HOME/Unity/Hub/Editor/6000.6.3f1/Editor/Unity" \
    -projectPath "$PWD" -screen-width 1280 -screen-height 720
  ```

  `-screen-width/-screen-height` are not decoration: launched without them the
  editor can come up with a 0x0 Game View (an unsized window), which culls
  renderer-based animation and makes the editor's own `GUIView` spam
  `RenderTexture.Create failed` into the run's error count. The report then shows
  pose and bone-motion checks failing on an otherwise healthy project. The game
  no longer depends on that (`ModelLoader` forces `AlwaysAnimate`), and the
  playtest ignores editor-chrome errors, but sizing the window keeps the whole
  run honest.

  See `Assets/Editor/EditorPlaytest.cs`. (Launch the editor directly rather than
  `launch.sh`, which would also fire its own AutoPlay request.)
- **Playing without the editor** — `./run-game.sh` builds on nothing but the
  built player in `Builds/Linux/`, so no editor has to be open (or even
  installed at play time):

  ```bash
  ./run-game.sh                        # play
  systemctl --user stop emberwild-game # quit
  ```

  It hands the game to a systemd user unit, so it survives the shell that
  started it, and it exports `ALSA_CONFIG_PATH` for the same reason `launch.sh`
  does — the *player* hangs on this host too, in exactly the same place: FMOD
  initialises the AudioManager, its ALSA probe never returns on the PipeWire
  default PCM, and the game freezes with no window and a log that stops dead
  after `Vulkan PSO: Cache data successfully loaded`. Do not launch the binary in
  `Builds/Linux/` by hand for the same reason. The null PCM means the game plays
  silent, which it already does here: the only PipeWire sink is a Dummy Output.

  Rebuild it after changing code:

  ```bash
  ALSA_CONFIG_PATH="$PWD/Tools/alsa-null.conf" "$HOME/Unity/Hub/Editor/6000.6.3f1/Editor/Unity" \
    -batchMode -quit -nographics -projectPath "$PWD" \
    -executeMethod PokeProfessions.EditorTools.GameBuild.BuildLinux -logFile "$PWD/Logs/build-linux.log"
  ```

  The build needs the project to itself, so close the editor first.
- **Launching the editor** — go through `launch.sh` (it exports
  `ALSA_CONFIG_PATH`), not Unity Hub. Hub skips that export and the editor then
  freezes during startup: FMOD initialises as part of loading the AudioManager
  singleton, its ALSA probe calls `snd_pcm_prepare()` on the "default" PCM, and
  on this machine that is the PipeWire plugin and the call blocks forever. The
  freeze happens right after the log prints
  `[Package Manager] log level set to [2]`, so it looks exactly like a Package
  Manager hang. `Tools/alsa-null.conf` sends the default PCM to a null device
  for that one process — nothing outside the editor is touched:

  ```bash
  ALSA_CONFIG_PATH="$PWD/Tools/alsa-null.conf" "$HOME/Unity/Hub/Editor/6000.6.3f1/Editor/Unity" -projectPath "$PWD"
  ```
- **The "packages with errors" dialog** — the editor warns on open that the
  project has packages with errors, but names none and logs nothing. The reason
  only exists in the Package Manager's own state, which
  `Assets/Editor/EditorPackageProbe.cs` dumps to `Logs/packages.txt` (name,
  version, source, path and every `PackageInfo.errors` entry):

  ```bash
  ALSA_CONFIG_PATH="$PWD/Tools/alsa-null.conf" "$HOME/Unity/Hub/Editor/6000.6.3f1/Editor/Unity" \
    -batchMode -quit -nographics -projectPath "$PWD" \
    -executeMethod PokeProfessions.EditorTools.EditorPackageProbe.Run
  ```

  It exits 0 when the project is clean. On this host the dialog tracked the
  *deprecated* entries in `Packages/manifest.json`, not a broken resolve: the
  Package Manager reported every package with empty `errors`, while
  `com.unity.analytics` and IAP 4's `com.unity.purchasing` carried
  `isDeprecated: 1`. No code references either, so both were dropped from the
  manifest, along with the equally unused `com.unity.xr.legacyinputhelpers` and
  `com.unity.ai.navigation` — which also removed their indirect pulls
  (`services.core`, `services.analytics`, `nuget.newtonsoft-json`). The project
  now registers 52 packages, has no `isDeprecated` entry anywhere in the
  resolution, and the build and the playtest are unaffected.
  `com.unity.multiplayer.center` stays: it ships with the editor (source
  `BuiltIn`) and Unity manages it itself.
