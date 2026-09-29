using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.World;
using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game;

// The Hungering Maw (World/Hunger): every few minutes of a run in the cave, a mouth opens in the floor near the player; kills within its ring feed it. Fed its fill
// in time it spits out a hoard (a burst of experience, silver, tree experience, maybe a chest); starved, it spews up a pack of rare ghouls and a brute. While it
// is open more monsters come. Not in the boss hunt, not while a boss is up, a Monster Rush is on or a mini boss is stirring.
public sealed partial class ArenaMasterContent
{
    private const string MawModel = "hunger_maw.glb";
    private const string MawRingModel = "hunger_ring.glb";
    private const string MawMoteModel = "hunger_mote.glb";
    private const int MawLightId = 7297;

    /// <summary>The maw sits this far up from the ground it opens in (its lip's foot is buried, its gullet just clear of the ground).</summary>
    private const float MawRise = 0.42f;

    /// <summary>What a sated maw spits out: this many gems, silver for the run, tree experience, and a chance of a chest (the world's odds).</summary>
    private const int HoardGems = 24;
    private const int HoardSilver = 40;
    private const int HoardTreeExperience = 50;
    private const float HoardChestChance = 0.4f;

    private Hunger? _hungerEvent;
    private readonly List<CrowdInstance> _mawMotes = new();

    private Hunger HungerEvent => _hungerEvent ??= new Hunger(_random);

    private void BeginHunger() => HungerEvent.Reset();

    /// <summary>A run's frame of the maw: its clocks, the crowd it draws, what it pays or spews, and how it looks.</summary>
    private void UpdateHunger(EngineWindow window, float deltaSeconds)
    {
        if (_plan.Kind == Delve.RunKind.Arena || !_underground || _caveFlow is not { } flow || window.Terrain is not { } terrain)
        {
            return;
        }

        var hunger = HungerEvent;
        var feet = window.PlayerFeet;
        bool allowed = _enemies.Boss is null && !_rush.Active && StirringEvent.Phase != StirringPhase.Stirring;
        switch (hunger.Update(deltaSeconds, _runSeconds, allowed, () => PickMawSpot(terrain, flow, feet)))
        {
            case HungerNews.Opened:
                Announce($"A Hungering Maw opens! Feed it {hunger.Need} kills within its ring");
                break;

            case HungerNews.Sated:
                SpitHoard(hunger.Centre);
                break;

            case HungerNews.Starved:
                Spew(terrain, hunger.Centre);
                break;
        }

        if (hunger.IsOpen)
        {
            _enemies.TargetCount = hunger.Crowd(_enemies.TargetCount);   // more to feed it (the director sets its own number again next frame)
            _enemies.SpawnInterval = MathF.Min(_enemies.SpawnInterval, 0.15f);
        }

        DrawMaw(window);
    }

    /// <summary>Where a maw opens: 14-24 m from the player on open floor (the ground round it open too), a short way along the floor; null if nowhere just now.</summary>
    private Vector3D<float>? PickMawSpot(Terrain terrain, CaveFlow flow, Vector3D<float> feet)
    {
        for (int attempt = 0; attempt < 16; attempt++)
        {
            float angle = (float)_random.NextDouble() * MathF.Tau;
            float distance = 14f + 10f * (float)_random.NextDouble();
            float x = feet.X + MathF.Sin(angle) * distance, z = feet.Z + MathF.Cos(angle) * distance;
            bool open = !CaveLayout.Blocked(x, z) && !CaveLife.NearPool(x, z, 4f) && flow.Distance(x, z) <= 35f;
            for (int k = 0; k < 6 && open; k++)
            {
                float a = MathF.Tau * k / 6f;
                open = !CaveLayout.Blocked(x + MathF.Sin(a) * 5f, z + MathF.Cos(a) * 5f);
            }

            if (open)
            {
                return CaveLayout.Ground(terrain, new Vector2D<float>(x, z));
            }
        }

        return null;
    }

    /// <summary>Sated: a burst of experience gems round it, silver and tree experience for the run, and maybe a chest.</summary>
    private void SpitHoard(Vector3D<float> centre)
    {
        int each = 2 + (int)(_runSeconds / 120f);
        for (int i = 0; i < HoardGems; i++)
        {
            float angle = MathF.Tau * i / HoardGems, reach = 1.5f + 2.5f * (float)_random.NextDouble();
            _gems.Drop(centre + new Vector3D<float>(MathF.Sin(angle) * reach, 0f, MathF.Cos(angle) * reach), each);
        }

        _runSilver += HoardSilver;
        _runTreeExperience += HoardTreeExperience;
        _runTreeLevels += _tree.AddExperience(HoardTreeExperience);
        bool chest = _random.NextDouble() < HoardChestChance;
        if (chest)
        {
            _loot.DropChest(centre, RarityWeights.World);
        }

        Announce($"The Maw is sated! It spits out its hoard: +{HoardSilver} silver, +{HoardTreeExperience} tree experience{(chest ? ", a chest" : "")}");
    }

