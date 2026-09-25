using ArenaMaster.Game.Camp;
using ArenaMaster.Game.Ranger;
using ArenaMaster.Game.Ui;
using CEngine.Core;
using Silk.NET.Input;

namespace ArenaMaster.Game;

// Camp: walking up to a station and pressing E opens its screen - the item chest, the passive tree, the bounty board, the quartermaster, or the loadout at the
// departure gate, which is where a run begins.
// A screen pauses the world (the engine's GamePaused) and frees the cursor until it is closed.
public sealed partial class ArenaMasterContent
{
    private readonly ItemChestScreen _chestScreen = new();
    private readonly PassiveTreeScreen _treeScreen = new();
    private readonly LoadoutScreen _loadoutScreen = new();
    private readonly RunSummaryScreen _summaryScreen = new();
    private readonly BountyBoardScreen _bountyScreen = new();
    private readonly QuartermasterScreen _shopScreen = new();
    private readonly ConfirmScreen _newGameScreen = new(
        "New game",
        "Start over from nothing? Your item chest, loadout, passive tree, silver, bounties and quartermaster upgrades all go back to the start. Your current save is kept as a backup copy next to it.",
        "Start a new game");

    /// <summary>The station the player is standing at, if any, and what pressing E there does.</summary>
    private (CampStation Station, string Prompt)? _nearStation;

    private void UpdateCamp(EngineWindow window)
    {
        _health.Reset(_stats.MaxHealth);   // camp is safe
        _nearStation = CampLayout.StationNear(window.PlayerFeet);

        // Testing shortcut, like the run's F5-F7: F8 at camp gives the active tree one level, to try deep nodes without the runs. Goes before a real release.
        if (Pressed(window, Key.F8) && _tree.Level < Progression.TreeProgress.MaxLevel)
        {
            _tree.AddExperience(Progression.TreeProgress.RequiredFor(_tree.Level) - _tree.IntoLevel);
            SaveProfile();
        }
        if (_nearStation is not { } near || !Pressed(window, Key.E))
        {
            return;
        }

        switch (near.Station)
        {
            case CampStation.Stash:
                _chestScreen.Open();
                break;
            case CampStation.Tree:
                _treeScreen.Open();
                break;
            case CampStation.Gate:
                Loadout.Sanitize(_profile);
                _loadoutScreen.Open();
                break;
            case CampStation.Bounties:
                _bountyScreen.Open();
                break;
            case CampStation.Quartermaster:
                _shopScreen.Open();
                break;
        }

        window.GamePaused = true;
    }

    /// <summary>Draws whichever camp screen is open and acts on what the player did in it. True if one was open.</summary>
    private bool DrawCampScreens(EngineWindow window)
    {
        if (_chestScreen.IsOpen)
        {
            if (_chestScreen.Draw(_profile))
            {
                CloseScreen(window);
            }

            return true;
        }

        if (_treeScreen.IsOpen)
        {
            bool closed = _treeScreen.Draw(_tree, "Ranger");
            if (_treeScreen.Changed)
            {
                _treeScreen.Changed = false;
                _stats.Tree = SharpshooterBonuses.From(_tree.Save.Ranks);
                SaveProfile();
            }

            if (closed)
            {
                CloseScreen(window);
            }

            return true;
        }

        if (_bountyScreen.IsOpen)
        {
            if (_bountyScreen.Draw(_profile))
            {
                CloseScreen(window);
            }

            return true;
        }

        if (_shopScreen.IsOpen)
        {
            bool closed = _shopScreen.Draw(_profile);
            if (_shopScreen.Changed)
            {
                _shopScreen.Changed = false;
                SaveProfile();
            }

            if (closed)
            {
                CloseScreen(window);
            }

            return true;
        }

        if (_loadoutScreen.IsOpen)
        {
            var result = _loadoutScreen.Draw(_profile);
            if (result != LoadoutScreen.Result.None)
            {
                SaveProfile();   // the loadout is remembered either way
                CloseScreen(window);
                if (result == LoadoutScreen.Result.Begin)
                {
                    BeginRun(window);
                }
            }

            return true;
        }

        return false;
    }

    /// <summary>Closes every camp screen at once (a new game starting over them).</summary>
    private void CloseCampScreens()
    {
        _chestScreen.Close();
        _treeScreen.Close();
        _loadoutScreen.Close();
        _bountyScreen.Close();
        _shopScreen.Close();
    }

    private void CloseScreen(EngineWindow window)
    {
        window.GamePaused = false;
        SwallowKey(Key.E);
    }

    /// <summary>From a run's summary back to camp: by the fire, whole again.</summary>
    private void ReturnToCamp(EngineWindow window)
    {
        _mode = GameMode.Camp;
        _stats.Reset();
        _stats.Tree = SharpshooterBonuses.From(_tree.Save.Ranks);
        _health.Reset(_stats.MaxHealth);
        _condition.Clear();
        if (window.Terrain is { } terrain)
        {
            window.TeleportPlayer(CampLayout.Ground(terrain, CampLayout.Spawn));
        }

        window.GamePaused = false;
        SwallowKey(Key.E);
    }
}
