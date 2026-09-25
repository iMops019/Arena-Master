# Arena Master

A game built on [C-Engine](https://github.com/iMops019/C-Engine), which is included here as a git submodule in `engine/`.

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
| `src/ArenaMaster.Play` | Launches the game in Play mode. |
| `tools/` | Helper scripts, e.g. `make_placeholder_ranger.py` (builds the stand-in Ranger model). |
| `assets/` | Arena Master's own models, textures, sounds, and saved `terrain.dat` / `scene.json`. |

## Running

```bash
dotnet run --project src/ArenaMaster.Editor
dotnet run --project src/ArenaMaster.Play
```

## Updating the engine

1. Make the change in the engine repo (`C:\Users\conov\Documents\C-Engine`), then commit and push it there.
2. Here, run `./update-engine.ps1`. It moves `engine/` to the engine's latest `main` and commits the bump.
3. `git push`.

Each Arena Master commit records exactly which engine commit it was built against, so an engine change never breaks the game until you choose to pull it in.
