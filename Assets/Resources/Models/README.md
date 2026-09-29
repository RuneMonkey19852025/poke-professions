# Model Packs (runtime-loaded from Resources/Models)

Third-party open-source packs used to replace procedural creature/NPC visuals.

| Folder | Pack | Author | License | Contents |
|---|---|---|---|---|
| `CuteMonsters/` | Cute Animated Monsters (Aug 2020) | Quaternius | CC0 | 21 textured animated monster FBX |
| `Quaternius/` | Monster/Fish/Farm Animals/Easy Enemies/Dinosaurs (Animated) | Quaternius | CC0 | 30 rigged animated FBX + textures |
| `MantisHatchery/` | Mantis Hatchery Battle Creatures | MantisDA | **CC-BY-SA 4.0** | 10 high-poly creatures, ~6-15 anims each, 1024² diffuse+normal |
| `KayKitAdventurers/` | KayKit Adventurers 2.0 (Free) | Kay Lousberg | CC0 | 6 textured rigged humans + animation library refs |

## Attribution (required for CC-BY-SA)

Ship a credits line if Mantis models are used:
"Creature models: Mantis Hatchery Battle Creatures Pack by MantisDA (CC-BY-SA 4.0)"

## Animation clip naming (as imported by Unity)

- Quaternius/CuteMonsters: `<Armature>|Idle`, `|Walk`, `|Attack`, `|Death`, `|HitRecieve`...
- Mantis: `idle`, `run`, `walk`, `bite`, `alerted_*`, `stagger_*`, `die_*` (one FBX per clip)

CreatureVisual maps these to the game's anim states (idle/walk/attack/hit/death).
