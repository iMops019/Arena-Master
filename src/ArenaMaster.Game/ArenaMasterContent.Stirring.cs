using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.Ui;
using ArenaMaster.Game.World;
using CEngine.Core;
using ImGuiNET;
using Silk.NET.Maths;

namespace ArenaMaster.Game;

// The Stirring (World/Stirring): every few minutes of a run in the cave, a mini boss begins to stir in another chamber - announced, its spot marked by a red sigil
// and a column of light, an arrow at the top of the screen pointing the way along the floor, the count and the distance under the clock - and when the count runs
// out it wakes there. Its kill pays silver, passive tree experience, maybe a Delve Mark and maybe a chest (MiniBosses.Reward). Not in the boss hunt's arena, and
// it waits while a boss is on the field.
public sealed partial class ArenaMasterContent
{
    private const string SigilModel = "stirring_sigil.glb";
    private const string ColumnModel = "stirring_column.glb";
    private const int StirLightId = 7298;

    private Stirring? _stirringEvent;

    /// <summary>The way along the floor to where it stirs (a flow aimed at the spot), for the arrow.</summary>
    private CaveFlow? _stirFlow;

    private Stirring StirringEvent => _stirringEvent ??= new Stirring(_random);

    private void BeginStirring()
    {
        StirringEvent.Reset();
        _stirFlow = null;
    }

    /// <summary>A run's frame of the Stirring: its clocks, the announcements, waking the mini boss, and the marker at its spot.</summary>
    private void UpdateStirring(EngineWindow window, float deltaSeconds)
    {
        if (_plan.Kind == Delve.RunKind.Arena || !_underground || _caveFlow is not { } flow || window.Terrain is not { } terrain)
        {
            return;
        }

        var stirring = StirringEvent;
        var feet = window.PlayerFeet;
        var news = stirring.Update(deltaSeconds, allowed: _enemies.Boss is null,
            () => stirring.PickSpot(new Vector2D<float>(feet.X, feet.Z), flow.Distance, (x, z) => !CaveLayout.Blocked(x, z) && !CaveLife.NearPool(x, z, 2f)));
        switch (news)
        {
            case StirringNews.Began:
                Announce($"{stirring.Kind!.Name} stirs{(stirring.Where is { } where ? $" in {where.Replace("The ", "the ")}" : "")}! Get there before it wakes");
                _stirFlow = new CaveFlow(CaveLayout.Grid);
                _stirFlow.Update(new Vector3D<float>(stirring.Spot.X, 0f, stirring.Spot.Y), float.MaxValue);
                break;

            case StirringNews.Woke:
                var at = CaveLayout.Ground(terrain, stirring.Spot);
                var boss = _enemies.Spawn(at, stirring.Kind!, MonsterRarity.Normal);
                boss.Yaw = MathF.Atan2(feet.X - at.X, feet.Z - at.Z);
                stirring.Woken(boss);
                bool there = Vector3D.Distance(at, feet) < 30f;
                Announce(there ? $"{boss.Kind.Name} wakes!" : $"{boss.Kind.Name} has woken, and hunts you!");
                _stirFlow = null;
                break;
        }

        DrawStirringMarker(window, terrain);
    }

    /// <summary>The red sigil and the column of light where it stirs, pulsing faster as it nears waking, and a red light on the ground there. Nothing otherwise.</summary>
    private void DrawStirringMarker(EngineWindow window, Terrain terrain)
    {
        var stirring = StirringEvent;
        if (stirring.Phase != StirringPhase.Stirring)
        {
            ClearStirringMarker(window);
            return;
        }

        var at = CaveLayout.Ground(terrain, stirring.Spot);
        float urgency = 1f - stirring.WakesIn / Stirring.StirSeconds;
        float pulse = 0.35f + 0.35f * MathF.Abs(MathF.Sin(_runSeconds * (2f + 8f * urgency)));
        window.SetCrowd(SigilModel, new[] { new CrowdInstance(at + new Vector3D<float>(0f, 0.08f, 0f), _runSeconds * 0.6f, 4.5f, Flash: pulse) });
        window.SetCrowd(ColumnModel, new[] { new CrowdInstance(at, 0f, 1f + 0.3f * urgency, Flash: pulse) });
        window.SetCrowdGlow(SigilModel, 1.2f);
        window.SetCrowdGlow(ColumnModel, 1.4f);
        window.SetPointLight(StirLightId, at + new Vector3D<float>(0f, 2f, 0f), new Vector3D<float>(1.3f, 0.2f, 0.15f) * (0.6f + pulse), 12f);
    }

    private static void ClearStirringMarker(EngineWindow window)
    {
        window.SetCrowd(SigilModel, ReadOnlySpan<CrowdInstance>.Empty);
        window.SetCrowd(ColumnModel, ReadOnlySpan<CrowdInstance>.Empty);
        window.RemovePointLight(StirLightId);
    }

