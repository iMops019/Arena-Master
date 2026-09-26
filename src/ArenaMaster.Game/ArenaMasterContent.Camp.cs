using ArenaMaster.Game.Camp;
using ArenaMaster.Game.Ui;
using CEngine.Core;
using Silk.NET.Input;
using Silk.NET.Maths;

namespace ArenaMaster.Game;

// Camp: walking up to a station and pressing E opens its screen - the item chest, the passive tree, the class rack, the bounty board, the quartermaster, the
// armour stand's gear, or the Delve chart at the departure gate, which leads through the loadout to a run: a Delve node, or a classic run.
// A screen pauses the world (the engine's GamePaused) and frees the cursor until it is closed.
// Camp is walled in and dressed, with the quartermaster idling behind his stall; the trip to a run and back goes through a fade to black (Travel).
public sealed partial class ArenaMasterContent
{
    private readonly ItemChestScreen _chestScreen = new();
    private readonly PassiveTreeScreen _treeScreen = new();
    private readonly LoadoutScreen _loadoutScreen = new();
    private readonly RunSummaryScreen _summaryScreen = new();
    private readonly BountyBoardScreen _bountyScreen = new();
    private readonly QuartermasterScreen _shopScreen = new();
    private readonly ClassScreen _classScreen = new();
    private readonly DelveChartScreen _delveScreen = new();
    private readonly GearScreen _gearScreen = new();

    /// <summary>The run chosen on the Delve chart, waiting on the loadout screen.</summary>
    private Delve.RunPlan _nextPlan = Delve.RunPlan.Classic;
    private readonly ConfirmScreen _newGameScreen = new(
        "New game",
        "Start over from nothing? Your item chest, loadout, passive tree, silver, bounties and quartermaster upgrades all go back to the start. Your current save is kept as a backup copy next to it.",
        "Start a new game");

    /// <summary>The station the player is standing at, if any, and what pressing E there does.</summary>
    private (CampStation Station, string Prompt)? _nearStation;

    /// <summary>The fade between camp and a run.</summary>
    private readonly ScreenFade _fade = new();

    /// <summary>How far the quartermaster is into his idle clip; and whether his model could be loaded (null until tried).</summary>
    private float _quartermasterSeconds;
    private bool? _quartermasterLoaded;

