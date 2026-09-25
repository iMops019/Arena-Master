using ArenaMaster.Game.Ranger;
using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game;

/// <summary>
/// Arena Master's side of the engine seam. For now: a small test map (rolling hills, some trees and rocks to run around and jump onto) and the Ranger in third person - build-order
/// step 1 in DESIGN.md.
/// </summary>
public sealed class ArenaMasterContent : IGameContent
{
    // A 512 m test map: big enough to run around, small enough to build fast. The real map comes later.
    private const int TerrainResolution = 513;
    private const float TerrainWorldSize = 512f;
    private const float TerrainHeightScale = 60f;
    private const float BaseHeight = TerrainHeightScale * 0.3f;   // where the height-based palette is grass

    private readonly RangerController _ranger = new();
    private bool _started;

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
        if (window.Mode != EngineMode.Play)
        {
            return;
        }

        if (!_started)
        {
            window.ThirdPerson = true;
            _started = true;
        }

        _ranger.Update(window, deltaSeconds);
    }

    public void DrawHud(IHud hud)
    {
        // Dash charge, bottom centre: fills back up after each dash.
        float ready = Math.Clamp(_ranger.DashReadiness, 0f, 1f);
        var fill = ready >= 1f ? new Vector4D<float>(0.55f, 0.85f, 0.45f, 0.95f) : new Vector4D<float>(0.45f, 0.55f, 0.45f, 0.8f);
        hud.Bar(HudAnchor.BottomCenter, new Vector2D<float>(0f, -40f), new Vector2D<float>(160f, 8f), ready, fill, new Vector4D<float>(0f, 0f, 0f, 0.45f));
        hud.Text(HudAnchor.BottomCenter, new Vector2D<float>(0f, -54f), "DASH  [Shift]", new Vector4D<float>(1f, 1f, 1f, ready >= 1f ? 0.85f : 0.45f), 0.7f);
    }
}
