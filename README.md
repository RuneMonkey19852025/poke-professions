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
| Mouse wheel | Zoom |
| E | Interact / advance dialogue |
| Y / N | Answer dialogue choices |
| I | Bag (browse & use items) |
| G | Gear (trainer & creature equipment) |
| J | Journal (quests, professions, bestiary) |
| C | Creatures (party, items, equip, rest) |
| F5 | Save (also autosaves every 25s) |
| Esc | Close top panel |

## The loop

- **Professions** — Forager, Miner, Fisher, Chef, Smith, Tailor. Press E at
  bushes / veins / fishing spots to gather; XP accumulates automatically.
  Levels raise yields (Lv4 +1, Lv8 +2) and unlock recipes.
- **Crafting** — Forge / Campfire / Workbench / anywhere-recipes. Craft capture
  cores (Timber → Ironbound → Moonlit), healing food, and profession gear
  (gloves, pick, rod, lure charm, scout band).
- **Taming** — Fight wild creatures in zones (meadow lv2-6, ridge lv8-12, lake
  lv6-10, hollow lv10-14, deep hollow lv14-18). Weaken them, then BAG → throw a
  core. Caught creatures join your party (6) or box.
- **Battles** — Turn-based: FIGHT / BAG / SWITCH / RUN. 12 elements with full
  effectiveness chart, STAB bonus, statuses (burn/poison/sleep/paralysis), buffs,
  draining moves. Enemies level with their zone.
- **The Lucky Snorlax** — slot machines in Emberpost (one at the spawn plaza,
  one by the Vale Post stall), 25c a spin. Wins pay coins and drop crafting
  materials; every losing spin feeds the shared progressive jackpot, and
  triple stars pay 500c + a Moonshard **plus the whole pot**.
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
- **Offline typecheck** without the editor: `Tools/typecheck.csproj` compiles all
  game scripts against minimal Unity API stubs (`Tools/UnityStubs*.cs`). Build it
  with the .NET SDK bundled with the Unity editor:

  ```bash
  U=~/Unity/Hub/Editor/6000.6.3f1
  export DOTNET_ROOT=$U/Editor/Data/DotNetSdk
  cd Tools && $U/Editor/Data/NetCoreRuntime/dotnet build typecheck.csproj
  ```

  If the game uses a new Unity API, add it to the stubs. `#if UNITY_EDITOR`
  blocks are excluded from the typecheck automatically.
