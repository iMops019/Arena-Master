# Arena Master: Game Design

A 3D fantasy survivor-like (Vampire Survivors' loop in a Megabonk-style third-person 3D world). Single player. Built on C-Engine (`engine/`).

"Arena Master" is a working title.

Items marked **(draft)** are proposals the user hasn't confirmed yet. Items marked **(open)** are undecided. Update this file whenever a decision is made.

## Ground rules

- **Nothing carries over from `ConnEngine-Game`** (the older game on this engine): no code, assets, designs or content. Arena Master starts clean.
- **One class at a time.** Build the first class (Ranger) and its passive tree, and iterate until it feels right, before starting a second class.
- **Classes don't share anything.** Each class has its own attacks, level-up pool and passive tree. No shared abilities between classes.

## The core loop

1. Start a 30-minute run as a class.
2. Move and aim. Attacks fire automatically in the direction the camera faces (Megabonk-style), so positioning and aim matter but you never click to attack.
3. Kill monsters, which drop XP. Collect it to level up.
4. On level up the game pauses and offers **3 choices**: new abilities, upgrades to owned abilities, or stat boosts. Choices stack and can come up again.
5. Monsters and chests drop **items** that give passive bonuses and multipliers for the rest of the run.
6. Survive the escalating waves, elites and bosses to the 30-minute mark.
7. After the run, spend progress on permanent unlocks (see **Meta progression**).

## Camera and controls (draft)

- Third-person follow camera behind the player. The mouse orbits the camera and sets the aim direction.
- WASD to move (7 m/s), Space to jump, Shift to dash (a 0.18 s burst with a 1.2 s cooldown).
- Esc pauses. The level-up screen is its own pause.

## Enemies and pacing (draft, based on the recommended hybrid)

The map-style decision is **(open)**: one big arena or a large Megabonk-style map. The recommendation is a hybrid:

- **Fodder swarms** that keep growing over the run, so the player's power growth is visible. This needs the engine's crowd renderer (see the build order).
- **Elite packs** on a timer: few enemies, dangerous, with telegraphed attacks (lunges, jump slams, stuns, charges). Clear wind-ups so they're fair. This is where dodging and skill matter.
- **Bosses** at set times, with a final one near 30:00.
- **A bounded map**, mid-to-large, with edges (Megabonk-style levels, not an open world).

## First class: Ranger

- **Basic attack:** a bow that auto-fires arrows where the camera aims. The arrows converge on whatever the crosshair is on. Current numbers: 1 shot per 0.5 s, 12 damage, 50 m/s, 60 m range, straight flight.
- **Level-up pool (draft):**
  - Stats: damage, attack speed, projectile count, pierce, crit chance/damage, projectile speed, move speed, pickup radius, max HP, regen.
  - Abilities: Volley (fan of arrows), Piercing Shot, Rain of Arrows (area), Traps, Poison or Fire Arrows (damage over time), Hawk companion.
- **Passive tree (draft):** three branches that each push a different build:
  - **Marksman:** crits, single-target and boss damage.
  - **Volley:** multi-projectile, area, clearing swarms.
  - **Trapper/Beast:** traps, damage over time, companion.
- **How passive points are earned (open):** in-run on level up, or permanently between runs, or both.

## Items

- Dropped by monsters (elites and bosses more likely) and found in chests.
- Give passive bonuses and multipliers for the rest of the run. They stack.
- Rarity tiers (draft): common, rare, epic, legendary.

## Meta progression (like Megabonk)

- Between runs the player unlocks things permanently (new items added to the drop pool, new abilities, later new classes) by completing challenges and spending a currency earned in runs.
- The details are **(open)** until the Ranger loop feels good.

## Build order

Each step should be playable before the next one starts. **[engine]** means the work goes in C-Engine (commit and push there, then `./update-engine.ps1` here). **[game]** means the work goes in this repo.

1. *(Done; the user is happy with the feel.)* **Third-person Ranger movement:** follow camera, WASD, jump, dash, on a test map with a placeholder character. [engine: general follow camera] [game: Ranger controller]
2. *(Built; waiting for the user to try it.)* **Shooting and killing:** an auto-firing bow, arrow projectiles, one basic chaser enemy, hit detection, damage, death. [engine: general projectile/hitbox/damage helpers] [game: the bow, the enemy]
3. **Levelling up:** XP drops and pickup, the level-up pause with 3 choices, the first handful of stacking upgrades, and a HUD (HP, XP bar, timer, level). [game]
4. **A full run:** a 30-minute spawn director, one elite with telegraphed attacks, one boss, win/lose screens. [game] [engine: basic steering/avoidance for many enemies]
5. **Items:** drops, chests, bonuses and multipliers. [game]
6. **Ranger passive tree:** the tree UI and nodes. [game]
7. **Swarms:** a batched crowd renderer for hundreds of animated enemies. [engine]
8. **Meta progression:** unlocks, currency, a save file. [game]
9. Iterate on the Ranger until it feels right, then design class #2.

## Open questions

- One big arena or a large Megabonk-style map? (Leaning toward the hybrid described above.)
- Are passive-tree points earned in-run, permanently between runs, or both?
- Megabonk's items are found during a run and meta progression unlocks them. Is that the model, or should some items persist between runs?
