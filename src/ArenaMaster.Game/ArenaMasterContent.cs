using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game;

/// <summary>
/// Arena Master's side of the engine seam. Placeholder for now: the same blank slate the engine's
/// own Editor starts from (flat grass, no vegetation, no water), until the game's design is settled.
/// </summary>
public sealed class ArenaMasterContent : IGameContent
{
    private const int TerrainResolution = 3201;
    private const float TerrainWorldSize = 3200f;
    private const float TerrainHeightScale = 400f;
    private const float BaseHeight = TerrainHeightScale * 0.3f;

    public string AssetsRoot => Path.Combine(EngineAssets.RepoRoot, "assets");

    public Terrain CreateInitialTerrain() =>
        Terrain.CreateFlat(TerrainResolution, TerrainWorldSize, TerrainHeightScale, BaseHeight);

    public IReadOnlyList<PropPlacement> CreateProps(Terrain terrain) => Array.Empty<PropPlacement>();

    public IReadOnlyList<(VegetationMaterial Material, float[] Vertices, uint[] Indices)> CreateVegetationExtras(Terrain terrain) =>
        Array.Empty<(VegetationMaterial, float[], uint[])>();

    public void ConfigureVegetation(VegetationSettings settings)
    {
    }

    public (Vector3D<float> Position, float YawDegrees, float PitchDegrees) InitialCameraPose =>
        (new Vector3D<float>(0f, BaseHeight + 10f, 25f), -90f, -15f);

    public float? WaterLevel => null;

    public void DrawEditorExtras(EngineWindow window)
    {
    }

    public void Update(EngineWindow window, float deltaSeconds)
    {
    }
}
