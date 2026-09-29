using ArenaMaster.Game.Delve;
using ArenaMaster.Game.World;
using CEngine.Core;
using ImGuiNET;
using Silk.NET.Maths;

namespace ArenaMaster.Game;

// The cave (World/CaveLayout): built once, on the first frame of play (or from the Editor's preview) - its stalagmites and the tunnel's mouth as props, its crystals,
// bones and lanterns as crowds, the fires by the tunnel. Going down (a run) turns the engine's cave look on and puts the roof up; coming back up to camp takes them
// away. Underground, the nearest of its lights are lit each frame as the player moves.
public sealed partial class ArenaMasterContent
{
    /// <summary>The ids the cave's point lights are given: this plus the light's place in the list.</summary>
    private const int CaveLightIdBase = 7300;

    private const string LanternFireModel = "cave_lantern_fire.glb";

    private bool _caveBuilt;
    private bool _underground;
    private IReadOnlyList<(Vector3D<float> At, Vector3D<float> Colour, float Radius)> _caveLights = Array.Empty<(Vector3D<float>, Vector3D<float>, float)>();
    private readonly HashSet<int> _caveLit = new();

    /// <summary>The way to the player through the cave's tunnels, for the enemies (built with the cave).</summary>
    private CaveFlow? _caveFlow;
    private (float Insects, float Animals)? _surfaceLife;

    /// <summary>Puts the cave's dressing in the world, once: the stalagmites (solid) and the tunnel's mouth with its braziers as props, the rest as crowds.</summary>
    private void BuildCave(EngineWindow window, Terrain terrain)
    {
        if (_caveBuilt)
        {
            return;
        }

        _caveBuilt = true;
        window.PlayerMaxSlope = CaveLayout.PlayerMaxSlope;   // the cave's walls, pillars and ridge are rock, not something to walk up

        var crystals = new List<CrowdInstance>();
        var bones = new List<CrowdInstance>();
        var lanterns = new List<CrowdInstance>();
        foreach (var (thing, at, yaw, scale) in CaveLayout.Things())
        {
            var ground = CaveLayout.Ground(terrain, at);
            switch (thing)
            {
                case CaveThing.Stalagmite:
                    window.PlaceProp(new PropPlacement(CaveLayout.StalagmiteModel, ground - new Vector3D<float>(0f, 0.3f, 0f), yaw, scale, Collision: PropCollision.Cylinder));
                    break;
                case CaveThing.Crystals:
                    crystals.Add(new CrowdInstance(ground, yaw, scale));
                    break;
                case CaveThing.Bones:
                    bones.Add(new CrowdInstance(ground, yaw, scale));
                    break;
                case CaveThing.Lantern:
                    lanterns.Add(new CrowdInstance(ground, yaw, scale));
                    break;
            }
        }

        window.SetCrowd(CaveLayout.CrystalModel, crystals.ToArray());
        window.SetCrowd(CaveLayout.BonesModel, bones.ToArray());
        window.SetCrowd(CaveLayout.LanternModel, lanterns.ToArray());
        window.SetCrowd(LanternFireModel, lanterns.ToArray());
        window.SetCrowdGlow(CaveLayout.CrystalModel, 0.55f);
        window.SetCrowdGlow(LanternFireModel, 1.4f);

        window.PlaceProp(new PropPlacement(CaveLayout.TunnelModel, CaveLayout.Ground(terrain, CaveLayout.Tunnel), 0f, 1f, Collision: PropCollision.Box));
        foreach (var (offset, fireId) in CaveLayout.Braziers)
        {
            var at = CaveLayout.Ground(terrain, CaveLayout.Tunnel + offset);
            window.PlaceProp(new PropPlacement("camp_brazier.glb", at, 0f, 1f, Collision: PropCollision.Cylinder));
            window.AddFire(fireId, at + new Vector3D<float>(0f, CaveLayout.BrazierFireHeight, 0f), scale: 0.6f);
        }

        _caveLights = CaveLayout.Lights(terrain);
        _caveFlow = new CaveFlow(CaveLayout.Grid);
    }

