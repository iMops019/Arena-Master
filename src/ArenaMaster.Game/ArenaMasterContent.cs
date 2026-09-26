using System.Diagnostics.CodeAnalysis;
using ArenaMaster.Game.Camp;
using ArenaMaster.Game.Classes;
using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.Mage;
using ArenaMaster.Game.Paladin;
using ArenaMaster.Game.Progression;
using ArenaMaster.Game.Ranger;
using ArenaMaster.Game.Shaman;
using ArenaMaster.Game.Ui;
using ArenaMaster.Game.World;
using CEngine.Core;
using Silk.NET.Input;
using Silk.NET.Maths;

namespace ArenaMaster.Game;

/// <summary>Where the player is in the game's loop.</summary>
internal enum GameMode
{
    /// <summary>At camp: walking around, using the stations, choosing a loadout.</summary>
    Camp,

    /// <summary>On a run.</summary>
    Run,

    /// <summary>The run just ended; its summary is up.</summary>
    Summary,
}

/// <summary>
/// Arena Master's side of the engine seam. The player starts at camp (see <c>ArenaMasterContent.Camp.cs</c>): the item chest, the passive tree, the class rack and
/// the departure gate. Setting out starts a 30-minute run (<c>ArenaMasterContent.Run.cs</c>) in the middle of the map as the chosen class - the Ranger, the
/// Paladin, the Mage or the Shaman, each an <see cref="IHeroClass"/>; the run ends in victory, death, or a return to camp, shows its summary, and puts the player back at camp. What lasts
/// between runs - the item stash, the loadout, the class and its tree - is the <see cref="Profile"/>, saved as it changes. This file holds the world and the
/// switching between those parts; the HUD is in <c>ArenaMasterContent.Hud.cs</c>.
/// </summary>
public sealed partial class ArenaMasterContent : IGameContent
{
    // A 512 m test map: big enough to run around, small enough to build fast. The real map comes later.
    private const int TerrainResolution = 513;
    private const float TerrainWorldSize = 512f;
    private const float TerrainHeightScale = 60f;
    private const float BaseHeight = TerrainHeightScale * 0.3f;   // where the height-based palette is grass

    private readonly Random _random = new();
    private readonly string _profilePath;
    private Profile _profile;
    private TreeProgress _tree;

    /// <summary>Every playable class, and the one being played (the profile's choice).</summary>
    private readonly IHeroClass[] _classes;
    private IHeroClass _hero;
    private readonly PlayerHealth _health = new(100f);
    private readonly PlayerCondition _condition = new();
    private readonly HashSet<Key> _keysDown = new();
    private EngineWindow? _window;
    private bool _started;
    private GameMode _mode = GameMode.Camp;
    private string? _saveProblem;
    private bool _newGameRequested;

    public ArenaMasterContent()
        : this(ProfileStore.DefaultPath)
    {
    }

    /// <param name="profilePath">Where the player's progress is saved.</param>
    internal ArenaMasterContent(string profilePath)
    {
        _profilePath = profilePath;
        _profile = ProfileStore.Load(profilePath);
        Loadout.Sanitize(_profile);
        _classes = new IHeroClass[] { new RangerClass(_random), new PaladinClass(_random), new MageClass(_random), new ShamanClass(_random) };
        UseClass(ClassFor(_profile.ActiveClass));
        _health.Reset(_hero.MaxHealth);
        _enemies = new EnemyField(_random);
        _crates = new CrateField(_random);
        _loot = new LootField(_random)
        {
            Available = item => Bounties.IsUnlocked(item, _profile),   // locked items don't come out of chests or drops until their bounty is done
            Luck = weights => Shop.Lucky(weights, _profile),
        };
    }

    public string AssetsRoot => Path.Combine(EngineAssets.RepoRoot, "assets");

    /// <summary>
    /// Asks for a new game - everything that lasts between runs back to nothing - behind a confirmation screen, shown on the next frame of play. The Play host wires
    /// this to the title screen's New Game button.
    /// </summary>
    public void RequestNewGame() => _newGameRequested = true;

