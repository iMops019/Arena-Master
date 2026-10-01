# Arena Master

A 3D fantasy survivor-like (Megabonk-style), single player, built on [C-Engine](https://github.com/iMops019/C-Engine), which is included here as a git submodule in `engine/`.

You play one of six classes (Ranger, Paladin, Mage, Shaman, Warrior, Priest): set out from camp, go down into the cave under it, and survive a run against a growing horde, elites and bosses - a 30-minute classic run, a 10-minute Delve, or a boss hunt. Every class attacks and aims on its own; you move, dodge and choose. Level up along the way, and come back with items, gear, silver and passive tree experience to spend. Each class has two passive trees (Sharpshooter and Trapper, Defiance and Crusade, Frost and Pyromancy, Lightning Alignment and Earth Alignment, Berserker and Reaver, Unholy and Grave Calling); the active one is chosen at camp, and it shapes the class's attack. The design, what's built and the build order are in [DESIGN.md](DESIGN.md).

## First-time clone

```bash
git clone --recurse-submodules https://github.com/iMops019/Arena-Master.git
```

Already cloned without it? Run `git submodule update --init`.

## Layout

| Path | What it is |
|---|---|
| `engine/` | C-Engine, pinned to a specific commit (submodule). **Don't edit it here.** |
| `src/ArenaMaster.Game` | The game itself: `ArenaMasterContent` implements the engine's `IGameContent`. |
| `src/ArenaMaster.Editor` | Launches the engine Editor with Arena Master's content. |
| `src/ArenaMaster.Play` | Launches the game in Play mode (title screen: Play or New Game). |
| `tests/ArenaMaster.Game.Tests` | The game's tests: combat, the classes and their trees, items, gear, the Delve, the cave, camp and meta progression. |
| `tools/` | Helper scripts: `make_placeholder_models.py` builds every stand-in model (heroes, enemies, loot, camp, the cave, and each tree's own). |
| `assets/` | Arena Master's own models, textures, sounds, and saved `terrain.dat` / `scene.json`. |

## Running

```bash
dotnet run --project src/ArenaMaster.Play
dotnet run --project src/ArenaMaster.Editor
dotnet test ArenaMaster.slnx
```

The save lives in `%AppData%\ArenaMaster\profile.json`. New Game on the title screen starts over and copies the old save to `profile.backup-<date-time>.json` beside it.

## Controls

| Input | Action |
|---|---|
| Mouse | Look round; every class aims and attacks on its own |
| WASD / Space / Shift | Run / jump / the class's dash (dash, shield rush, blink, surge, battle charge, Rotting Step) |
| E | Use a camp station (the archery target is the passive tree: a tab for each tree, and a button to make one active); close a camp screen |
| 1 / 2 / 3, R | Pick a level-up card; reroll (once bought) |
| Esc | Pause menu (Return to Camp, Settings, Quit) |
| F5 / F6 / F7 / F9, F8 | Dev keys: skip a minute / spawn a Brute / spawn the Hollow King / start a Monster Rush in a run; +1 tree level at camp |

## Updating the engine

1. Make the change in the engine repo (`C:\Users\conov\Documents\C-Engine`), then commit and push it there.
2. Here, run `./update-engine.ps1`. It moves `engine/` to the engine's latest `main` and commits the bump.
3. `git push`.

Each Arena Master commit records exactly which engine commit it was built against, so an engine change never breaks the game until you choose to pull it in.