    /// <summary>
    /// Going underground: the engine's cave look on (no sky or sun, a dim cold light, a dark fog, stone-grey ground), the roof up, a low dark mist, no birds or insects.
    /// A Delve floor's own look (<see cref="ApplyLook"/>) tints it after.
    /// </summary>
    private void EnterCave(EngineWindow window)
    {
        if (_underground || window.Terrain is not { } terrain)
        {
            return;
        }

        BuildCave(window, terrain);
        _underground = true;
        var cave = window.Cave;
        cave.Enabled = true;
        cave.FogColor = CaveFog;
        // Darker than at first (the user's call, 2026-09-29: more edge), but not so dark a monster can't be seen coming.
        cave.Visibility = 120f;
        cave.Ambient = 0.15f;
        cave.AmbientTint = new System.Numerics.Vector3(0.68f, 0.76f, 1f);
        cave.TopLight = 0.26f;
        window.GroundMist.Amount = 0.35f;
        window.GroundMist.Color = CaveMist;
        window.GroundMist.ReferenceHeight = CaveLayout.FloorHeight + 0.5f;
        window.WispAmount = 0.5f;
        _enemyView.Brighten = MonsterBrighten;
        if (_caveFlow is { } flow)
        {
            // Enemies come round the walls by the tunnels, and only spawn where there is a way to the player.
            flow.Update(window.PlayerFeet, float.MaxValue);
            _enemies.Steer = flow.Steer;
            _enemies.SpawnFilter = (x, z) => flow.Distance(x, z) <= SpawnPathReach;
        }
        _surfaceLife ??= (window.InsectAmount, window.AnimalAmount);
        window.InsectAmount = 0f;
        window.AnimalAmount = 0f;
        window.SetCrowd(CaveLayout.CeilingModel, new[] { new CrowdInstance(new Vector3D<float>(0f, CaveLayout.CeilingHeight, 0f), 0f) });
        window.SetCrowdGlow(CaveLayout.CeilingModel, CaveLayout.CeilingGlow);
    }

    /// <summary>The cave's air and mist, before a Delve floor tints them.</summary>
    private static readonly System.Numerics.Vector3 CaveFog = new(0.016f, 0.017f, 0.02f);
    private static readonly System.Numerics.Vector3 CaveMist = new(0.12f, 0.15f, 0.13f);

    /// <summary>Coming back up: the sky again, no roof, the cave's lights out, the birds and insects back. (The camp's own sky and weather come back with <see cref="RestoreCampLook"/>.)</summary>
    private void LeaveCave(EngineWindow window)
    {
        if (!_underground)
        {
            return;
        }

        _underground = false;
        _enemyView.Brighten = 0f;
        window.RemovePointLight(PlayerLightId);
        _enemies.Steer = null;
        _enemies.SpawnFilter = null;
        window.Cave.Enabled = false;
        window.SetCrowd(CaveLayout.CeilingModel, ReadOnlySpan<CrowdInstance>.Empty);

        foreach (int index in _caveLit)
        {
            window.RemovePointLight(CaveLightIdBase + index);
        }

        _caveLit.Clear();
        if (_surfaceLife is { } life)
        {
            window.InsectAmount = life.Insects;
            window.AnimalAmount = life.Animals;
            _surfaceLife = null;
        }
    }

    /// <summary>Underground, a Delve floor's look is its mist's colour and its wisps, darkened for the cave (no sky, sun or weather reach down here).</summary>
    private void TintCave(EngineWindow window, DelveBand band)
    {
        if (!_underground)
        {
            return;
        }

        var mist = new System.Numerics.Vector3(band.MistColour.R, band.MistColour.G, band.MistColour.B);
        window.GroundMist.Color = mist * 0.2f;
        window.GroundMist.Amount = 0.2f + 0.3f * band.Mist;
        window.Cave.FogColor = CaveFog + mist * 0.02f;
        window.Weather.Rain = 0f;
        window.Weather.Snowfall = 0f;
        window.Weather.Settle();
    }

    /// <summary>An enemy spawns only where the way to the player along the floor is no longer than this (the spawn ring is 24-38 m straight out).</summary>
    private const float SpawnPathReach = 55f;

