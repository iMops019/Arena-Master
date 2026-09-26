using ArenaMaster.Game.Camp;
using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Delve;
using ArenaMaster.Game.Gear;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.Progression;
using ArenaMaster.Game.Ui;
using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game;

// The Delve: a run on one of the chart's nodes. A Delve node is about 10 minutes on the usual map (DelveDirector's schedule), lit and weathered for its floor's
// band; a Boss node is the Hollow King Unbound alone in the arena. Either way the boss leaves a Delve cache, and opening it clears the node: its reward is paid,
// the floor below opens, and the run's summary follows. Gear worn at the armour stand counts on every run.
public sealed partial class ArenaMasterContent
{
    /// <summary>How close the player must come to the Delve cache to open it, and how long it takes to open.</summary>
    private const float CacheReach = 2.2f;
    private const float CacheOpenSeconds = 0.8f;
    private const string CacheModel = "delve_cache.glb";
    private const string CacheBeamModel = "loot_beam.glb";

    /// <summary>How long the arena's King waits after the fight begins (the player's upgrades picked) before his first attack.</summary>
    private const float ArenaGrace = 4f;

    private RunPlan _plan = RunPlan.Classic;
    private DelveDirector? _delveDirector;

    /// <summary>The Delve cache, once the boss has left it: where it stands, its props, and how far along opening it is (null until it is touched).</summary>
    private Vector3D<float>? _cacheAt;
    private int? _cacheProp;
    private int? _cacheBeam;
    private float? _cacheOpening;
    private float _cacheSpin;

    /// <summary>What the engine's sky and weather were at camp, put back on the way home.</summary>
    private CampLook? _campLook;

    private sealed record CampLook(float TimeOfDay, bool AutoDayCycle, float Mist, System.Numerics.Vector3 MistColour, float MistHeight, float Fog, float Cloud, float Rain,
        float Snow, float Warmth, float Wisps);

    /// <summary>Puts the arena up (its wall, braziers and fires), once at the start of play.</summary>
    private static void BuildArena(EngineWindow window, Terrain terrain)
    {
        foreach (var placement in BossArena.Props(terrain))
        {
            window.PlaceProp(placement);
        }

        foreach (var (id, offset, height, scale) in BossArena.Fires())
        {
            window.AddFire(id, BossArena.Ground(terrain, BossArena.Centre + offset) + new Vector3D<float>(0f, height, 0f), scale);
        }
    }

    /// <summary>The worn gear's bonuses join the run's, after the loadout's items.</summary>
    private void WearGear()
    {
        foreach (var piece in GearCatalog.Worn(_profile))
        {
            _items.Carried.AddBonus(piece.Apply);
        }
    }

    /// <summary>
    /// The start of a Delve or Boss node's run, after the common setup: the floor's look, and for a Delve node its director; for a Boss node the arena - the player at
    /// its edge, the King in the middle, no swarm, and the player's levels to pick before the fight.
    /// </summary>
    private void BeginDelve(EngineWindow window, Terrain terrain)
    {
        _cacheAt = null;
        _cacheOpening = null;
        _delveDirector = null;
        if (_plan.Node is not { } node)
        {
            return;
        }

        ApplyLook(window, DelveBands.For(node.Depth));
        if (_plan.Kind == RunKind.Delve)
        {
            _delveDirector = new DelveDirector(node.Depth);
            window.TeleportPlayer(CampLayout.Ground(terrain, CampLayout.RunStart));
            Announce($"Depth {node.Depth}: the Hollow King comes at 10:00");
            return;
        }

        window.TeleportPlayer(BossArena.Ground(terrain, BossArena.Start));
        _enemies.TargetCount = 0;
        _enemies.Mix = Array.Empty<(EnemyKind, float)>();
        _enemies.Scaling = new EnemyScaling(DelveRules.HealthMultiplier(node.Depth), DelveRules.DamageMultiplier(node.Depth), 1f);
        var king = _enemies.Spawn(BossArena.Ground(terrain, BossArena.BossStart), DelveBosses.HollowKingUnbound);
        king.Yaw = MathF.PI;   // facing the way in
        king.AttackCooldown = ArenaGrace;

        // The fight starts at a set level: every level's upgrade is picked first, one screen at a time.
        int level = DelveRules.ArenaLevel(node.Depth);
        int experience = 0;
        for (int l = 1; l < level; l++)
        {
            experience += Experience.RequiredFor(l);
        }

        _pendingLevels += _experience.Add(experience);
        Announce("The Hollow King Unbound awaits");
    }