    private void UpdateCamp(EngineWindow window, float deltaSeconds)
    {
        _health.Reset(_hero.MaxHealth);   // camp is safe
        _nearStation = CampLayout.StationNear(window.PlayerFeet);
        PoseQuartermaster(window, deltaSeconds);

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
                _delveScreen.Open();
                break;
            case CampStation.Gear:
                _gearScreen.Open();
                break;
            case CampStation.Bounties:
                _bountyScreen.Open();
                break;
            case CampStation.Quartermaster:
                _shopScreen.Open();
                break;
            case CampStation.Classes:
                _classScreen.Open();
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
            bool closed = _treeScreen.Draw(_tree, _hero.Name);
            if (_treeScreen.Changed)
            {
                _treeScreen.Changed = false;
                _hero.UseTree(_tree.Save.Ranks);
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

        if (_classScreen.IsOpen)
        {
            var (closed, chosen) = _classScreen.Draw(_classes, _hero, _profile);
            if (chosen is not null && chosen != _hero)
            {
                _hero.Hide(window);
                UseClass(chosen);
                SaveProfile();
            }

            if (closed)
            {
                CloseScreen(window);
            }

            return true;
        }

        if (_gearScreen.IsOpen)
        {
            bool closed = _gearScreen.Draw(_profile);
            if (_gearScreen.Changed)
            {
                _gearScreen.Changed = false;
                SaveProfile();
            }

            if (closed)
            {
                CloseScreen(window);
            }

            return true;
        }

        if (_delveScreen.IsOpen)
        {
            var (choice, node) = _delveScreen.Draw(_profile);
            switch (choice)
            {
                case DelveChartScreen.Choice.Delve when node is not null:
                    _nextPlan = Delve.RunPlan.For(node);
                    Loadout.Sanitize(_profile);
                    _loadoutScreen.Open();   // still paused: on to the loadout
                    break;
                case DelveChartScreen.Choice.Classic:
                    _nextPlan = Delve.RunPlan.Classic;
                    Loadout.Sanitize(_profile);
                    _loadoutScreen.Open();
                    break;
                case DelveChartScreen.Choice.Close:
                    CloseScreen(window);
                    break;
            }

            return true;
        }

        if (_loadoutScreen.IsOpen)
        {
            var result = _loadoutScreen.Draw(_profile, _hero.Name, _nextPlan.Destination);
            if (result != LoadoutScreen.Result.None)
            {
                SaveProfile();   // the loadout is remembered either way
                if (result == LoadoutScreen.Result.Begin)
                {
                    CloseScreen(window);
                    var plan = _nextPlan;
                    Travel(window, plan.Title, () => BeginRun(window, plan));
                }
                else
                {
                    _delveScreen.Open();   // back to the chart
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
        _classScreen.Close();
        _delveScreen.Close();
        _gearScreen.Close();
    }

    private void CloseScreen(EngineWindow window)
    {
        window.GamePaused = false;
        SwallowKey(Key.E);
    }

    /// <summary>
    /// Goes somewhere through a fade: the world holds still, the picture fades to black, <paramref name="move"/> is made in the dark (with <paramref name="title"/>
    /// on the screen), and the picture comes back on the new place, which starts moving again once it is clear (see <see cref="DrawOverlay"/>).
    /// </summary>
    private void Travel(EngineWindow window, string title, Action move)
    {
        _fade.Start(title, move);
        window.GamePaused = true;
    }

    /// <summary>Puts up the palisade and camp's dressing, and lights the camp fire, the braziers and the cooking fire, once at the start of play.</summary>
    private static void BuildCamp(EngineWindow window, Terrain terrain)
    {
        foreach (var placement in CampLayout.DecorProps(terrain))
        {
            window.PlaceProp(placement);
        }

        window.AddFire(CampLayout.FireId, CampLayout.Ground(terrain, CampLayout.Centre) + new Vector3D<float>(0f, 0.1f, 0f), scale: 1f);
        foreach (var (id, offset, height, scale) in CampLayout.SmallFires())
        {
            window.AddFire(id, CampLayout.Ground(terrain, CampLayout.Centre + offset) + new Vector3D<float>(0f, height, 0f), scale);
        }
    }

    /// <summary>The quartermaster idles behind his stall while the player is at camp.</summary>
    private void PoseQuartermaster(EngineWindow window, float deltaSeconds)
    {
        _quartermasterLoaded ??= window.TryLoadSkinnedModel(QuartermasterNpc.Model, AssetsRoot);
        if (_quartermasterLoaded != true || window.Terrain is not { } terrain)
        {
            return;
        }

        _quartermasterSeconds = QuartermasterNpc.ClipTime(_quartermasterSeconds + deltaSeconds);
        var (offset, yaw) = QuartermasterNpc.Stand();
        window.SetSkinnedPropPose(QuartermasterNpc.Model, QuartermasterNpc.IdleClip, _quartermasterSeconds, CampLayout.Ground(terrain, CampLayout.Centre + offset), yaw, 1f);
    }

    /// <summary>Takes the quartermaster out of the world while the player is away on a run (a skinned model is drawn wherever it is, near or far).</summary>
    private void DismissQuartermaster(EngineWindow window)
    {
        if (_quartermasterLoaded == true)
        {
            window.RemoveSkinnedProp(QuartermasterNpc.Model);
        }
    }

    /// <summary>From a run's summary back to camp: by the fire, whole again.</summary>
    private void ReturnToCamp(EngineWindow window)
    {
        _mode = GameMode.Camp;
        _plan = Delve.RunPlan.Classic;
        RestoreCampLook(window);
        _hero.ReturnToCamp();
        _hero.UseTree(_tree.Save.Ranks);
        _health.Reset(_hero.MaxHealth);
        _condition.Clear();
        if (window.Terrain is { } terrain)
        {
            window.TeleportPlayer(CampLayout.Ground(terrain, CampLayout.Spawn));
        }

        window.GamePaused = false;
        SwallowKey(Key.E);
    }
}