    /// <summary>Underground, each frame: the way through the tunnels follows the player, and the nearest lights are lit.</summary>
    private void UpdateCave(EngineWindow window, float deltaSeconds)
    {
        if (!_underground)
        {
            return;
        }

        _caveFlow?.Update(window.PlayerFeet, deltaSeconds);
        UpdateCaveLights(window, window.PlayerFeet);
        window.SetPointLight(PlayerLightId, window.PlayerFeet + new Vector3D<float>(0f, 2.4f, 0f), PlayerLight, PlayerLightReach);
    }

    /// <summary>
    /// The hero's own light underground, a pale warm glow that goes where they go: the floor round them, and the monsters coming at them, are always to be seen
    /// (the user's call, 2026-09-29: a darker cave, the monsters still to be seen).
    /// </summary>
    private const int PlayerLightId = 7299;

    private static readonly Vector3D<float> PlayerLight = new(0.5f, 0.46f, 0.38f);
    private const float PlayerLightReach = 15f;

    /// <summary>How much brighter than the dark round them the monsters are drawn underground (see <see cref="EnemyView.Brighten"/>).</summary>
    private const float MonsterBrighten = 0.5f;

    /// <summary>Lights the cave's lights nearest <paramref name="from"/> (the player, or the Editor's camera), and puts out the ones left behind.</summary>
    private void UpdateCaveLights(EngineWindow window, Vector3D<float> from)
    {
        if (!_underground)
        {
            return;
        }

        var nearest = CaveLayout.Nearest(_caveLights, from, CaveLayout.LitAtOnce);
        foreach (int index in _caveLit.Where(i => !nearest.Contains(i)).ToList())
        {
            window.RemovePointLight(CaveLightIdBase + index);
            _caveLit.Remove(index);
        }

        foreach (int index in nearest)
        {
            var (at, colour, radius) = _caveLights[index];
            if (window.SetPointLight(CaveLightIdBase + index, at, colour, radius))
            {
                _caveLit.Add(index);
            }
        }
    }

    /// <summary>
    /// Where enemies, gems and crates can stand: the ground, except underground in the cave's walls, pillars and the mound over the tunnel (null there, which stops
    /// them walking in).
    /// </summary>
    private Func<float, float, float?> Walkable(Terrain terrain) => (x, z) =>
        _underground && CaveLayout.Blocked(x, z) ? null : terrain.TryGetHeight(x, z, out float height) ? height : null;

    /// <summary>
    /// Shows the cave as a run would, or the surface again - for looking at it from the Editor (its checkbox) or a host rendering frames of it, with no run under way.
    /// </summary>
    public void ShowCave(EngineWindow window, bool underground)
    {
        if (underground)
        {
            EnterCave(window);
        }
        else
        {
            LeaveCave(window);
        }
    }

    /// <summary>Lights the cave's lights nearest <paramref name="from"/>, as a run does each frame round the player.</summary>
    public void LightCave(EngineWindow window, Vector3D<float> from)
    {
        UpdateCaveLights(window, from);
        window.SetPointLight(PlayerLightId, from + new Vector3D<float>(0f, 2.4f, 0f), PlayerLight, PlayerLightReach);
    }

    /// <summary>The Editor's look at the cave: turn the underground look on (building the cave if need be) and fly down to the tunnel's mouth.</summary>
    private void DrawCaveEditorExtras(EngineWindow window)
    {
        ImGui.Separator();
        ImGui.Text("The Delve's cave");
        bool underground = _underground;
        if (ImGui.Checkbox("Underground look (roof, dark, lights)", ref underground))
        {
            ShowCave(window, underground);
        }

        if (ImGui.Button("Fly to the tunnel's mouth") && window.Camera is { } camera && window.Terrain is { } terrain)
        {
            camera.Position = CaveLayout.Ground(terrain, Camp.CampLayout.RunStart) + new Vector3D<float>(0f, 6f, 14f);
            camera.SetOrientation(-90f, -14f);
        }

        if (_underground && window.Camera is { } eye)
        {
            UpdateCaveLights(window, eye.Position);
        }
    }
}