    /// <summary>A Delve run's own frame: its director (a Boss node has none), the boss's stages, and the cache.</summary>
    private void UpdateDelve(EngineWindow window, float deltaSeconds, Func<float, float, float?> groundAt)
    {
        if (_delveDirector is { } director)
        {
            var orders = director.Update(_runSeconds, _enemies, _enemies.Boss);
            FollowOrders(orders, window.PlayerFeet, groundAt);
        }

        // A new stage of the arena's King: his line, and a Brute (two when enraged) stepping out at his side - inside the wall, not in the usual ring out past it.
        foreach (var (boss, phase) in _enemies.TakePhaseChanges())
        {
            Announce(phase.Announcement);
            int brutes = _plan.Kind == RunKind.Arena ? (phase == DelveBosses.HollowKingUnbound.Phases[^1] ? 2 : 1) : 0;
            for (int i = 0; i < brutes; i++)
            {
                float angle = boss.Yaw + (i == 0 ? 1.6f : -1.6f);
                float x = boss.Position.X + MathF.Sin(angle) * 4f;
                float z = boss.Position.Z + MathF.Cos(angle) * 4f;
                if (groundAt(x, z) is { } ground)
                {
                    _enemies.Spawn(new Vector3D<float>(x, ground, z), EnemyKind.Brute);
                }
            }
        }

        UpdateCache(window, deltaSeconds);
    }

    /// <summary>The boss of a Delve run has fallen: his cache appears where he fell (in the arena, in its middle).</summary>
    private void DropCache(EngineWindow window, Vector3D<float> at)
    {
        if (_cacheAt is not null || window.Terrain is not { } terrain)
        {
            return;
        }

        var spot = _plan.Kind == RunKind.Arena ? BossArena.Ground(terrain, BossArena.Centre) : at;
        _cacheAt = spot;
        _cacheSpin = 0f;
        _cacheProp = window.PlaceProp(new PropPlacement(CacheModel, spot, 0f, 1.2f, Collision: PropCollision.None));
        _cacheBeam = window.PlaceProp(new PropPlacement(CacheBeamModel, spot, 0f, 1.6f));
        Announce("The Delve cache is yours: open it to clear the floor");
    }

    /// <summary>Turns the cache to catch the eye, and opens it when the player reaches it; once open, the node is cleared.</summary>
    private void UpdateCache(EngineWindow window, float deltaSeconds)
    {
        if (_cacheAt is not { } at || _cacheProp is not { } prop)
        {
            return;
        }

        _cacheSpin += deltaSeconds;
        float lift = _cacheOpening is { } t ? 0.3f * Math.Clamp(t / CacheOpenSeconds, 0f, 1f) : 0f;
        window.SetPlacedProp(prop, new PropPlacement(CacheModel, at + new Vector3D<float>(0f, lift, 0f), _cacheSpin * 0.6f, 1.2f + lift));

        if (_cacheOpening is { } opening)
        {
            _cacheOpening = opening + deltaSeconds;
            if (_cacheOpening >= CacheOpenSeconds)
            {
                EndRun(window, RunEnding.DelveCleared);
            }

            return;
        }

        var feet = window.PlayerFeet;
        if (Vector2D.Distance(new Vector2D<float>(feet.X, feet.Z), new Vector2D<float>(at.X, at.Z)) <= CacheReach)
        {
            _cacheOpening = 0f;
        }
    }

