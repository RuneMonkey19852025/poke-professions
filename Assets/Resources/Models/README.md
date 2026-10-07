# Model Packs (runtime-loaded from Resources/Models)

Third-party open-source packs used to replace procedural creature/NPC visuals.

| Folder | Pack | Author | License | Contents |
|---|---|---|---|---|
| `CuteMonsters/` | Cute Animated Monsters (Aug 2020) | Quaternius | CC0 | 21 textured animated monster FBX |
| `Quaternius/` | Monster/Fish/Farm Animals/Easy Enemies/Dinosaurs (Animated) | Quaternius | CC0 | 30 rigged animated FBX + textures |
| `Quaternius/Animals/` | Ultimate Animated Animal Pack (Jul 2021) | Quaternius | CC0 | 12 rigged animated mammals (Alpaca…Wolf), 12+ anims each |
| `Quaternius/Nature/` + `NatureTextures/` | Stylized Nature MegaKit (free, 68/116 models) | Quaternius | CC0 | 68 static foliage/rock/tree FBX + 20 diffuse PNGs, rendered via `KitProp` shader |
| `MantisHatchery/` | Mantis Hatchery Battle Creatures | MantisDA | **CC-BY-SA 4.0** | NOT referenced in code (all-CC0 policy). Present on disk only — safe to delete to slim builds |
| `KayKitAdventurers/` | KayKit Adventurers 2.0 (Free) | Kay Lousberg | CC0 | 6 textured rigged humans (`Rig_Medium`), clips from `KayKitAnimations/` |
| `KayKitSkeletons/` | KayKit Skeletons 1.1 (Free) | Kay Lousberg | CC0 | 4 rigged skeleton variants (mage/minion/rogue/warrior), `Rig_Medium`, + texture |
| `KayKitAnimations/` | KayKit Character Animations 1.1 | Kay Lousberg | CC0 | `Rig_Medium_{General,MovementBasic,CombatMelee}.fbx` — the shared clip library (see below) |

## Attribution

