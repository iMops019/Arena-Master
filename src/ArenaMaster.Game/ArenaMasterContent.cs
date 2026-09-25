using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Ranger;
using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game;

/// <summary>
/// Arena Master's side of the engine seam. For now: a small test map (rolling hills, some trees and rocks to run around and jump onto), the Ranger in third person with an
/// auto-firing bow, and ghouls that keep coming (build-order steps 1 and 2 in DESIGN.md).
/// </summary>
public sealed class ArenaMasterContent : IGameContent
{
    // A 512 m test map: big enough to run around, small enough to build fast. The real map comes later.
    private const int TerrainResolution = 513;
    private const float TerrainWorldSize = 512f;
    private const float TerrainHeightScale = 60f;
    private const float BaseHeight = TerrainHeightScale * 0.3f;   // where the height-based palette is grass

    /// <summary>How long the "slain" message shows before the fight starts over.</summary>
    private const float RespawnDelay = 3f;

    private readonly RangerController _ranger = new();
    private readonly RangerBow _bow = new();
    private readonly PlayerHealth _health = new(100f);
    private readonly EnemyField _enemies = new(new Random());
    private readonly EnemyView _enemyView = new();
    private readonly DamageNumbers _numbers = new();
    private EngineWindow? _window;
    private bool _started;
    private float _respawnIn;
    private int _lastRunKills;

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

        _ranger.Update(window, deltaSeconds);
        _health.Update(deltaSeconds);
        _numbers.Update(deltaSeconds);

        if (_respawnIn > 0f)
        {
            _respawnIn -= deltaSeconds;
            if (_respawnIn <= 0f)
            {
                _health.Restore();
                _enemies.ResetKills();
            }

            return;
        }

        _bow.Update(window, deltaSeconds, _enemies, _numbers, GroundAt);
        var gone = _enemies.Update(deltaSeconds, window.PlayerFeet, GroundAt, _health);
        _enemyView.Sync(window, _enemies, gone, deltaSeconds);

        if (_health.IsDead)
        {
            Slain(window);
        }
    }

    /// <summary>The Ranger went down: clear the field and start over after a moment.</summary>
    private void Slain(EngineWindow window)
    {
        _lastRunKills = _enemies.Kills;
        foreach (var enemy in _enemies.Clear())
        {
            _enemyView.Remove(window, enemy);
        }

        _bow.Clear(window);
        _numbers.Clear();
        _respawnIn = RespawnDelay;
    }

    public void DrawHud(IHud hud)
    {
        var white = new Vector4D<float>(1f, 1f, 1f, 0.9f);
        var shade = new Vector4D<float>(0f, 0f, 0f, 0.45f);

        if (_window?.Camera is { } camera)
        {
            _numbers.Draw(hud, camera);
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

        // Kills, top right.
        hud.Text(HudAnchor.TopRight, new Vector2D<float>(-24f, 20f), $"Kills  {_enemies.Kills}", white, 0.9f);

        // Dash charge, bottom centre: fills back up after each dash.
        float ready = Math.Clamp(_ranger.DashReadiness, 0f, 1f);
        var fill = ready >= 1f ? new Vector4D<float>(0.55f, 0.85f, 0.45f, 0.95f) : new Vector4D<float>(0.45f, 0.55f, 0.45f, 0.8f);
        hud.Bar(HudAnchor.BottomCenter, new Vector2D<float>(0f, -40f), new Vector2D<float>(160f, 8f), ready, fill, shade);
        hud.Text(HudAnchor.BottomCenter, new Vector2D<float>(0f, -54f), "DASH  [Shift]", new Vector4D<float>(1f, 1f, 1f, ready >= 1f ? 0.85f : 0.45f), 0.7f);

        if (_respawnIn > 0f)
        {
            hud.Text(HudAnchor.Center, new Vector2D<float>(0f, -30f), "YOU WERE SLAIN", new Vector4D<float>(0.95f, 0.3f, 0.3f, 1f), 1.8f);
            hud.Text(HudAnchor.Center, new Vector2D<float>(0f, 20f), $"Kills: {_lastRunKills}     Back in {MathF.Ceiling(_respawnIn)}...", white, 0.9f);
        }
    }
}
