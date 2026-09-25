# Arena Master

A game built on ConnEngine-3D. The engine is the git submodule in `engine/` (repo: iMops019/C-Engine, a local working copy lives at `C:\Users\conov\Documents\ConnEngine-3D`).

## Rules

- **Never edit files under `engine/`.** Engine changes go in the engine repo: commit and push there, then run `./update-engine.ps1` here to move the submodule pointer and commit the bump. Anything game-specific stays out of the engine; the engine's `Instructions.md` explains the split.
- The game talks to the engine only through `IGameContent` (`engine/src/ConnEngine.Core/IGameContent.cs`) and the public `EngineWindow` API. `ArenaMasterContent` in `src/ArenaMaster.Game` is the implementation.
- Game assets go under this repo's `assets/` (`models/`, `textures/`, `sounds/`). The Editor's Save Terrain / Save Scene write `assets/terrain.dat` and `assets/scene.json` here. Raw Meshy exports go in the git-ignored `assets/raw/`; convert them with `engine/tools/asset-pipeline` and commit only the `.glb`.
- Engine architecture and conventions: `engine/CLAUDE.md` (large; search it rather than reading it all) and `engine/Instructions.md`.

## Status

Scaffold only: `ArenaMasterContent` starts from the engine's blank slate (flat grass, no vegetation, no water). The game design hasn't been decided yet.