All mapped packs are CC0 — no attribution required. (If Mantis models were
re-enabled, ship: "Creature models: Mantis Hatchery Battle Creatures Pack by
MantisDA (CC-BY-SA 4.0)")

## Animation clip naming (as imported by Unity)

- Quaternius/CuteMonsters: `<Armature>|Idle`, `|Walk`, `|Attack`, `|Death`, `|HitRecieve`...
- Mantis: `idle`, `run`, `walk`, `bite`, `alerted_*`, `stagger_*`, `die_*` (one FBX per clip)

CreatureVisual maps these to the game's anim states (idle/walk/attack/hit/death).

## Model scale

Packs ship at wildly different raw unit scales (Quaternius mammals ~4–9 units,
dinosaurs 12–50, CuteMonsters ~1.45). `ModelLoader.ModelRawHeight` records each
mapped model's raw import height and `Instantiate` sizes it with

```
localScale = callerScale * ReferenceModelHeight(1.45) / rawHeight
```

which is final on the first frame. Nothing re-measures a model at runtime —
see the unit trap below for why that mattered.

Re-measure a model offline (no Unity needed) after swapping a pack file:

```bash
python3 Tools/fbx_probe.py Assets/Resources/Models/Quaternius/Monsters/Dragon.fbx
```

The probe reads the FBX vertex bounds directly (handles the zlib-compressed
array records these packs use) and prints the X/Y/Z sizes — the Y value is what
`ModelRawHeight` stores. Its output matches the recorded table for the
CuteMonsters/Quaternius packs, so it is safe to trust for new entries.

### The 100x unit trap (read before measuring a rigged model)

The creature packs are imported **Legacy** (`animationType: 1`) so their embedded
clips play, and a Legacy import bakes the FBX unit conversion into the
**skeleton nodes** (`AnimalArmature=100`, `Dragon=100`, `DragonArmature=100`, …)
while the mesh vertices stay in metres. Two consequences, both measured:

- **Order of magnitude.** The animated model renders at the raw FBX height
  (Deer 4.40u), but a *bind pose* bounds read is ~100x that — the same Deer
  reports `SkinnedMeshRenderer.bounds` 449.0u and `BakeMesh` 425u.
- **Timing.** A SkinnedMeshRenderer that is off-screen and not forced to update
  is never re-skinned, so it keeps reporting that bind pose indefinitely.
  `BakeMesh` on it reports the bind pose too, even 18 frames in.

`Renderer.bounds` is therefore only trustworthy on a rig that has actually been
skinned: `ModelLoader.Instantiate` calls `KeepSkinnedMeshesVisible`, which sets
`updateWhenOffscreen = true`, and after one frame the bounds report the animated
pose (Deer 1.53u at the size the loader set). Creature sizes in
`Logs/playtest.txt` are read from exactly that; the `bindpose=` number printed
next to them is the contrast case, not what the player sees.

Dividing a bounds read by the model's scale — the "measure the animated height,
then rescale" pass that used to live in `ModelSizer` — is what rendered the
creatures at 0.02u, and the same bind-pose read also lifted the dragon 29 units
off the ground. Sizing is consequently table-driven and fixed, and the only
runtime pass left is grounding: `ModelFooting` lifts the model so the union of
its renderer bounds sits at `y = 0` of the parent, a couple of frames in, and it
refuses any measurement more than 2.5x the expected height (a bind-pose read
would move the model by tens of units).

## Animation: the creature packs are Legacy, the humans are not

`CuteMonsters/` (21) and `Quaternius/` (42) — every model `ModelRawHeight` maps
to a creature — are imported with `animationType: 1` (Legacy) in their `.meta`.
Their embedded clips then register on a legacy `Animation` component, so
`ModelLoader.SetupAnimation` plays `idle` and `CreatureVisual.PlayState` can
switch to walk/run/attack/hit/death. The playtest asserts both that the idle
state landed and that it actually moves the skeleton (max bone delta over 30
frames).

Deliberately left at `animationType: 2` (Generic):

- `Quaternius/Nature/**` (68) — static props with nothing to play.
- `MantisHatchery/**` — unused (all-CC0 policy).

### The KayKit characters borrow a shared clip library

`KayKitAdventurers/` / `KayKitSkeletons/` (12 characters) ship **no clips of
their own**. They borrow them from **KayKit - Character Animations 1.1** (CC0,
`kaylousberg.com/game-assets/character-animations`), imported Legacy under two
folders so each family only ever sees clips authored for its own rig:

| Model path prefix | Library folder | Rig | Sets imported |
|---|---|---|---|
| `KayKitAdventurers` | `Models/KayKitAnimations/` | `Rig_Medium` | `Rig_Medium_{General,MovementBasic,CombatMelee}.fbx` |
| `KayKitSkeletons` | `Models/KayKitSkeletons/Animations/` | `Rig_Medium` Special | `Rig_Medium_Special.fbx` |

`ModelLoader.ClipLibrary` does that mapping, and `PreferredClips` pins the exact
clip per game state (`idle`→`Idle_A`, `walk`→`Walking_A`, `run`→`Running_A`,
`attack`→`Melee_1H_Attack_Chop`, `hit`→`Hit_A`, `death`→`Death_A`, and
`spawn`→`Spawn_Ground` / `Skeletons_Spawn_Ground` for the send-out). Keyword
matching alone would be wrong here: it takes `Melee_2H_Idle` for idle and
`Jump_Full_Long` for attack, because the attack keywords include "jump".

`spawn` is the odd one out: it is resolved by `MapSpawn`, a pass of its own that
runs *after* every other state has had its pick and only claims a clip nothing
else took. The clips that read as a creature taking the field are the packs'
jump clips, and "jump" is also an attack keyword — a creature whose only jump is
its attack would otherwise lose the attack to its arrival pose. Purely additive:
it can add a state, never re-point one. What each family has differs, so the
listing path is a chain of increasingly loose keywords: KayKit humans and
skeletons have real send-outs (`Spawn_Ground`, `Skeletons_Spawn_Ground`;
1.3 s and 3.6 s respectively), Quaternius Animals leap in and land on
`Jump_toIdle`, Dinosaurs carry `*_Jump`, most CuteMonsters a plain `Jump`.
Thirteen species resolve none at all: `glimmfish`, `luminkeel`, `aurorafin`,
`frostfin`, `rimefin` (the fish), `thornmare` (the dragon), `flitwing`,
`galedart`, `magmite`, `gloomcap`, `mothlumen`, `nocturne` and `gildra` —
measured: 31 of the 44 mapped
species resolve a `spawn` clip, and the coverage line in `Logs/playtest.txt`
re-measures it alongside the other states and names the synthetic-send-out set.
Those thirteen still *enter* rather than appear: `CreatureVisual.EnterField`
runs a code-driven arrival when the pack has no clip (see below).

Measured on the Ranger (23 bones), **59 of the library's 63 clips bind**, e.g.
Idle_A 61°, Walking_A 128°, Running_A 121°, Hit_A 66°, Death_A 130° of bone
rotation within the clip. The player uses that same Ranger rig, so it animates
too.

The skeleton characters were replaced with **KayKit Skeletons 1.1** — same
`Skeleton_*.fbx` names and unchanged texture, imported against the existing
`.meta` files so GUIDs, and therefore `ModelLoader.NpcModel`, stayed valid. The
1.0 models were 22 MB each because each embedded the pre-2025 animation set
(a 41-bone `Rig/root/control-*` rig, whose bundled clips import Generic and so
cannot be played by the legacy component at all); 1.1 is ~450 KB with
`Rig_Medium`. The folder went from 88 MB to 4.2 MB, and the `Skeletons_*` clips
now bind: 8.9° of bone rotation over 30 frames of idle on `Skeleton_Warrior`,
against 2.9° for the adventurers' `Idle_A`.

`EditorPlaytest` asserts that all 12 NPC models resolve a legacy idle clip and
that both rig families (`maren`, `korrin`) actually rotate their skeletons and
stay human-sized.

**Pitfall — the old 2021 pack does not work.** The *"KayKit: Character
Animations"* v1.2 files on OpenGameArt/GitHub do not drive these characters,
whatever "compatible with all previous and future KayKit character assets"
suggests. Measured: those clips animate a 4-node prototype rig (`KayKit Animated
Character` → `Body` → `armLeft`/`armRight`/`Head` — 7 curve paths), while the
Adventurers are skinned to a 23-bone `Rig_Medium`
(`root/hips/spine/chest/upperarm.l/…/toes.r`). They import as Legacy and register
idle/attack/hit/death states, but their paths bind to nothing, so the skeleton
never moves (bone delta 0.00° over 30 frames) — and Humanoid retargeting cannot
rescue them either, because that old rig has no hips, spine or legs to build an
avatar from. Only the per-rig `Rig_Medium` / `Rig_Large` / `Special` sets share
the bone names the characters actually use.

### The movement gears are tuned to the clip stride

The player's two gears (`PlayerController.walkSpeed` / `runSpeed`) are set from a
measurement of the clip they play, not from taste — the visible failure mode of a
too-fast gear is **foot sliding**: the body covers more ground than the clip's
stride, so the planted foot skates.

`EditorPlaytest.IdealSpeed` measures it from the clip itself. These cycles are in
place (the body does not travel inside them), so while a foot is planted it must
travel backwards under the body at exactly the body's forward speed — that is the
gear. It samples the rig 48x through the cycle, keeps the part where the foot sits
in the bottom quarter of its lift, and averages the ground it covers per frame
there. Measured on `Ranger`:

| Clip | Length | Planted speed | Gear |
|---|---|---|---|
| `Walking_A` (walk) | 1.067 s | 0.81 u/s | 0.78 |
| `Running_A` (run) | 0.800 s | 2.85 u/s | 2.85 |

The obvious shortcut — `2 x stride / clip length`, since a cycle is two steps —
agrees on the walk (0.75 u/s) but reads **35% low on the run**, because a run's
flight phase means the foot is not planted at the extremes of its swing. Both
gears are slow in absolute terms because the character is only ~1.45 u tall and
the clips are a settled walk and a jog, not a sprint. The playtest fails if a
replacement clip drifts more than 15% from the gear, so the two stay in step.

### Who drives each skeleton

One owner per body, so two systems never write to the same `Animation`
component:

| Body | Driver | States it plays |
|---|---|---|
| Player | `PlayerLocomotion` | idle/walk/run from the measured speed, plus the event poses |
| Overworld wild | `MeleeTarget` → `CreatureVisual.PlayState` | idle standing, walk roaming, attack on the lunge, hit when struck, death on a KO |
| Battle creature | `BattlePanel` → `CreatureVisual.ReplayState` | one beat per action the engine recorded, in order: swing, then the target's flinch/death, released after the beat |
| NPC | `ModelLoader.SetupAnimation` | idle — they never move |

A battle turn is choreographed from `BattleEngine.Beats`: the engine appends one
entry per executed action as it resolves the turn, and `BattlePanel.PlayTurn`
replays them in order (the first at `BeatSeconds`, any follow-up at
`FollowUpSeconds`). That list is the only way to show the exchange faithfully —
`PlayerTurn` resolves both sides inside one call and picks who moves first by
speed and move priority, so the panel cannot reconstruct the order from the HP
bars, and it cannot tell "the enemy swung and missed" from "the enemy never
acted". A beat with `damage == 0` is a swing that still plays (a whiff is a
swing) whose target simply has nothing to react to.

