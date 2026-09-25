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

## Enemies and pacing (built, first pass)

**What's built** (numbers in `Combat/EnemyKind.cs` and `Combat/RunDirector.cs`):
- **The director:** fodder grows from 14 to about 70 over the 30 minutes and spawns faster. Health scales +12% per minute, damage +5%/min, speed +1%/min.
- **Ghoul (fodder):** 30 HP, 3.6 m/s, claws on contact for 8.
- **Ghoul Brute (elite):** first at 3:00, then every 2.5 min, 1 per wave (2 after 10:00, 3 after 20:00). 260 HP, 12 XP. Two telegraphed attacks:
  - *Lunge:* crouches for 0.75 s over a red lane, then charges 7.2 m down it. 22 damage plus knock-back.
  - *Leap slam:* a red circle (3.5 m) fills up over 1.45 s, then it lands in it. 26 damage, knock-back, 0.9 s stun.
- **The Hollow King (boss):** at 10:00, 20:00, and a final one (x1.5 health) at 27:00. 3200 HP before scaling, 80 XP. Health bar under the clock. Three attacks:
  - *Leap slam:* 6 m circle, 35 damage, 1 s stun.
  - *Shockwave:* a ring spreads 18 m along the ground; jump over it or take 25.
  - *Summon:* 8 ghouls around itself.
- **Win:** survive to 30:00. **Lose:** death. Either way, a summary shows and a new run starts.
- **Dev keys (remove before release):** F5 skips a minute, F6 spawns a brute, F7 spawns the king.

**The design intent this came from:**

The map-style decision is **(open)**: one big arena or a large Megabonk-style map. The recommendation is a hybrid:

- **Fodder swarms** that keep growing over the run, so the player's power growth is visible. This needs the engine's crowd renderer (see the build order).
- **Elite packs** on a timer: few enemies, dangerous, with telegraphed attacks (lunges, jump slams, stuns, charges). Clear wind-ups so they're fair. This is where dodging and skill matter.
- **Bosses** at set times, with a final one near 30:00.
- **A bounded map**, mid-to-large, with edges (Megabonk-style levels, not an open world).

## First class: Ranger

- **Basic attack:** a bow that auto-fires arrows where the camera aims. The arrows converge on whatever the crosshair is on. Current numbers: 1 shot per 0.5 s, 12 damage, 50 m/s, 60 m range, straight flight.
- **Levelling:** each ghoul drops a 1 XP gem. Level n to n+1 takes 5n XP. Gems within the pickup radius (3 m base) fly to the player. Each level pauses the game and offers 3 random upgrades from the pool below; maxed ones drop out, and once all are maxed the offer is a heal (Second Wind, 30 HP). A death ends the run and resets level, upgrades and clock.
- **Level-up pool (built, first pass):** Sharpened Tips (+20% damage, x5), Quick Draw (+15% attack speed, x5), Split Shot (+1 arrow fanned 7 degrees apart, x4), Piercing Arrows (+1 pierce, x3), Deadeye (+8% crit, crits x2, base 5%, x5), Fletching (+20% arrow speed and +15% range, x3), Fleet Foot (+8% move speed, x5), Vitality (+20 max HP and heal, x5), Scavenger (+35% pickup range, x4).
- **Ideas for later levels of the pool (draft):** Rain of Arrows (area), Traps, Poison or Fire Arrows (damage over time), Hawk companion, health regen.
- **Passive tree (draft):** three branches that each push a different build:
  - **Marksman:** crits, single-target and boss damage.
  - **Volley:** multi-projectile, area, clearing swarms.
  - **Trapper/Beast:** traps, damage over time, companion.
- **How passive points are earned (open):** in-run on level up, or permanently between runs, or both.

## Items

- Dropped by monsters (elites and bosses more likely) and found in chests.
- Give passive bonuses and multipliers for the rest of the run. They stack.
- Rarity tiers: common, rare, epic, legendary.

**What's built** (`src/ArenaMaster.Game/Items/`):
- **Items last one run and are shared loot: any class can carry any item.** They speak in general terms (damage, attack speed, max health, ...) and each class's stats decide what those mean for it. This is how "classes share nothing" was read: class abilities, upgrade pools and passive trees are per class; the loot is the world's.
- **Bonuses vs multipliers:** commons and rares give bonuses that add to each other and to the class's upgrades. Epics and legendaries give multipliers that multiply the total.
- **Sources:**
  - A chest turns up 18-45 m from the player every 60 s (first at 0:40, at most 3 waiting), marked by a gold beam. Odds: 60% common, 28% rare, 10% epic, 2% legendary.
  - An elite drops a chest that is rare or better.
  - A boss drops a chest that is epic or better.
  - Fodder has a 1-in-200 chance to drop an item orb.
  - Walk into a chest or orb to take it.
- **The 16 items:**
  - *Common:* Whetstone (+8% damage), Feather Charm (+8% attack speed), Worn Boots (+6% move speed), Troll Blood (+0.4 HP/s), Leather Brigandine (6% less damage taken), Lodestone (+20% pickup range), Old Tome (+8% XP).
  - *Rare:* Hawk Feather (+6% crit), Troll Heart (+25 max HP), Vampire Fang (heal 1 per kill), Serrated Edge (+30% crit damage).
  - *Epic:* Rune of Might (x1.2 damage), Swiftwind Sigil (x1.15 attack speed), Ironbark Totem (x0.85 damage taken, +20 max HP).
  - *Legendary:* Dragon Heart (+60 max HP, +1.5 HP/s), Hunter's Moon (x1.35 damage, +10% crit).

## Meta progression (like Megabonk)

- Between runs the player unlocks things permanently (new items added to the drop pool, new abilities, later new classes) by completing challenges and spending a currency earned in runs.
- The details are **(open)** until the Ranger loop feels good.

## Build order

Each step should be playable before the next one starts. **[engine]** means the work goes in C-Engine (commit and push there, then `./update-engine.ps1` here). **[game]** means the work goes in this repo.

1. *(Done; the user is happy with the feel.)* **Third-person Ranger movement:** follow camera, WASD, jump, dash, on a test map with a placeholder character. [engine: general follow camera] [game: Ranger controller]
2. *(Done; the user is happy with the feel.)* **Shooting and killing:** an auto-firing bow, arrow projectiles, one basic chaser enemy, hit detection, damage, death. [engine: general projectile/hitbox/damage helpers] [game: the bow, the enemy]
3. *(Done; the user is happy with the feel.)* **Levelling up:** XP drops and pickup, the level-up pause with 3 choices, the first handful of stacking upgrades, and a HUD (HP, XP bar, timer, level). [game]
4. *(Done; the user is happy with the feel.)* **A full run:** a 30-minute spawn director, one elite with telegraphed attacks, one boss, win/lose screens. [game] (The engine steering turned out not to be needed yet: the game's own separation handles about 80 enemies. Revisit with the swarm renderer in step 7.)
5. *(Built; waiting for the user to try it.)* **Items:** drops, chests, bonuses and multipliers. [game]
6. **Ranger passive tree:** the tree UI and nodes. [game]
7. **Swarms:** a batched crowd renderer for hundreds of animated enemies. [engine]
8. **Meta progression:** unlocks, currency, a save file. [game]
9. Iterate on the Ranger until it feels right, then design class #2.

## Open questions

- One big arena or a large Megabonk-style map? (Leaning toward the hybrid described above.)
- Are passive-tree points earned in-run, permanently between runs, or both?
- Megabonk's items are found during a run and meta progression unlocks them. Is that the model, or should some items persist between runs?
