using ArenaMaster.Game;
using CEngine.Core;

var window = new EngineWindow
{
    Title = "Arena Master Editor",
    Width = 1280,
    Height = 720,
    Mode = EngineMode.Editor,
    GameContent = new ArenaMasterContent(),
};

window.Run();
