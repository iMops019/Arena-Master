using ArenaMaster.Game;
using ConnEngine.Core;

var window = new EngineWindow
{
    Title = "Arena Master",
    Width = 1280,
    Height = 720,
    Mode = EngineMode.Play,
    StartOnTitleScreen = true,
    GameContent = new ArenaMasterContent(),
};

window.Run();