    private void RemoveCache(EngineWindow window)
    {
        if (_cacheProp is { } prop)
        {
            window.RemovePlacedProp(prop);
        }

        if (_cacheBeam is { } beam)
        {
            window.RemovePlacedProp(beam);
        }

        _cacheProp = null;
        _cacheBeam = null;
        _cacheAt = null;
        _cacheOpening = null;
    }

    /// <summary>
    /// The end of a Delve run: if its node was cleared, the cache pays out (silver, tree experience, items, gear, Delve Marks, as the node says) and the node is
    /// marked cleared, opening the floor below. Returns what happened, for the summary; null for a classic run.
    /// </summary>
    private DelveOutcome? SettleDelve(RunEnding ending)
    {
        if (_plan.Node is not { } node)
        {
            return null;
        }

        if (ending != RunEnding.DelveCleared)
        {
            return new DelveOutcome(node.Depth, node.Name, Cleared: false);
        }

        var reward = DelveRules.Reward(node);
        var carried = _items.Carried.Bonuses;
        long silver = (long)MathF.Round(reward.Silver * (1f + carried.CacheSilver));

        var items = new List<RunItem>();
        for (int i = 0; i < reward.Items; i++)
        {
            var item = Items.ItemCatalog.Roll(_random, Shop.Lucky(RarityWeights.Boss, _profile), item => Bounties.IsUnlocked(item, _profile));
            GainItem(item);
            items.Add(item);
        }

        GearPiece? gear = null;
        if (reward.Gear)
        {
            gear = GearCatalog.Grant(_profile, _random);
            if (gear is null)
            {
                silver += DelveRules.NoGearSilver;   // every piece owned already
            }
        }

        if (reward.TreeExperience > 0)
        {
            _runTreeLevels += _tree.AddExperience(reward.TreeExperience);
        }

        _profile.Silver += silver;
        _profile.Delve.Marks += reward.Marks;
        DelveRules.Clear(_profile.Delve, node);
        return new DelveOutcome(node.Depth, node.Name, Cleared: true, silver, reward.TreeExperience, reward.Marks, gear, items);
    }

    /// <summary>Lights and weathers the world for a floor's band, keeping camp's own to put back afterwards.</summary>
    private void ApplyLook(EngineWindow window, DelveBand band)
    {
        _campLook ??= new CampLook(window.TimeOfDay, window.AutoDayCycle, window.GroundMist.Amount, window.GroundMist.Color, window.GroundMist.ReferenceHeight,
            window.Weather.Fog, window.Weather.Cloud, window.Weather.Rain, window.Weather.Snowfall, window.Weather.Warmth, window.WispAmount);

        window.TimeOfDay = band.TimeOfDay;
        window.GroundMist.Amount = band.Mist;
        window.GroundMist.Color = new System.Numerics.Vector3(band.MistColour.R, band.MistColour.G, band.MistColour.B);
        window.GroundMist.ReferenceHeight = BaseHeight + 0.5f;
        window.Weather.Fog = band.Fog;
        window.Weather.Cloud = band.Cloud;
        window.Weather.Rain = band.Rain;
        window.Weather.Snowfall = band.Snow;
        window.Weather.Warmth = band.Warmth;
        window.Weather.Settle();
        window.WispAmount = band.Wisps;
    }

    /// <summary>Camp's sky and weather back, as they were before the run (and warm enough that any snow a run left melts away).</summary>
    private void RestoreCampLook(EngineWindow window)
    {
        if (_campLook is not { } look)
        {
            return;
        }

        window.TimeOfDay = look.TimeOfDay;
        window.AutoDayCycle = look.AutoDayCycle;
        window.GroundMist.Amount = look.Mist;
        window.GroundMist.Color = look.MistColour;
        window.GroundMist.ReferenceHeight = look.MistHeight;
        window.Weather.Fog = look.Fog;
        window.Weather.Cloud = look.Cloud;
        window.Weather.Rain = look.Rain;
        window.Weather.Snowfall = look.Snow;
        window.Weather.Warmth = MathF.Max(look.Warmth, 0.6f);
        window.Weather.Settle();
        window.WispAmount = look.Wisps;
        _campLook = null;
    }
}
