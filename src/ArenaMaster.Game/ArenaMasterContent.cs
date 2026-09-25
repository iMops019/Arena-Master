using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Ranger;
using ArenaMaster.Game.Ui;
using CEngine.Core;
using Silk.NET.Input;
using Silk.NET.Maths;

namespace ArenaMaster.Game;

/// <summary>
/// Arena Master's side of the engine seam. For now: a small test map (rolling hills, some trees and rocks to run around and jump onto) and a full 30-minute run as
/// the Ranger - an auto-firing bow, a director that grows the ghoul horde over time, elite brutes and the Hollow King with telegraphed attacks, experience gems,
/// level-ups that pause for a pick of three upgrades, and a win at 30:00 or a restart on death (build-order steps 1 to 4 in DESIGN.md).
/// </summary>
public sealed class ArenaMasterContent : IGameContent
{
    // A 512 m test map: big enough to run around, small enough to build fast. The real map comes later.
    private const int TerrainResolution = 513;
    private const float TerrainWorldSize = 512f;
    private const float TerrainHeightScale = 60f;
    private const float BaseHeight = TerrainHeightScale * 0.3f;   // where the height-based palette is grass

    /// <summary>How long the end-of-run screen shows before a new run starts: a death, and a win.</summary>
    private const float SlainScreenSeconds = 3f;
    private const float VictoryScreenSeconds = 6f;

    /// <summary>How much tougher the final boss is than the director's scaling alone would make it.</summary>
    private const float FinalBossHealthBonus = 1.5f;

    private const float AnnouncementSeconds = 3f;

    private readonly Random _random = new();
    private readonly RangerStats _stats = new();
    private readonly RangerController _ranger = new();
    private readonly RangerBow _bow;
    private readonly PlayerHealth _health = new(RangerStats.BaseMaxHealth);
    private readonly PlayerCondition _condition = new();
    private readonly Experience _experience = new();
    private readonly RunDirector _director = new();
    private readonly EnemyField _enemies;
    private readonly EnemyView _enemyView = new();
    private readonly XpGemField _gems = new();
    private readonly XpGemView _gemView = new();
    private readonly DamageNumbers _numbers = new();
    private readonly LevelUpScreen _levelUp = new();
    private readonly HashSet<Key> _keysDown = new();
    private EngineWindow? _window;
    private bool _started;
    private float _runSeconds;
    private int _pendingLevels;

    /// <summary>Seconds left on the end-of-run screen; 0 while a run is being played.</summary>
    private float _endIn;
    private bool _won;
    private (int Kills, int Level, float Seconds) _lastRun;

    private string _announcement = "";
    private float _announcementLeft;

    public ArenaMasterContent()
    {
        _bow = new RangerBow(_random);
        _enemies = new EnemyField(_random);
    }

    public string AssetsRoot => Path.Combine(EngineAssets.RepoRoot, "assets");

    public Terrain CreateInitialTerrain()
    {
        var terrain = Terrain.CreateFlat(TerrainResolution, TerrainWorldSize, TerrainHeightScale, BaseHeight);

        // A few hills of different sizes around the start, to see the camera follow over rises and dips.
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

        return terrain;
    }

    public IReadOnlyList<PropPlacement> CreateProps(Terrain terrain) => Array.Empty<PropPlacement>();

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