    public Terrain CreateInitialTerrain()
    {
        var terrain = Terrain.CreateFlat(TerrainResolution, TerrainWorldSize, TerrainHeightScale, BaseHeight);

        // A few hills of different sizes around the middle, where runs are played.
        (float X, float Z, float Radius, float Height)[] hills =
        {
            (30f, -40f, 18f, 6f),
            (-45f, -25f, 26f, 9f),
            (60f, 35f, 35f, 12f),
            (-70f, 60f, 22f, 5f),
            (5f, 80f, 14f, 3f),
            (-20f, -95f, 40f, 14f),
        };
        foreach (var hill in hills)
        {
            terrain.ApplyBrush(hill.X, hill.Z, hill.Radius, hill.Height);
        }

        CampLayout.PaintClearing(terrain);
        return terrain;
    }

    public IReadOnlyList<PropPlacement> CreateProps(Terrain terrain) => CampLayout.Props(terrain).ToList();

    public IReadOnlyList<(VegetationMaterial Material, float[] Vertices, uint[] Indices)> CreateVegetationExtras(Terrain terrain) =>
        Array.Empty<(VegetationMaterial, float[], uint[])>();

    public void ConfigureVegetation(VegetationSettings settings)
    {
        // Light cover: something to judge speed against, and trunks and rocks to test collision on.
        settings.TreeDensity = 0.25f;
        settings.RockDensity = 0.6f;
        settings.GroundFoliageDensity = 0.4f;
        settings.GroundCoverDensity = 0.3f;
        settings.FlowerDensity = 0.2f;
    }

    /// <summary>The game starts at camp, a few steps from the fire.</summary>
    public (Vector3D<float> Position, float YawDegrees, float PitchDegrees) InitialCameraPose =>
        (new Vector3D<float>(CampLayout.Spawn.X, BaseHeight + 1.8f, CampLayout.Spawn.Y), CampLayout.SpawnYawDegrees, -12f);

    public float? WaterLevel => null;

    public void DrawEditorExtras(EngineWindow window)
    {
    }

    public void Update(EngineWindow window, float deltaSeconds)
    {
        if (window.Mode != EngineMode.Play || window.Terrain is not { } terrain)
        {
            return;
        }

        _window = window;
        if (!_started)
        {
            Start(window, terrain);
        }

        float? GroundAt(float x, float z) => terrain.TryGetHeight(x, z, out float height) ? height : null;

        if (_newGameRequested)
        {
            _newGameRequested = false;
            _newGameScreen.Open();
            window.GamePaused = true;
            return;
        }

        _hero.Move(window, deltaSeconds, _condition.IsStunned);
        _health.Update(deltaSeconds);
        _condition.Update(deltaSeconds);
        _numbers.Update(deltaSeconds);
        _announcementLeft = MathF.Max(0f, _announcementLeft - deltaSeconds);
        UpdateItemToasts(deltaSeconds);
        window.PlayerPush = _hero.DashVelocity + _condition.Knockback;

        switch (_mode)
        {
            case GameMode.Camp:
                UpdateCamp(window);
                break;
            case GameMode.Run:
                UpdateRun(window, deltaSeconds, GroundAt);
                break;
        }
    }

    /// <summary>The game's own screens, over the world: whichever is open takes the frame.</summary>
    public void DrawOverlay(EngineWindow window)
    {
        if (_newGameScreen.IsOpen)
        {
            switch (_newGameScreen.Draw())
            {
                case ConfirmScreen.Answer.Confirm:
                    StartNewGame(window);
                    break;
                case ConfirmScreen.Answer.Cancel:
                    window.GamePaused = _levelUp.IsOpen || _summaryScreen.IsOpen;   // back to whatever was up before
                    break;
            }

            return;
        }

        if (_summaryScreen.IsOpen)
        {
            if (_summaryScreen.Draw())
            {
                ReturnToCamp(window);
            }

            return;
        }

        if (DrawCampScreens(window))
        {
            return;
        }

        DrawLevelUp(window);
    }