    /// <summary>Starved: it spews up six rare ghouls and a magic brute round itself.</summary>
    private void Spew(Terrain terrain, Vector3D<float> centre)
    {
        for (int i = 0; i < 7; i++)
        {
            float angle = MathF.Tau * i / 7f;
            var at = CaveLayout.Ground(terrain, new Vector2D<float>(centre.X + MathF.Sin(angle) * 3.5f, centre.Z + MathF.Cos(angle) * 3.5f));
            if (i == 0)
            {
                _enemies.Spawn(at, EnemyKind.Brute, MonsterRarity.Magic);
            }
            else
            {
                _enemies.Spawn(at, _enemies.Kind, MonsterRarity.Rare);
            }
        }

        Announce("The Maw starves, and spews up what it has eaten!");
    }

    /// <summary>The maw, breathing; the ring of its reach, pulsing; the motes flying into it; a red glow over it. Nothing while it is shut.</summary>
    private void DrawMaw(EngineWindow window)
    {
        var hunger = HungerEvent;
        if (!hunger.IsOpen)
        {
            ClearMaw(window);
            return;
        }

        var centre = hunger.Centre;
        float breath = 1f + 0.06f * MathF.Sin(_runSeconds * 3f);
        float full = hunger.Need > 0 ? (float)hunger.Fed / hunger.Need : 0f;
        window.SetCrowd(MawModel, new[] { new CrowdInstance(centre + new Vector3D<float>(0f, MawRise, 0f), _runSeconds * 0.2f, breath * (1.4f + 0.4f * full)) });
        window.SetCrowd(MawRingModel, new[] { new CrowdInstance(centre + new Vector3D<float>(0f, 0.07f, 0f), -_runSeconds * 0.3f, Hunger.Reach,
            Flash: 0.3f + 0.3f * MathF.Abs(MathF.Sin(_runSeconds * 2.5f))) });
        _mawMotes.Clear();
        var mouth = centre + new Vector3D<float>(0f, MawRise + 0.3f, 0f);
        foreach (var (from, through) in hunger.Motes)
        {
            var at = Vector3D.Lerp(from, mouth, through) + new Vector3D<float>(0f, 2.5f * through * (1f - through), 0f);
            _mawMotes.Add(new CrowdInstance(at, through * 6f, 1.2f - 0.6f * through, Flash: 0.6f));
        }

        window.SetCrowd(MawMoteModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_mawMotes));
        window.SetCrowdGlow(MawModel, 0.2f);
        window.SetCrowdGlow(MawRingModel, 0.9f);
        window.SetCrowdGlow(MawMoteModel, 1.5f);
        window.SetPointLight(MawLightId, centre + new Vector3D<float>(0f, 1.5f, 0f), new Vector3D<float>(1.1f, 0.18f, 0.12f) * (0.6f + 0.6f * full), 10f);
    }

    private static void ClearMaw(EngineWindow window)
    {
        window.SetCrowd(MawModel, ReadOnlySpan<CrowdInstance>.Empty);
        window.SetCrowd(MawRingModel, ReadOnlySpan<CrowdInstance>.Empty);
        window.SetCrowd(MawMoteModel, ReadOnlySpan<CrowdInstance>.Empty);
        window.RemovePointLight(MawLightId);
    }

    /// <summary>Under the clock while it is open: how fed it is, and how long it waits.</summary>
    private void DrawHungerHud(IHud hud)
    {
        var hunger = HungerEvent;
        if (!hunger.IsOpen)
        {
            return;
        }

        int seconds = (int)MathF.Ceiling(hunger.Left);
        hud.Text(HudAnchor.TopCenter, new Vector2D<float>(0f, 118f), $"The Hungering Maw  ·  {hunger.Fed} / {hunger.Need} fed  ·  {seconds / 60}:{seconds % 60:00}",
            StirRed, 0.8f);
        hud.Bar(HudAnchor.TopCenter, new Vector2D<float>(0f, 142f), new Vector2D<float>(300f, 8f), (float)hunger.Fed / Math.Max(1, hunger.Need),
            new Vector4D<float>(0.9f, 0.25f, 0.2f, 0.95f), new Vector4D<float>(0f, 0f, 0f, 0.5f));
    }
}
