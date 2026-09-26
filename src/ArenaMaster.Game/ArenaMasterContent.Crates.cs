using ArenaMaster.Game.Combat;
using ArenaMaster.Game.World;
using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game;

// Crates on a run: they turn up around the player (World/Crates), any attack breaks one, and what it leaves is taken by walking over it - a magnet that pulls in
// every gem, food that heals, a pouch of silver for the run's reward, a bomb that blasts everything near, a frenzy potion, or a big gem.
public sealed partial class ArenaMasterContent
{
    /// <summary>What the pickups do: heals as shares of max health, the pouch's silver, the bomb's reach and damage (before the director's health scaling), the
    /// frenzy's attack speed and length, and a big gem's worth (plus one per minute survived).</summary>
    private const float AppleHeal = 0.15f;
    private const float RoastHeal = 0.4f;
    private const int SilverPouch = 25;
    private const float BombRadius = 8f;
    private const float BombDamage = 60f;
    private const float FrenzyBonus = 0.4f;
    private const float FrenzySeconds = 10f;
    private const int BigGemValue = 5;

    private readonly CrateField _crates;
    private readonly CrateView _crateView = new();

    /// <summary>Silver picked up from crates this run, added to the run's reward at its end.</summary>
    private int _runSilver;
    private float _frenzyLeft;

    private void BeginCrates()
    {
        _crates.Clear();
        _runSilver = 0;
        _frenzyLeft = 0f;
        _items.Carried.Bonuses.Frenzy = 0f;
    }

    /// <summary>One frame: crates turning up, pickups taken where the player stands, the frenzy running down.</summary>
    private void UpdateCrates(EngineWindow window, float deltaSeconds, Func<float, float, float?> groundAt)
    {
        _crates.Update(deltaSeconds, window.PlayerFeet, _enemies, groundAt);
        foreach (var kind in _crates.Collect(window.PlayerFeet))
        {
            UsePickup(kind, window.PlayerFeet);
        }

        _frenzyLeft = MathF.Max(0f, _frenzyLeft - deltaSeconds);
        _items.Carried.Bonuses.Frenzy = _frenzyLeft > 0f ? FrenzyBonus : 0f;
        _crateView.Sync(window, _crates, deltaSeconds);
    }

    /// <summary>A crate broke: what it leaves goes on the ground - or, for a big gem, experience worth more the longer the run has gone.</summary>
    private void OnCrateBroken(Enemy crate)
    {
        if (_crates.Break(crate.Position) == PickupKind.BigGem)
        {
            _gems.Drop(crate.Position, BigGemValue + (int)(_runSeconds / 60f));
        }
    }

    private void UsePickup(PickupKind kind, Vector3D<float> feet)
    {
        switch (kind)
        {
            case PickupKind.Magnet:
                _gems.AttractAll();
                Announce("Magnet! Every gem comes to you");
                break;
            case PickupKind.Apple:
                _health.Heal(_health.Max * AppleHeal);
                break;
            case PickupKind.Roast:
                _health.Heal(_health.Max * RoastHeal);
                Announce("A hearty roast");
                break;
            case PickupKind.Silver:
                _runSilver += SilverPouch;
                Announce($"+{SilverPouch} silver");
                break;
            case PickupKind.Bomb:
                float damage = BombDamage * _enemies.Scaling.Health;
                foreach (var enemy in _enemies.Within(feet, BombRadius))
                {
                    bool killed = _enemies.Damage(enemy, damage);
                    _numbers.Add(enemy.Position + new Vector3D<float>(0f, enemy.Kind.Height * 0.7f, 0f), damage, killed);
                }

                _crates.AddBlast(feet, BombRadius);
                Announce("Boom!");
                break;
            case PickupKind.Frenzy:
                _frenzyLeft = FrenzySeconds;
                Announce("Frenzy! Attacks come faster");
                break;
        }
    }

    private void ClearCrates(EngineWindow window)
    {
        _crates.Clear();
        _crateView.Clear(window);
        _frenzyLeft = 0f;
        _items.Carried.Bonuses.Frenzy = 0f;
    }
}
