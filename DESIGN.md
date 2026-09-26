# Arena Master: Game Design

A 3D fantasy survivor-like (Vampire Survivors' loop in a Megabonk-style third-person 3D world). Single player. Built on C-Engine (`engine/`).

"Arena Master" is a working title.

Items marked **(draft)** are proposals the user hasn't confirmed yet. Items marked **(open)** are undecided. Update this file whenever a decision is made.

## Ground rules

- **Nothing carries over from `ConnEngine-Game`** (the older game on this engine): no code, assets, designs or content. Arena Master starts clean.
- **One class at a time.** Build the first class (Ranger) and its passive tree, and iterate until it feels right, before starting a second class. (On 2026-09-25 the user chose to start the second class, the Paladin, before the Ranger tuning pass.)
- **Classes don't share anything.** Each class has its own attacks, level-up pool and passive tree. No shared abilities between classes. In code, each class lives in its own folder (`Ranger/`, `Paladin/`) and the game runs it through one seam, `Classes/IHeroClass`; no class shares code with another (each has its own stats, controller, pool and tree). Shared are only the world's things: enemies, items, the camp, the meta progression, and the general combat rules in `Combat/`: a blow can be blocked (any class can have a block chance; only the Paladin's isn't 0), an enemy can be chilled (slowed) or frozen, and the player's health can carry a barrier that takes damage first.

## The core loop

0. At **camp**, choose a class at the weapon rack, check the item chest, spend passive tree points, read the bounty board, buy upgrades from the quartermaster with silver, and choose a loadout of up to 5 items (more with Bigger Pack) at the departure gate.
1. Start a 30-minute run as a class.
2. Move and aim. Attacks fire automatically in the direction the camera faces (Megabonk-style), so positioning and aim matter but you never click to attack.
3. Kill monsters, which drop XP. Collect it to level up.
4. On level up the game pauses and offers **3 choices**: new abilities, upgrades to owned abilities, or stat boosts. Choices stack and can come up again.
5. Monsters and chests drop **items**. They go to the chest at camp and give their bonuses on a later run that brings them, not the one they were found in.
6. Survive the escalating waves, elites and bosses to the 30-minute mark.
7. The run ends in victory, death, or "Return to Camp" from the pause menu. A summary shows what it earned: silver, bounties completed, tree experience, items found. Back at camp, spend it.

## Camera and controls (built)

- Third-person follow camera behind the player. The mouse orbits the camera and sets the aim direction.
- WASD to move (the Ranger and the Mage 7 m/s, the Paladin 6.4), Space to jump, Shift to dash (the Ranger: a 0.18 s burst with a 1.2 s cooldown; the Paladin's shield rush: 0.2 s, a little slower, 1.6 s cooldown; the Mage's blink: 0.12 s, very fast, about 4.5 m, 2.2 s cooldown).
- Esc pauses (Return to Camp is in the pause menu). The level-up screen is its own pause. E uses a camp station and closes camp screens.
- The title screen has Play (continue the save) and **New Game** (start over, behind a confirmation; the old save is copied to `profile.backup-<date-time>.json`, never deleted).

## Enemies and pacing (built, first pass)

**What's built** (numbers in `Combat/EnemyKind.cs` and `Combat/RunDirector.cs`):
- **The director:** fodder grows from 16 to a swarm of 300 over the 30 minutes (about 75 at 10:00 and 170 at 20:00), and spawns faster to keep up (up to 6 a frame). Health scales +12% per minute, damage +5%/min, speed +1%/min.
- **Ghoul (fodder):** 30 HP, 3.6 m/s, claws on contact for 8.
- **Crossbow Ghoul (ranged fodder, the user's request; numbers are a first pass):** 24 HP, 3.2 m/s, 2 XP, claws for 6 if you walk up to it. Mixed into the fodder from 1:30: 6% of spawns, growing to 15% by 16:30. It walks in until you're within 11 m and stops there. With you 3 to 14 m away it aims for 1.1 s, turning to follow you, while its crossbow lights up from faint to bright (the telegraph). Then it looses a bolt at where you stand: 12 damage (scaled like any enemy's), a small shove, 20 m/s, so a step aside after the glow peaks dodges it. 2.8 s cooldown between shots. A shield blocks a bolt like any blow (no damage, no shove); a frozen shooter holds its shot.
- **Ghoul Brute (elite):** first at 3:00, then every 2.5 min, 1 per wave (2 after 10:00, 3 after 20:00). 260 HP, 12 XP. Two telegraphed attacks:
  - *Lunge:* crouches for 0.75 s over a red lane, then charges 7.2 m down it. 22 damage plus knock-back.
  - *Leap slam:* a red circle (3.5 m) fills up over 1.45 s, then it lands in it. 26 damage, knock-back, 0.9 s stun.
- **The Hollow King (boss):** at 10:00, 20:00, and a final one (x1.5 health) at 27:00. 3200 HP before scaling, 80 XP. Health bar under the clock. Three attacks:
  - *Leap slam:* 6 m circle, 35 damage, 1 s stun.
  - *Shockwave:* a ring spreads 18 m along the ground; jump over it or take 25.
  - *Summon:* 8 ghouls around itself.
- **Win:** survive to 30:00. **Lose:** death. Either way, a summary shows and then it's back to camp.
- **Dev keys (remove before release):** in a run, F5 skips a minute, F6 spawns a brute, F7 spawns the king. At camp, F8 gives the passive tree a level.

**The design intent this came from** (the user chose the hybrid map):

- **Fodder swarms** that keep growing over the run, so the player's power growth is visible. Drawn with the engine's crowds (step 7).
- **Elite packs** on a timer: few enemies, dangerous, with telegraphed attacks (lunges, jump slams, stuns, charges). Clear wind-ups so they're fair. This is where dodging and skill matter.
- **Bosses** at set times, with a final one near 30:00.
- **A bounded map**, mid-to-large, with edges (Megabonk-style levels, not an open world). The current map is a 512 m test map; the real one is still to be made.

## First class: Ranger

- **Basic attack:** a bow that auto-fires arrows where the camera aims. The arrows converge on whatever the crosshair is on. Current numbers: 1 shot per 0.5 s, 12 damage, 50 m/s, 60 m range, straight flight.
- **Levelling:** each ghoul drops a 1 XP gem. Level n to n+1 takes 5n XP. Gems within the pickup radius (3 m base) fly to the player. Each level pauses the game and offers 3 random upgrades from the pool below; maxed ones drop out, and once all are maxed the offer is a heal (Second Wind, 30 HP). A death ends the run and resets level, upgrades and clock.
- **Level-up pool (built, first pass):** Sharpened Tips (+20% damage, x5), Quick Draw (+15% attack speed, x5), Split Shot (+1 arrow fanned 7 degrees apart, x4), Piercing Arrows (+1 pierce, x3), Deadeye (+8% crit, crits x2, base 5%, x5), Fletching (+20% arrow speed and +15% range, x3), Fleet Foot (+8% move speed, x5), Vitality (+20 max HP and heal, x5), Scavenger (+35% pickup range, x4).
- **Rerolls and banishes** on the level-up screen come from the Quartermaster (see Meta progression).
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

## Second class: Paladin (built, first pass)

The user's brief: a Paladin with a flail and a big crusader shield. The first tree is **Defiance**. Its attack is **Holy Nova**: an area explosion that damages what it hits, then leaves a holy circle on the ground that damages enemies over time and heals the player over time. Defiance is about unlocking thorns and boosting them, life regeneration, block chance, boosting blocks, Holy Nova nodes and majors, and area nodes and majors.

Everything below the brief is a first pass **(draft)**: the numbers, the level-up pool, the tree's nodes and majors, and where the nova goes off.

- **Holy Nova** (`Paladin/HolyLight.cs`): bursts on its own every 1.25 s, **centred on the Paladin** (a nova goes off around the caster; the Paladin is built to stand in the crowd, so aim matters less than where you stand). 30 damage to every enemy within 4.5 m (5% crit, x2). A stun holds it, as it holds the Ranger's bow.
- **Holy circle**: each nova leaves one on the ground where it went off: 3 m in radius, 4 s, 10 damage per second to each enemy in it (a tick every 0.5 s), and 2 health per second to the Paladin while standing in one. Circles' damage stacks where they overlap; their healing doesn't (Consecrated Ground makes it stack). At most 12 at once. Their steady burn shows as the enemies flashing, not as damage numbers.
- **The shield**: 10% base chance to **block** a blow (contact or attack). A block stops all of it: no damage, no knock-back, no stun. Block chance is capped at 60%.
- **Thorns** (unlocked by Crown of Thorns): every 0.5 s, each enemy touching the Paladin takes 6 damage (more from the nodes).
- **Base numbers**: 130 max health (the Ranger has 100), 6.4 m/s, 3 m pickup.
- **Items** read the same way as for the Ranger: damage and attack speed mean the nova's (and the circles' and thorns' damage), crit means the nova's.
- **Level-up pool** (`Paladin/PaladinUpgrades.cs`): Holy Wrath (+20% nova damage, x5), Quickened Prayer (+12% nova frequency, x5), Radiance (+10% nova and circle size, x4), Consecration (+25% circle damage and +0.5 s, x4), Shield Training (+4% block, x5), Heavy Plate (+20 max health and heal, x5), Prayer of Mending (+0.5 health per second, x5), Barbed Plating (+40% thorns, x4, only once thorns are unlocked), Pilgrim's Stride (+8% move speed, x5), Gleaner (+35% pickup range, x4).
- **Defiance tree** (`Paladin/DefianceTree.cs`): 40 nodes in 7 tiers (the same levels as the Sharpshooter: 1, 3, 6, 10, 15, 21, 28) and four lanes: **Bulwark** (block chance, what a block does, regeneration), **Retribution** (thorns), **Radiance** (the nova) and **Consecration** (area: the nova's reach and the circles). Two starting nodes: Shield Wall (+2% block, +8 max health per rank) and Zealous Light (+10% nova damage, +5% frequency per rank). Thirteen majors, a capstone per lane:
  - *Crown of Thorns* (tier 2, Retribution): unlocks thorns.
  - *Echoing Nova* (tier 3, Radiance): every nova bursts again 0.35 s later at 60% damage.
  - *Shield of Faith* (tier 4, Bulwark): every 12 s a holy shield readies and the next blow is blocked for certain ("SHIELD OF FAITH" under the crosshair while it's up).
  - *Retribution* (tier 4, Retribution): every blow that reaches the Paladin, landed or blocked, is paid back: the attacker takes 200% of it.
  - *Consecrated Ground* (tier 4, Consecration): the circles' healing stacks, one per circle stood in.
  - *Wrath of the Many* (tier 5, Radiance): +2% nova damage per enemy it hits, up to +60%.
  - *Expanding Light* (tier 5, Consecration): circles grow to twice their size over their life.
  - *Unbroken Vow* (tier 6, Bulwark): once per run, a killing blow leaves the Paladin on 1 health and heals 40%.
  - *Sanctuary* (tier 6, Consecration): in a holy circle, 20% less damage taken and +10% block.
  - Capstones (tier 7): *Holy Bastion* (every block releases a nova at 75% damage), *Crown of Briars* (thorns reach every enemy within 3 m, +50%), *Radiant Avatar* (every 8th nova is a Great Nova: twice the radius, triple the damage, a bigger circle), *Resonance* (every nova also bursts from each holy circle at 50%).
  - Minors worth knowing: Braced Stance (block chance while standing still), Shield Bash (a block damages the attacker), Mending Guard (a block heals), Iron Briars (thorns add a share of max health), Bloodthorns (thorns kills heal), Desperate Prayer (more regeneration below 40% health), Lingering Light (+1 s circles per rank).
- **How it plays, from a simulation** (no rendering, the real director, a level-up taken at random each level): a fresh Paladin with no tree points who stands its ground survives the first 10 minutes; one that keeps walking leaves its circles behind and died at about 4.5 minutes. With a modest tree (about 30 points) both survive 10 minutes easily. Defiance rewards standing your ground. Whether that's fun, or standing still is too strong, is the first thing to feel out.
- The model is a stand-in like the Ranger's (plate, a white tabard with a red cross, a great helm, the tower shield on the left arm, the flail in the right hand). Nothing is animated: the flail doesn't swing yet.

## Third class: Mage (built, first pass)

The user's brief: a Mage whose first tree is **Frost**. Its attack is **Frost Barrage**: 7 projectiles fire out from the Mage and target enemies, with a slight delay between each one. The tree has cold damage nodes, projectile nodes and +1 projectile nodes; **Frost Shield**, a major that makes a shield that takes damage for the player for a short time and comes up on its own during the game; and **Frost Blast**, a major that makes a barrage bolt explode in a small area when it hits. The rest of the nodes and majors were left to Claude. A free class, like the others: not unlocked with silver.

Everything below the brief is a first pass **(draft)**.

- **Frost Barrage** (`Mage/FrostBarrage.cs`): every 1.8 s, while an enemy is within 22 m, 3 bolts leave the staff 0.07 s apart (7 in the brief; the user cut it to 3 after the first build). Each fans out, a little upward, then curves onto its target (flying straight in for the last 3 m, so it never circles). 15 damage each (5% crit, x2), 24 m/s, 30 m of flight. A stun holds the barrage, bolts not yet away included.
  - **Targeting:** each bolt goes for the best target in range (in front of the camera first, then nearest) that the bolts already flying at it won't kill. So a volley kills what it hits and then spreads out, instead of grazing seven enemies and killing none. The first simulation showed the one-bolt-each version dying in seconds.
- **Cold:** every frost hit chills: 1.5 s, walking 20% slower (chill capped at 70%). A frozen enemy stands still, can't claw, and holds any attack it was winding up; it shows pale. Bosses can't be frozen.
- **Base numbers:** 90 max health (the most fragile class), 7 m/s, 3 m pickup.
- **Items:** damage means cold damage, attack speed means cast speed, crit means a bolt's crit.
- **Level-up pool** (`Mage/MageUpgrades.cs`): Ice Shards (+20% cold damage, x5), Quickened Casting (+12% cast speed, x5), Splinter Bolt (+1 projectile, x3), Winter Wind (+20% projectile speed, +15% range, x3), Numbing Cold (+8% chill, +0.3 s, x4), Piercing Ice (+1 pierce, x2), Glacial Ward (+30% Frost Shield, x3, only with Frost Shield), Concussive Frost (+25% Frost Blast radius, x3, only with Frost Blast), Arcane Vigor (+15 max health and heal, x5), Fleet Step (+8% move speed, x5), Attunement (+35% pickup range, x4), Frozen Precision (+6% crit chance, +15% crit damage, x4).
- **Frost tree** (`Mage/FrostTree.cs`): 34 nodes in 7 tiers (levels 1, 3, 6, 10, 15, 21, 28) and three lanes: **Winter** (cold damage, chill, freezing), **Barrage** (the bolts) and **Ward** (the Frost Shield and staying alive). Two starting nodes: Cold Hands (+10% cold damage, +3% chill per rank) and Quickened Mind (+8% cast speed, +8% projectile speed per rank). Two +1 projectile nodes: Extra Shard (tier 2) and Volley of Ice (tier 4), 2 ranks each. Ten majors:
  - *Frost Shield* (tier 2, Ward; the user's): every 10 s a shield forms on its own and takes damage for 4 s: 25 plus 20% of max health. It's a barrier on the health bar (the icy band, and "+N shield" by the HP). The first comes 2 s into a run.
  - *Frost Blast* (tier 3, Barrage; the user's): a bolt that hits explodes: 50% of its damage to every other enemy within 2 m, chilling them.
  - *Deep Freeze* (tier 4, Winter): a frost hit on a chilled enemy has a 10% chance to freeze it for 1.2 s (half for elites).
  - *Shattering Ward* (tier 4, Ward): a Frost Shield that breaks bursts for 300% of a bolt's damage within 4 m.
  - *Shatter* (tier 5, Winter): frozen enemies take 60% more.
  - *Splitting Ice* (tier 5, Barrage): a bolt that kills splits into two half-damage bolts that seek the nearest other enemies.
  - *Ice Block* (tier 6, Ward): once per run, a killing blow leaves 1 health, heals 25% and puts up a shield of half max health.
  - Capstones (tier 7): *Blizzard* (every enemy within 5 m takes a bolt's damage per second and is chilled), *Comet* (every 4th barrage ends with a comet at the toughest enemy in range: 500%, and a 4 m blast of half that), *Glacial Fortress* (the Frost Shield holds until it breaks, and forms 50% faster).
- **How it plays, from a simulation** (no rendering, the real director, a random level-up each level): a fresh Mage that keeps moving survives 10 minutes, about as well as a fresh Paladin that stands still. A fresh Mage that stands still dies in about 25 seconds. With about 30 tree points it survives 10 minutes either way. The Mage is the class that has to keep moving.
- The model is a stand-in: a long blue robe, a pointed hat, a white beard, a staff with an ice crystal. The bolts are ice crystals; the shield and the blizzard are ice shards circling the Mage.

## Items

- Dropped by monsters (elites and bosses more likely) and found in chests.
- Give passive bonuses and multipliers. They stack.
- **Items are kept between runs** (changed by the user after step 5). Everything found goes into the stash at camp. Before a run, the player picks up to **5 different items** to bring (up to 8 with the Quartermaster's Bigger Pack), and each comes with every copy owned. The limit is a starting point to tune while playing.
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
- **The 40 items.** The first 16:
  - *Common:* Whetstone (+8% damage), Feather Charm (+8% attack speed), Worn Boots (+6% move speed), Troll Blood (+0.4 HP/s), Leather Brigandine (6% less damage taken), Lodestone (+20% pickup range), Old Tome (+8% XP).
  - *Rare:* Hawk Feather (+6% crit), Troll Heart (+25 max HP), Vampire Fang (heal 1 per kill), Serrated Edge (+30% crit damage).
  - *Epic:* Rune of Might (x1.2 damage), Swiftwind Sigil (x1.15 attack speed), Ironbark Totem (x0.85 damage taken, +20 max HP).
  - *Legendary:* Dragon Heart (+60 max HP, +1.5 HP/s), Hunter's Moon (x1.35 damage, +10% crit).
- **The 24 added with the Paladin and the Mage** (the user asked for Paladin- and Mage-friendly items and general ones, Claude proposed these, the user took them all). Class-leaning items still give every class something; they just pay the most for the class they lean to:
  - *Paladin-leaning:* Iron Buckler (common, +4% block chance: any class can block with it), Thorned Bracers (common, thorns: 4 damage every 0.5 s to what touches you, any class), Pilgrim's Censer (rare, +20% area: the Paladin's nova and circles, the Mage's blasts, blizzard and ward burst, the Ranger's Rain of Arrows patch), Blessed Reliquary (rare, +1 s to lingering effects: holy circles, chill, the Frost Shield, Momentum), Bulwark Sigil (epic, x1.25 block chance, x0.9 damage taken), Martyr's Crown (legendary, every blow that reaches you is paid back at 150% and heals 1% of max health), Aegis of the Dawn (legendary, +12% block, every block bursts for 25 damage within 3 m).
  - *Mage-leaning:* Rime Charm (common, every hit chills 10% for 1 s; the Mage's own chill +10%), Prism Shard (rare, +1 projectile: an arrow or a bolt; the Paladin gets +12% nova damage instead), Warding Crystal (rare, every 15 s a 20-point ward for 5 s; a Mage with the Frost Shield gets a 25% stronger shield instead), Lens of Clarity (rare, +20% projectile speed and range, +5% crit), Heart of Winter (epic, x1.25 damage to chilled or frozen enemies), Staff of the Long Night (legendary, hits have a 5% chance to freeze a non-boss for 1 s, frozen enemies take x1.4), Splintered Crown (legendary, +2 projectiles each 15% weaker; the Paladin +24% nova damage).
  - *For everyone:* Merchant's Purse (common, +15% silver), Headsman's Axe (rare, x1.25 damage to elites and bosses), Wind-Walker Boots (rare, the Shift move recharges 25% faster, +5% move speed), Bloodstone (rare, heal 1 per 60 damage dealt), Hourglass of Chances (rare, +1 level-up reroll per run, +5% XP), Glass Pendant (epic, x1.35 damage but x1.2 damage taken), Berserker's Band (epic, up to +35% attack speed as health drops), Cursed Idol (epic, +40% XP and silver, but enemies +15% health), Phoenix Feather (legendary, once per copy per run a killing blow brings you back at 50%; a class's own last stand, Unbroken Vow or Ice Block, is used first), Crown of Plenty (legendary, x1.2 damage, XP and silver).
- **Where each effect lives:** a class's stats read what changes its own attacks (area, duration, projectiles, block, dash recharge, attack speed with Berserk). The enemy field's hit effects apply what changes every hit, whoever lands it (chill, freeze, damage to chilled, frozen and elite enemies). `Items/ItemEffects` runs what acts on its own (thorns, the ward, Bloodstone, Martyr's Crown, the Aegis burst, the Phoenix). The content applies silver, XP and rerolls.

## Camp (built)

- A clearing in a corner of the map (`Camp/Camp.cs`), well away from the run area in the middle. A fire, a tent, and six stations; walk up and press E:
  - **Weapon rack** (behind the spawn) -> choose the class: the Ranger, the Paladin or the Mage. Each keeps its own tree; the item chest, silver and upgrades are shared.
  - **Stash chest** -> Item Chest: every item, how many owned, and which are still undiscovered.
  - **Archery target** -> the chosen class's passive tree (the in-game version of the mockup).
  - **Bounty board** -> the bounties, done and to do.
  - **Quartermaster's stall** -> upgrades for silver.
  - **Departure gate** -> loadout (pick up to 5 items, more if bought), then Begin run.
- Progress is saved to `%AppData%/ArenaMaster/profile.json` (stash, loadout, trees, silver, upgrades, bounties, lifetime totals). It saves on every change at camp, every 20 s in a run, when the tree levels, and at the end of a run.

## Meta progression (built, first pass)

All in `Progression/MetaProgress.cs`; all numbers are a starting point to tune.
- **Silver**, earned at the end of every run, win or lose: 1 per 10 kills, 5 per elite, 60 per boss, 3 per minute survived, +250 for a win. Shown at camp and in the run summary.
- **The Quartermaster** (camp stall) sells permanent upgrades with silver:
  - *Bigger Pack:* +1 loadout slot, 3 ranks (5 -> 8 items).
  - *Second Thoughts:* +1 reroll per run on the level-up screen, 5 ranks. A reroll swaps all three cards (R).
  - *Clear Mind:* +1 banish per run, 3 ranks. A banish strikes one card's upgrade from the pool for the rest of the run and replaces the card.
  - *Lucky Charm:* 5 ranks. Each makes rares 15%, epics 30% and legendaries 50% likelier (relative to commons) from chests and drops.
- **The Bounty Board** (camp) has 21 one-time challenges, checked at the end of every run. Each pays silver once, and 16 unlock an item into the drop pool. **Every epic and legendary starts locked** (items already owned stay owned). The board lists the open ones first and scrolls:
  - Brute Force (kill a Brute) -> Ironbark Totem
  - Holding On (survive 10 min) -> Swiftwind Sigil
  - Regicide (kill the Hollow King) -> Hunter's Moon
  - The Long Night (survive 20 min) -> Dragon Heart
  - Massacre (1,000 kills in a run) -> Rune of Might
  - Shieldbearer (survive 10 min as the Paladin) -> Bulwark Sigil
  - Cold Snap (500 kills in a run as the Mage) -> Heart of Winter
  - Glass Cannon (reach level 25 in a run) -> Glass Pendant
  - Brute Hunter (5 Brutes in a run) -> Berserker's Band
  - Dark Bargain (set out on 15 runs) -> Cursed Idol
  - Martyr (defeat the Hollow King as the Paladin) -> Martyr's Crown
  - Dawnbringer (win as the Paladin) -> Aegis of the Dawn
  - Endless Winter (win as the Mage) -> Staff of the Long Night
  - Deadeye's Prize (win as the Ranger) -> Splintered Crown
  - Rise Again (survive 25 min) -> Phoenix Feather
  - Hoarder (own 30 different items) -> Crown of Plenty
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
8. *(Built, first pass; the user is starting a fresh save to try it.)* **Meta progression:** silver, the Quartermaster, the Bounty Board, locked items, and a New Game button on the title screen. (The save file came in step 6.) [game] [engine: title-screen buttons]
9. *(Built, first pass.)* **Class #2, the Paladin, and its Defiance tree**, with a class rack at camp to switch. [game]
10. *(Built, first pass.)* **Class #3, the Mage, and its Frost tree.** [game]
11. Play the three classes and tune them: the Ranger, the Paladin (how much standing still should pay), the Mage (how hard standing still should punish). Then more enemies, or second trees.

## Open questions

- The Paladin: should the Holy Nova stay centred on the Paladin, or go off where the crosshair aims (so aim matters, as for the Ranger)? Should the flail do anything of its own?

- Should item experience bonuses (Old Tome) also speed up the passive tree? (Left for later; currently they don't.)
- Items stack for good across runs. With no cap on copies, a loadout keeps getting stronger; watch the balance while playing.
- Meta progression numbers (silver rates, prices, which items are locked behind which bounty) are a first guess.
- The real map: size, layout and look (the hybrid: bounded, mid-to-large).
- Respec is free for now. Keep it free, or give it a cost later?