A creature entering the field plays that `spawn` pose — `BattlePanel.PlaySendOut`
→ `CreatureVisual.EnterField` — both when the battle opens and when a replacement
takes over, and the hold is the clip's own length (1.3–3.6 s) rather than a fixed
beat, because a fixed hold leaves the long ones frozen mid-landing (see
`CreatureVisual.ClipLength`).

`EnterField` is the one entry point for *every* creature. When the pack has a
`spawn` clip it plays it and returns the clip's length; when it has none it runs a
synthetic arrival instead — a short leap out of the ground along a stretched arc,
landing on the creature's spot with a squash — and returns
`SyntheticArrivalSeconds` (0.72 s). Both paths answer with the same thing, seconds
to hold, so the panel does not have to know which kind of creature it is bringing
on. The synthetic arrival motions the visual's *root* transform, not the rig, so it
layers over the idle pose already playing and works for the procedural fallback
bodies too, which have no clips at all. This is what makes the thirteen species
above enter rather than blink into existence.

Once the beats are done, `BattlePanel.RefreshStage` rebaselines the bodies against
the engine's active creatures: a SWITCH sets that index outright, and a faint
advances it (`BattleEngine.HandleFaint`), so the fight otherwise keeps animating
the creature that already left — the incoming one would attack, flinch and faint
as a body it no longer is, and a trainer's next send-out would never appear. Only
the side whose creature changed is rebuilt (the other keeps its pose, facing and
platform), and the refresh runs *after* the beats so a creature that fainted holds
its death pose through the blow that dropped it.