    /// <summary>The first frame of play: third person, the camp fire lit, the pause menu's own button.</summary>
    private void Start(EngineWindow window, Terrain terrain)
    {
        window.ThirdPerson = true;
        var fire = CampLayout.Ground(terrain, CampLayout.Centre);
        window.AddFire(CampLayout.FireId, fire + new Vector3D<float>(0f, 0.1f, 0f), scale: 1f);
        window.AddPauseMenuButton("Return to Camp", () =>
        {
            if (_mode == GameMode.Run)
            {
                EndRun(window, RunEnding.ReturnedToCamp);
            }
            else if (_mode == GameMode.Camp)
            {
                window.TeleportPlayer(CampLayout.Ground(terrain, CampLayout.Spawn));
            }
        });
        _started = true;
    }

    /// <summary>
    /// A new game: the old save is copied aside (never deleted), then everything that lasts between runs starts again from nothing - the chest, the loadout, the tree,
    /// silver, bounties and upgrades - and the player is back at camp. A run in progress ends without paying out.
    /// </summary>
    private void StartNewGame(EngineWindow window)
    {
        if (_mode != GameMode.Camp)
        {
            ClearField(window);
        }

        _summaryScreen.Close();
        CloseCampScreens();

        try
        {
            ProfileStore.Backup(_profilePath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _saveProblem = $"Couldn't back up the old save: {e.Message}";
        }

        _profile = new Profile();
        var hero = ClassFor(_profile.ActiveClass);
        if (hero != _hero)
        {
            _hero.Hide(window);
        }

        UseClass(hero);
        SaveProfile();
        ReturnToCamp(window);
    }

    /// <summary>The class with id <paramref name="id"/>, or the first (the Ranger) if there is none - an old or hand-edited save.</summary>
    private IHeroClass ClassFor(string id) => _classes.FirstOrDefault(c => c.Id == id) ?? _classes[0];

    /// <summary>
    /// Makes <paramref name="hero"/> the class played: it goes in the profile, its tree's progress becomes the active tree, and it takes that tree's bonuses. The
    /// caller takes the old class's body out of the world.
    /// </summary>
    [MemberNotNull(nameof(_hero), nameof(_tree))]
    private void UseClass(IHeroClass hero)
    {
        _hero = hero;
        _profile.ActiveClass = hero.Id;
        _profile.ActiveTree = hero.Tree.Id;
        _tree = new TreeProgress(hero.Tree, _profile.Tree(hero.Id, hero.Tree.Id));
        hero.UseTree(_tree.Save.Ranks);
    }

    /// <summary>Writes the profile. A failure is shown on the HUD rather than stopping the game.</summary>
    private void SaveProfile()
    {
        try
        {
            ProfileStore.Save(_profile, _profilePath);
            _saveProblem = null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _saveProblem = $"Couldn't save your progress: {e.Message}";
        }
    }

    /// <summary>True on the frame <paramref name="key"/> goes down.</summary>
    private bool Pressed(EngineWindow window, Key key)
    {
        bool down = window.Keyboard?.IsKeyPressed(key) == true;
        bool wasDown = _keysDown.Contains(key);
        if (down)
        {
            _keysDown.Add(key);
        }
        else
        {
            _keysDown.Remove(key);
        }

        return down && !wasDown;
    }

    /// <summary>
    /// Treats <paramref name="key"/> as already held, so it has to be let go before it counts again. After a screen closes on a key press the key is usually still down,
    /// and without this the next frame would read it as a fresh press and open the screen again.
    /// </summary>
    private void SwallowKey(Key key) => _keysDown.Add(key);

    private static string Clock(float seconds) => $"{(int)seconds / 60:00}:{(int)seconds % 60:00}";
}
