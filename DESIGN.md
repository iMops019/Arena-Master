# Arena Master: Game Design

A 3D fantasy survivor-like (Vampire Survivors' loop in a Megabonk-style third-person 3D world). Single player. Built on C-Engine (`engine/`).

"Arena Master" is a working title.

Items marked **(draft)** are proposals the user hasn't confirmed yet. Items marked **(open)** are undecided. Update this file whenever a decision is made.

## Ground rules

- **Nothing carries over from `ConnEngine-Game`** (the older game on this engine): no code, assets, designs or content. Arena Master starts clean.
- **One class at a time.** Build the first class (Ranger) and its passive tree, and iterate until it feels right, before starting a second class.
- **Classes don't share anything.** Each class has its own attacks, level-up pool and passive tree. No shared abilities between classes.

## The core loop

0. At **camp**, check the item chest, spend passive tree points, and choose a loadout of up to 5 items at the departure gate.
1. Start a 30-minute run as a class.
2. Move and aim. Attacks fire automatically in the direction the camera faces (Megabonk-style), so positioning and aim matter but you never click to attack.
3. Kill monsters, which drop XP. Collect it to level up.
4. On level up the game pauses and offers **3 choices**: new abilities, upgrades to owned abilities, or stat boosts. Choices stack and can come up again.
5. Monsters and chests drop **items** that give passive bonuses and multipliers for the rest of the run.
6. Survive the escalating waves, elites and bosses to the 30-minute mark.
7. The run ends in victory, death, or "Return to Camp" from the pause menu. Items found are already in the stash, and the passive tree has banked its experience. Back at camp, spend it.

## Camera and controls (draft)

- Third-person follow camera behind the player. The mouse orbits the camera and sets the aim direction.
- WASD to move (7 m/s), Space to jump, Shift to dash (a 0.18 s burst with a 1.2 s cooldown).
- Esc pauses. The level-up screen is its own pause.

## Enemies and pacing (built, first pass)

**What's built** (numbers in `Combat/EnemyKind.cs` and `Combat/RunDirector.cs`):
- **The director:** fodder grows from 16 to a swarm of 300 over the 30 minutes (about 75 at 10:00 and 170 at 20:00), and spawns faster to keep up (up to 6 a frame). Health scales +12% per minute, damage +5%/min, speed +1%/min.
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
- **Passive trees:** a class has several trees; the player picks one to be active. The active tree earns the run's experience (its base amount, before item bonuses) and levels during runs, but points are only spent at camp. Respec is free (for now). Cap: tree level 50, one point per level. Levelling is slow on purpose: level n to n+1 takes 200 + 60 x n^1.6 experience.
- **First tree: Sharpshooter** (`Ranger/SharpshooterTree.cs`, built): 34 nodes in 7 tiers opening at tree levels 1, 3, 6, 10, 15, 21, 28, in three lanes (Precision, Volley, Trickshot). Two starting nodes: Steady Hands and Honed Draw (+10% damage and attack speed per rank, x5). Nine majors (one rank each). Chain Projectiles is a tier 2 major (user's request). A node needs its tier's level and a ranked parent.
  - Every node is playable. How the majors work in the game:
    - *Chain Projectiles:* an arrow that would stop in an enemy jumps to the nearest other one within 10 m (more with Seeker Fletching), once per chain.
    - *Twin Shot:* +1 arrow per shot, all arrows x0.85 damage.
    - *Deadeye:* crits x3 instead of x2, -5% crit chance.
    - *Rain of Arrows:* every 6 s, 12 arrows (+4 per Arrowstorm rank) fall over a 4 m patch on the ground under the crosshair, arriving over 0.6 s, at full damage each. With the crosshair on the sky, it lands 15 m ahead instead.
    - *Sniper's Focus:* stand still (no walking, dashing or knock-back) for 1 s and the next shot's middle arrow does x2.5 damage and pierces everything. "FOCUSED" shows on the HUD when it's ready. Firing spends it.
    - *Fork:* an arrow's first hit splits off two halves 20 degrees apart, carrying what the arrow had left. Halves never split again.
    - *One Shot, One Kill:* a hit on an enemy still at full health is always a crit.
    - *Endless Quiver:* every 10th shot also fires a flat ring of 16 arrows around the Ranger.
    - *Storm of Splinters:* an arrow that kills bursts into 4 small flat splinters at 50% of its damage (12 m range). Splinters never burst again.
  - The mockup: https://claude.ai/artifact/FT8PtJd252AjLdRuyinfvh

## Items

- Dropped by monsters (elites and bosses more likely) and found in chests.
- Give passive bonuses and multipliers. They stack.
- **Items are kept between runs** (changed by the user after step 5). Everything found goes into the stash at camp. Before a run, the player picks up to **5 different items** to bring, and each comes with every copy owned. The limit of 5 is a starting point to tune while playing.
- Rarity tiers: common, rare, epic, legendary.

**What's built** (`src/ArenaMaster.Game/Items/`):
- **Items are shared loot: any class can carry any item.** They speak in general terms (damage, attack speed, max health, ...) and each class's stats decide what those mean for it. This is how "classes share nothing" was read: class abilities, upgrade pools and passive trees are per class; the loot is the world's.
- **Bonuses vs multipliers:** commons and rares give bonuses that add to each other and to the class's upgrades. Epics and legendaries give multipliers that multiply the total.
- **Sources:**
  - A chest turns up 18-45 m from the player every 60 s (first at 0:40, at most 3 waiting), marked by a gold beam. Odds: 60% common, 28% rare, 10% epic, 2% legendary.
  - An elite drops a chest that is rare or better.
  - A boss drops a chest that is epic or better.
  - Fodder has a 1-in-200 chance to drop an item orb.
  - Walk into a chest or orb to take it. It goes into the chest at once (kept even if the run is lost) but **does nothing in the run it was found in**, not even as an extra copy of an item already brought. The run's bonuses are the loadout's, fixed when it sets out. To use a find, choose it for a later run.
- **The 16 items:**
  - *Common:* Whetstone (+8% damage), Feather Charm (+8% attack speed), Worn Boots (+6% move speed), Troll Blood (+0.4 HP/s), Leather Brigandine (6% less damage taken), Lodestone (+20% pickup range), Old Tome (+8% XP).
  - *Rare:* Hawk Feather (+6% crit), Troll Heart (+25 max HP), Vampire Fang (heal 1 per kill), Serrated Edge (+30% crit damage).
  - *Epic:* Rune of Might (x1.2 damage), Swiftwind Sigil (x1.15 attack speed), Ironbark Totem (x0.85 damage taken, +20 max HP).
  - *Legendary:* Dragon Heart (+60 max HP, +1.5 HP/s), Hunter's Moon (x1.35 damage, +10% crit).

## Camp (built)

- A clearing in a corner of the map (`Camp/Camp.cs`), well away from the run area in the middle. A fire, a tent, and three stations; walk up and press E:
  - **Stash chest** -> Item Chest: every item, how many owned, and which are still undiscovered.
  - **Archery target** -> the passive tree (the in-game version of the mockup).
  - **Departure gate** -> loadout (pick up to 5 items), then Begin run.
- Progress is saved to `%AppData%/ArenaMaster/profile.json` (stash, loadout, trees). It saves on every change at camp, every 20 s in a run, when the tree levels, and at the end of a run.

## Meta progression (built, first pass)

All in `Progression/MetaProgress.cs`; all numbers are a starting point to tune.
- **Silver**, earned at the end of every run, win or lose: 1 per 10 kills, 5 per elite, 60 per boss, 3 per minute survived, +250 for a win. Shown at camp and in the run summary.
- **The Quartermaster** (camp stall) sells permanent upgrades with silver:
  - *Bigger Pack:* +1 loadout slot, 3 ranks (5 -> 8 items).
  - *Second Thoughts:* +1 reroll per run on the level-up screen, 5 ranks. A reroll swaps all three cards (R).
  - *Clear Mind:* +1 banish per run, 3 ranks. A banish strikes one card's upgrade from the pool for the rest of the run and replaces the card.
  - *Lucky Charm:* 5 ranks. Each makes rares 15%, epics 30% and legendaries 50% likelier (relative to commons) from chests and drops.
- **The Bounty Board** (camp) has 10 one-time challenges, checked at the end of every run. Each pays silver once, and five unlock an item into the drop pool. **The epics and legendaries start locked** (items already owned stay owned):
  - Brute Force (kill a Brute) -> Ironbark Totem
  - Holding On (survive 10 min) -> Swiftwind Sigil
  - Regicide (kill the Hollow King) -> Hunter's Moon
  - The Long Night (survive 20 min) -> Dragon Heart
  - Massacre (1,000 kills in a run) -> Rune of Might
  - Silver only: First Hunt, Collector, Deep Roots, A Thousand Cuts (lifetime), Champion (win).
  - While a rarity's items are all locked, a roll of that rarity falls back to the next one down.
- Later: unlocking a second class, more trees, cosmetic or camp upgrades.

## Build order

Each step should be playable before the next one starts. **[engine]** means the work goes in C-Engine (commit and push there, then `./update-engine.ps1` here). **[game]** means the work goes in this repo.

1. *(Done; the user is happy with the feel.)* **Third-person Ranger movement:** follow camera, WASD, jump, dash, on a test map with a placeholder character. [engine: general follow camera] [game: Ranger controller]
2. *(Done; the user is happy with the feel.)* **Shooting and killing:** an auto-firing bow, arrow projectiles, one basic chaser enemy, hit detection, damage, death. [engine: general projectile/hitbox/damage helpers] [game: the bow, the enemy]
3. *(Done; the user is happy with the feel.)* **Levelling up:** XP drops and pickup, the level-up pause with 3 choices, the first handful of stacking upgrades, and a HUD (HP, XP bar, timer, level). [game]
4. *(Done; the user is happy with the feel.)* **A full run:** a 30-minute spawn director, one elite with telegraphed attacks, one boss, win/lose screens. [game] (The engine steering turned out not to be needed yet: the game's own separation handles about 80 enemies. Revisit with the swarm renderer in step 7.)
5. *(Done; reworked in step 6 so items persist.)* **Items:** drops, chests, bonuses and multipliers. [game]
6. *(Done.)* **Camp, save file, persistent items and loadouts, and the Sharpshooter passive tree.** [game] [engine: TeleportPlayer, pause-menu buttons, MapsMenu switch]
7. *(Done.)* **Swarms:** a batched crowd renderer for hundreds of enemies. [engine: `SetCrowd`, instanced props] [game: enemies, arrows and gems drawn as crowds; an enemy grid for spacing and hits; the director ramps to 300 fodder; gems merge past 400; damage numbers capped at 60]. The enemies are rigid stand-ins. Animated crowds (baked animation) wait for rigged enemy models.
8. *(Built, first pass; waiting for the user to try it.)* **Meta progression:** silver, the Quartermaster, the Bounty Board, locked items. (The save file came in step 6.) [game]
9. Iterate on the Ranger until it feels right, then design class #2.

## Open questions

- One big arena or a large Megabonk-style map? (Leaning toward the hybrid described above.)
- Should item experience bonuses (Old Tome) also speed up the passive tree? (Left for later; currently they don't.)
- Items stack for good across runs. With no cap on copies, a 5-item loadout will keep getting stronger; watch the balance while playing.
- Respec is free for now. Keep it free, or give it a cost later?