`PlayerLocomotion.PlayOnce` is how an *event pose* gets to own the body: the
swing and the flinch are events, not speeds, and a `Play` raised from
`PlayerController` would be overwritten by the next locomotion frame. The
controller asks for the pose, the locomotion machine stands down for the window,
then re-picks idle/walk/run from the speed it kept measuring all along. The clip
is time-scaled to fill that window — `attack` runs at
`clip.length / weapon cooldown`, so the arm finishes exactly as the weapon
becomes ready again (1.19x for the 1.07 s chop on a 0.90 s club, 1.65x on a
0.65 s blade). Before this, the prop swung while the body stood in its idle
pose, which is the mismatch the wiring exists to remove.

`CreatureVisual.PlayState` is idempotent on purpose: the roaming wilds ask for a
pose every frame, and `Animation.Play` restarts a clip from zero, so without the
guard a creature that is walking stands frozen on frame one. `ReplayState` is the
other half of that contract — it always rewinds, and it is what a *battle* beat
uses, because a second swing is a second swing and not a repeat of the same ask.
Loop states (idle/walk/run/swim) repeat; the event states play once and hold. The
playtest drives a creature visual directly, asserting both that a repeat ask
keeps the cycle clock advancing and that `ReplayState` rewinds a clip mid-beat.

Every imported model is put on `AnimationCullingType.AlwaysAnimate` by
`ModelLoader.SetupAnimation`. The importer's default, `BasedOnRenderers`, stops
evaluating a rig whose renderers are not being drawn, which is a wider condition
than "off-camera": a session with no real viewport renders nothing at all, and
then every cycle in the scene freezes (measured: bone delta 0.00° over 30 frames,
`weight=0.0`, `time` stuck at 0, with `viewport=0x0`). A frozen rig is not merely
an animation that did not play — it is a creature that pops back mid-stride. It is
the same trade `KeepSkinnedMeshesVisible` makes for skinning, for the same reason:
the player looks one way at a time, so the wilds behind them, the NPC around the
corner and both battle creatures spend most of their lives off-camera.

How much of that is real is the pack's business, and the runtime contract is
"keep the pose you had": over the 44 mapped species, 31 carry a walk clip, 34 an
attack, 25 a hit and 33 a death (the state coverage line in `Logs/playtest.txt`
re-measures it). The species you fight in the overworld are the ones that carry
the cycles, so the roam/lunge poses have footage to drive.