    /// <summary>A mini boss slain: what it pays, and the news of it.</summary>
    private void OnMiniBossKilled(Enemy killed)
    {
        var reward = MiniBosses.Reward;
        _runSilver += reward.Silver;
        _runTreeExperience += reward.TreeExperience;
        int levels = _tree.AddExperience(reward.TreeExperience);
        _runTreeLevels += levels;
        var paid = new List<string> { $"+{reward.Silver} silver", $"+{reward.TreeExperience} tree experience" };
        if (_random.NextDouble() < reward.MarkChance)
        {
            _profile.Delve.Marks++;
            paid.Add("a Delve Mark!");
        }

        if (_random.NextDouble() < reward.ItemChance)
        {
            _loot.DropChest(killed.Position, RarityWeights.Elite);
            paid.Add("a chest");
        }

        Announce($"{killed.Kind.Name} is slain: {string.Join(", ", paid)}");
        SaveProfile();
    }

    /// <summary>Under the clock while one stirs: what, where, when it wakes and how far along the floor; while one is awake (and no boss is), its health.</summary>
    private void DrawStirringHud(IHud hud, Vector3D<float> feet)
    {
        var stirring = StirringEvent;
        if (_plan.Kind == Delve.RunKind.Arena)
        {
            return;
        }

        if (stirring.Phase == StirringPhase.Stirring && stirring.Kind is { } kind)
        {
            float along = _stirFlow?.Distance(feet.X, feet.Z) is { } d && float.IsFinite(d) ? d : Vector2D.Distance(stirring.Spot, new Vector2D<float>(feet.X, feet.Z));
            int seconds = (int)MathF.Ceiling(stirring.WakesIn);
            hud.Text(HudAnchor.TopCenter, new Vector2D<float>(0f, 118f), $"{kind.Name} wakes in {seconds / 60}:{seconds % 60:00}  ·  {along:0} m", StirRed, 0.8f);
        }
        else if (stirring.Awake is { IsAlive: true } boss && _enemies.Boss is null)
        {
            hud.Text(HudAnchor.TopCenter, new Vector2D<float>(0f, 64f), boss.Kind.Name.ToUpperInvariant(), StirRed, 0.85f);
            hud.Bar(HudAnchor.TopCenter, new Vector2D<float>(0f, 86f), new Vector2D<float>(380f, 12f), boss.Health / boss.MaxHealth,
                new Vector4D<float>(0.85f, 0.35f, 0.2f, 0.95f), new Vector4D<float>(0f, 0f, 0f, 0.55f));
        }
    }

    private static readonly Vector4D<float> StirRed = new(1f, 0.42f, 0.34f, 1f);

    /// <summary>
    /// The arrow at the top of the screen while one stirs: pointing the way to go along the floor (round the rock, by the tunnels), turned to where the camera
    /// looks - straight up is straight ahead.
    /// </summary>
    private void DrawStirringArrow(EngineWindow window)
    {
        var stirring = StirringEvent;
        if (_mode != GameMode.Run || stirring.Phase != StirringPhase.Stirring || window.Camera is not { } camera || window.GamePaused)
        {
            return;
        }

        var feet = window.PlayerFeet;
        var spot = new Vector3D<float>(stirring.Spot.X, feet.Y, stirring.Spot.Y);
        var way = _stirFlow?.Steer(feet, spot) ?? Geometry.FlatDirection(feet, spot, out _);
        var front = new Vector2D<float>(camera.Front.X, camera.Front.Z);
        var right = new Vector2D<float>(camera.Right.X, camera.Right.Z);
        if (front.LengthSquared < 1e-6f || way == Vector3D<float>.Zero)
        {
            return;
        }

        front = Vector2D.Normalize(front);
        right = Vector2D.Normalize(right);
        float angle = MathF.Atan2(way.X * right.X + way.Z * right.Y, way.X * front.X + way.Z * front.Y);

        var io = ImGui.GetIO();
        var centre = new System.Numerics.Vector2(io.DisplaySize.X / 2f, 170f * UiTheme.Scale);
        var dir = new System.Numerics.Vector2(MathF.Sin(angle), -MathF.Cos(angle));
        var side = new System.Numerics.Vector2(-dir.Y, dir.X);
        float size = 26f * UiTheme.Scale;
        var draw = ImGui.GetForegroundDrawList();
        uint fill = ImGui.GetColorU32(new System.Numerics.Vector4(1f, 0.38f, 0.3f, 0.92f));
        uint edge = ImGui.GetColorU32(new System.Numerics.Vector4(0.1f, 0.02f, 0.02f, 0.9f));
        var tip = centre + dir * size;
        var left = centre - dir * size * 0.55f + side * size * 0.7f;
        var rightCorner = centre - dir * size * 0.55f - side * size * 0.7f;
        var notch = centre - dir * size * 0.15f;
        draw.AddTriangleFilled(tip, left, notch, fill);
        draw.AddTriangleFilled(tip, notch, rightCorner, fill);
        draw.AddTriangle(tip, left, notch, edge, 2f);
        draw.AddTriangle(tip, notch, rightCorner, edge, 2f);
    }
}
