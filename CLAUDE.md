# Arena Master

A game built on C-Engine. The engine is the git submodule in `engine/` (repo: iMops019/C-Engine, a local working copy lives at `C:\Users\conov\Documents\C-Engine`).

## Rules

- **Never edit files under `engine/`.** Engine changes go in the engine repo: commit and push there, then run `./update-engine.ps1` here to move the submodule pointer and commit the bump. Anything game-specific stays out of the engine; the engine's `Instructions.md` explains the split.
- The game talks to the engine only through `IGameContent` (`engine/src/CEngine.Core/IGameContent.cs`) and the public `EngineWindow` API. `ArenaMasterContent` in `src/ArenaMaster.Game` is the implementation.
- Game assets go under this repo's `assets/` (`models/`, `textures/`, `sounds/`). The Editor's Save Terrain / Save Scene write `assets/terrain.dat` and `assets/scene.json` here. Raw Meshy exports go in the git-ignored `assets/raw/`; convert them with `engine/tools/asset-pipeline` and commit only the `.glb`.
- Engine architecture and conventions: `engine/CLAUDE.md` (large; search it rather than reading it all) and `engine/Instructions.md`.

## Status

The game is a 3D fantasy survivor-like (Megabonk-style), single player. **Read `DESIGN.md` first**: it has the design, the ground rules (nothing from ConnEngine-Game; one class at a time, starting with the Ranger; classes share nothing), the build order and the open questions. `ArenaMasterContent` is still the engine's blank slate; build-order step 1 hasn't started.