    public (Vector3D<float> Position, float YawDegrees, float PitchDegrees) InitialCameraPose =>
        (new Vector3D<float>(0f, BaseHeight + 1.8f, 0f), -90f, -15f);

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
            window.ThirdPerson = true;
            _started = true;
        }

        float? GroundAt(float x, float z) => terrain.TryGetHeight(x, z, out float height) ? height : null;

        _ranger.Update(window, deltaSeconds, _stats, _condition.IsStunned);
        _health.Update(deltaSeconds);
        _condition.Update(deltaSeconds);
        _numbers.Update(deltaSeconds);
        _announcementLeft = MathF.Max(0f, _announcementLeft - deltaSeconds);
        window.PlayerPush = _ranger.DashVelocity + _condition.Knockback;

        if (_endIn > 0f)
        {
            _endIn -= deltaSeconds;
            if (_endIn <= 0f)
            {
                StartRun();
            }

            return;
        }

        DevKeys(window, GroundAt);
        _runSeconds += deltaSeconds;
        if (RunDirector.IsWon(_runSeconds))
        {
            EndRun(window, won: true);
            return;
        }

        FollowOrders(_director.Update(_runSeconds, _enemies), window.PlayerFeet, GroundAt);

        _bow.Update(window, deltaSeconds, _stats, _enemies, _numbers, GroundAt, canFire: !_condition.IsStunned);
        var player = new PlayerTarget(window.PlayerFeet, window.PlayerGrounded, _health, _condition);
        var gone = _enemies.Update(deltaSeconds, player, GroundAt);
        foreach (var killed in _enemies.TakeNewlyKilled())
        {
            _gems.Drop(killed.Position, killed.Kind.Experience);
            if (killed.Kind.Tier == EnemyTier.Boss)
            {
                Announce($"{killed.Kind.Name} has fallen!");
            }
        }

        _enemyView.Sync(window, _enemies, gone, deltaSeconds, GroundAt);

        var collected = new List<XpGem>();
        _pendingLevels += _experience.Add(_gems.Update(deltaSeconds, window.PlayerFeet, _stats.PickupRadius, collected));
        _gemView.Sync(window, _gems, collected, deltaSeconds);

        if (_health.IsDead)
        {
            EndRun(window, won: false);
        }
        else if (_pendingLevels > 0 && !_levelUp.IsOpen)
        {
            OpenLevelUp(window);
        }
    }

    public void DrawOverlay(EngineWindow window)
    {
        if (_levelUp.Draw() is not { } choice)
        {
            return;
        }

        Apply(choice);
        _pendingLevels--;
        if (_pendingLevels > 0)
        {
            OpenLevelUp(window);   // several levels at once: one pick each
        }
        else
        {
            window.GamePaused = false;
        }
    }

    public void DrawHud(IHud hud)
    {
        var white = new Vector4D<float>(1f, 1f, 1f, 0.9f);
        var gold = new Vector4D<float>(1f, 0.84f, 0.35f, 1f);
        var red = new Vector4D<float>(0.95f, 0.3f, 0.3f, 1f);
        var shade = new Vector4D<float>(0f, 0f, 0f, 0.45f);

        if (_window?.Camera is { } camera)
        {
            _numbers.Draw(hud, camera);
        }

        // Experience across the top, with the level at its left end; the run clock under its middle; kills at the right.
        float width = hud.ScreenSize.X - 48f;
        hud.Bar(HudAnchor.TopLeft, new Vector2D<float>(24f, 14f), new Vector2D<float>(width, 12f), _experience.Progress,
            new Vector4D<float>(0.35f, 0.8f, 0.95f, 0.95f), shade);
        hud.Text(HudAnchor.TopLeft, new Vector2D<float>(26f, 32f), $"LV {_experience.Level}", gold, 1f);
        hud.Text(HudAnchor.TopCenter, new Vector2D<float>(0f, 32f), $"{Clock(_runSeconds)} / {Clock(RunDirector.RunLength)}", white, 1.1f);
        hud.Text(HudAnchor.TopRight, new Vector2D<float>(-26f, 32f), $"Kills  {_enemies.Kills}", white, 0.9f);

        // The boss's health, under the clock, while one is on the field.
        if (_enemies.Boss is { } boss)
        {
            hud.Text(HudAnchor.TopCenter, new Vector2D<float>(0f, 64f), boss.Kind.Name.ToUpperInvariant(), red, 0.9f);
            hud.Bar(HudAnchor.TopCenter, new Vector2D<float>(0f, 88f), new Vector2D<float>(520f, 16f), boss.Health / boss.MaxHealth,
                new Vector4D<float>(0.75f, 0.15f, 0.2f, 0.95f), shade);
        }

        // Health, bottom left, flashing red when hit.
        float health = _health.Current / _health.Max;
        var healthColor = Vector4D.Lerp(new Vector4D<float>(0.78f, 0.2f, 0.22f, 0.95f), new Vector4D<float>(1f, 0.55f, 0.55f, 1f), _health.HurtFlash);
        hud.Bar(HudAnchor.BottomLeft, new Vector2D<float>(24f, -28f), new Vector2D<float>(260f, 18f), health, healthColor, shade);
        hud.Text(HudAnchor.BottomLeft, new Vector2D<float>(28f, -50f), $"HP  {MathF.Ceiling(_health.Current)} / {_health.Max}", white, 0.75f);
        if (_health.HurtFlash > 0f)
        {
            hud.Rect(HudAnchor.TopLeft, Vector2D<float>.Zero, new Vector2D<float>(hud.ScreenSize.X, hud.ScreenSize.Y), new Vector4D<float>(0.7f, 0f, 0f, 0.18f * _health.HurtFlash));
        }

        // Dash charge, bottom centre: fills back up after each dash.
        float ready = Math.Clamp(_ranger.DashReadiness, 0f, 1f);
        var fill = ready >= 1f ? new Vector4D<float>(0.55f, 0.85f, 0.45f, 0.95f) : new Vector4D<float>(0.45f, 0.55f, 0.45f, 0.8f);
        hud.Bar(HudAnchor.BottomCenter, new Vector2D<float>(0f, -40f), new Vector2D<float>(160f, 8f), ready, fill, shade);
        hud.Text(HudAnchor.BottomCenter, new Vector2D<float>(0f, -54f), "DASH  [Shift]", new Vector4D<float>(1f, 1f, 1f, ready >= 1f ? 0.85f : 0.45f), 0.7f);

        if (_condition.IsStunned)
        {
            hud.Text(HudAnchor.Center, new Vector2D<float>(0f, 60f), "STUNNED", new Vector4D<float>(1f, 0.85f, 0.3f, 1f), 1.2f);
        }

        if (_announcementLeft > 0f && _endIn <= 0f)
        {
            float alpha = MathF.Min(1f, _announcementLeft);
            hud.Text(HudAnchor.Center, new Vector2D<float>(0f, -120f), _announcement, new Vector4D<float>(red.X, red.Y, red.Z, alpha), 1.4f);
        }

        if (_endIn > 0f)
        {
            hud.Text(HudAnchor.Center, new Vector2D<float>(0f, -40f), _won ? "VICTORY" : "YOU WERE SLAIN", _won ? gold : red, 1.8f);
            hud.Text(HudAnchor.Center, new Vector2D<float>(0f, 10f), $"Survived {Clock(_lastRun.Seconds)}     Level {_lastRun.Level}     Kills {_lastRun.Kills}", white, 0.9f);
            hud.Text(HudAnchor.Center, new Vector2D<float>(0f, 44f), $"New run in {MathF.Ceiling(_endIn)}...", white, 0.8f);
        }
    }

    /// <summary>Spawns what the director asked for: a wave of elites, a boss.</summary>
    private void FollowOrders(DirectorOrders orders, Vector3D<float> playerFeet, Func<float, float, float?> groundAt)
    {
        for (int i = 0; i < orders.Elites; i++)
        {
            _enemies.SpawnAround(EnemyKind.Brute, playerFeet, groundAt);
        }

        if (orders.Elites > 0)
        {
            Announce(orders.Elites == 1 ? "A Ghoul Brute approaches" : $"{orders.Elites} Ghoul Brutes approach");
        }

        if (orders.Boss)
        {
            SpawnBoss(playerFeet, groundAt, orders.FinalBoss);
        }
    }

    private void SpawnBoss(Vector3D<float> playerFeet, Func<float, float, float?> groundAt, bool final)
    {
        var scaling = _enemies.Scaling;
        if (final)
        {
            _enemies.Scaling = scaling with { Health = scaling.Health * FinalBossHealthBonus };
        }

        _enemies.SpawnAround(EnemyKind.HollowKing, playerFeet, groundAt);
        _enemies.Scaling = scaling;
        Announce(final ? "The Hollow King returns, in full fury" : "The Hollow King rises");
    }

    private void Announce(string text)
    {
        _announcement = text;
        _announcementLeft = AnnouncementSeconds;
    }

    /// <summary>
    /// Testing shortcuts, for now: F5 skips a minute ahead on the run clock, F6 brings in an elite, F7 brings in the boss. They go before a real release.
    /// </summary>
    private void DevKeys(EngineWindow window, Func<float, float, float?> groundAt)
    {
        if (Pressed(window, Key.F5))
        {
            _runSeconds = MathF.Min(_runSeconds + 60f, RunDirector.RunLength - 1f);
        }

        if (Pressed(window, Key.F6))
        {
            _enemies.SpawnAround(EnemyKind.Brute, window.PlayerFeet, groundAt);
            Announce("A Ghoul Brute approaches");
        }

        if (Pressed(window, Key.F7))
        {
            SpawnBoss(window.PlayerFeet, groundAt, final: false);
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

    /// <summary>Pauses the world and offers three upgrades for the next level still to be picked for.</summary>
    private void OpenLevelUp(EngineWindow window)
    {
        int reached = _experience.Level - _pendingLevels + 1;
        _levelUp.Open(RangerUpgrades.Roll(_stats, _random), reached);
        window.GamePaused = true;
    }

    private void Apply(UpgradeChoice choice)
    {
        if (choice.Upgrade is not { } upgrade)
        {
            _health.Heal(RangerUpgrades.SecondWindHeal);
            return;
        }

        _stats.Increase(upgrade);
        if (upgrade == RangerUpgrade.Vitality)
        {
            _health.RaiseMax(_stats.MaxHealth - _health.Max);
        }
    }

    /// <summary>The run is over - won at 30:00, or lost on death: clear the field, remember how it went, and start over after a moment.</summary>
    private void EndRun(EngineWindow window, bool won)
    {
        _won = won;
        _lastRun = (_enemies.Kills, _experience.Level, _runSeconds);
        foreach (var enemy in _enemies.Clear())
        {
            _enemyView.Remove(window, enemy);
        }

        _gemView.Sync(window, _gems, _gems.Clear(), 0f);
        _bow.Clear(window);
        _numbers.Clear();
        _levelUp.Close();
        _pendingLevels = 0;
        _condition.Clear();
        window.GamePaused = false;
        _endIn = won ? VictoryScreenSeconds : SlainScreenSeconds;
    }

    /// <summary>A fresh run: level 1, no upgrades, full health, the clock and the director back at zero.</summary>
    private void StartRun()
    {
        _stats.Reset();
        _experience.Reset();
        _health.Reset(_stats.MaxHealth);
        _enemies.ResetKills();
        _director.Reset();
        _runSeconds = 0f;
    }

    private static string Clock(float seconds) => $"{(int)seconds / 60:00}:{(int)seconds % 60:00}";
}
