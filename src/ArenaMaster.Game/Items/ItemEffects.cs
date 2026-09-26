using ArenaMaster.Game.Combat;
using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Items;

/// <summary>A hit an item dealt this frame (thorns, a blow paid back, the Aegis burst), for the damage numbers.</summary>
internal readonly record struct ItemHit(Enemy Enemy, Vector3D<float> Position, float Damage, bool Killed);

/// <summary>
/// The items that act on their own during a run, whichever class carries them: thorns on whatever touches the player, the Warding Crystal's ward, Bloodstone's
/// life from damage dealt, Berserker's Band keeping up with the health missing, Martyr's Crown paying blows back, Aegis of the Dawn's burst on a block, and the
/// Phoenix Feather bringing the player back. Pure - no engine calls - so it can be tested; <see cref="ItemEffectsView"/> draws the Aegis bursts.
/// </summary>
internal sealed class ItemEffects
{
    /// <summary>Thorns strike this often, and reach this far past touching.</summary>
    public const float ThornsInterval = 0.5f;
    public const float ThornsReach = 0.4f;

    /// <summary>The ward forms this often (the first this soon into a run) and holds this long.</summary>
    public const float WardInterval = 15f;
    public const float FirstWard = 3f;
    public const float WardSeconds = 5f;

    /// <summary>How long an item's chill lasts on a hit.</summary>
    public const float ChillSeconds = 1f;

    /// <summary>How long the Staff of the Long Night's freeze lasts.</summary>
    public const float FreezeSeconds = 1f;

    /// <summary>The Aegis burst's reach, and how long its light takes to spread (for the view).</summary>
    public const float BurstRadius = 3f;
    public const float BurstSeconds = 0.3f;

    /// <summary>The Phoenix Feather brings the player back with this share of max health.</summary>
    public const float PhoenixHeal = 0.5f;

    private readonly List<(Vector3D<float> Centre, float Age)> _bursts = new();
    private float _thornsIn;
    private float _wardIn = FirstWard;
    private float _wardLeft;
    private float _wardHeld;
    private float _dealtSeen;

    /// <summary>The Aegis bursts spreading right now, and how far along each is.</summary>
    public IReadOnlyList<(Vector3D<float> Centre, float Age)> Bursts => _bursts;

    /// <summary>Whether the item ward is holding.</summary>
    public bool WardUp => _wardLeft > 0f;

    /// <summary>What <paramref name="items"/> do to every hit the player lands, for <see cref="EnemyField.HitEffects"/>.</summary>
    public static HitEffects HitEffectsOf(ItemBonuses items) => new(
        ChilledMultiplier: items.ChilledDamage,
        FrozenMultiplier: items.FrozenDamage,
        EliteMultiplier: items.EliteDamage,
        ChillSlow: items.ChillOnHit,
        ChillSeconds: ChillSeconds,
        FreezeChance: items.FreezeChance,
        FreezeSeconds: FreezeSeconds);

    /// <summary>A new run: every clock back to the start.</summary>
    public void Begin()
    {
        _bursts.Clear();
        _thornsIn = 0f;
        _wardIn = FirstWard;
        _wardLeft = 0f;
        _wardHeld = 0f;
        _dealtSeen = 0f;
    }

    /// <summary>
    /// One frame, before the enemies strike: thorns, the ward (unless <paramref name="classKeepsBarrier"/> - a Mage with the Frost Shield gets a stronger shield
    /// instead), health from the damage dealt since last frame, and Berserk's share of missing health.
    /// </summary>
    public void Update(float deltaSeconds, Vector3D<float> feet, ItemBonuses items, EnemyField enemies, PlayerHealth health, bool classKeepsBarrier, List<ItemHit> hits)
    {
        items.MissingHealth = health.Max > 0f ? Math.Clamp(1f - health.Current / health.Max, 0f, 1f) : 0f;

        float dealt = enemies.DamageDealt;
        if (dealt > _dealtSeen)
        {
            health.Heal((dealt - _dealtSeen) * items.LifePerDamage);
        }

        _dealtSeen = dealt;

        if (items.Thorns > 0f)
        {
            _thornsIn -= deltaSeconds;
            if (_thornsIn <= 0f)
            {
                _thornsIn = MathF.Max(0f, _thornsIn + ThornsInterval);
                foreach (var enemy in enemies.Within(feet, EnemyField.PlayerRadius + ThornsReach))
                {
                    Hurt(enemy, items.Thorns * items.DamageMultiplier, enemies, hits);
                }
            }
        }

        UpdateWard(deltaSeconds, items, health, classKeepsBarrier);

        for (int i = _bursts.Count - 1; i >= 0; i--)
        {
            var burst = _bursts[i];
            burst.Age += deltaSeconds;
            if (burst.Age >= BurstSeconds)
            {
                _bursts.RemoveAt(i);
            }
            else
            {
                _bursts[i] = burst;
            }
        }
    }

    /// <summary>
    /// After the enemies' blows this frame, and after the class has answered them: Martyr's Crown pays each blow back and heals, Aegis of the Dawn bursts on each
    /// block, and a last stand the class didn't take for itself is the Phoenix Feather's.
    /// </summary>
    public void Answer(IReadOnlyList<Strike> strikes, Vector3D<float> feet, ItemBonuses items, EnemyField enemies, PlayerHealth health, List<ItemHit> hits)
    {
        foreach (var strike in strikes)
        {
            if (items.Retaliation > 0f)
            {
                Hurt(strike.Attacker, strike.Damage * items.Retaliation, enemies, hits);
                health.Heal(health.Max * items.HealPerBlow);
            }

            if (strike.Blocked && items.BlockBurst > 0f)
            {
                _bursts.Add((feet, 0f));
                foreach (var enemy in enemies.Within(feet, BurstRadius))
                {
                    Hurt(enemy, items.BlockBurst * items.DamageMultiplier, enemies, hits);
                }
            }
        }

        if (health.TakeLastStand())
        {
            health.Heal(health.Max * PhoenixHeal);   // the Phoenix Feather
        }
    }

    /// <summary>The Warding Crystal: a barrier of <see cref="ItemBonuses.Ward"/> every so often, taken away again (what is left of it) when its time is up.</summary>
    private void UpdateWard(float deltaSeconds, ItemBonuses items, PlayerHealth health, bool classKeepsBarrier)
    {
        if (items.Ward <= 0f || classKeepsBarrier)
        {
            return;
        }

        if (_wardLeft > 0f)
        {
            _wardLeft -= deltaSeconds;
            if (_wardLeft <= 0f)
            {
                health.Barrier = MathF.Max(0f, health.Barrier - _wardHeld);
                _wardHeld = 0f;
                _wardIn = WardInterval;
            }

            return;
        }

        _wardIn -= deltaSeconds;
        if (_wardIn <= 0f)
        {
            health.Barrier += items.Ward;
            _wardHeld = items.Ward;
            _wardLeft = WardSeconds;
        }
    }

    private static void Hurt(Enemy enemy, float amount, EnemyField enemies, List<ItemHit> hits)
    {
        if (!enemy.IsAlive || amount <= 0f)
        {
            return;
        }

        bool killed = enemies.Damage(enemy, amount);
        hits.Add(new ItemHit(enemy, enemy.Position + new Vector3D<float>(0f, enemy.Kind.Height * 0.7f, 0f), amount, killed));
    }
}

/// <summary>Draws the Aegis of the Dawn's bursts: a pale gold ring spreading over the ground, one engine crowd.</summary>
internal sealed class ItemEffectsView
{
    public const string BurstModel = "aegis_burst.glb";

    private readonly List<CrowdInstance> _bursts = new();

    public void Sync(EngineWindow window, ItemEffects effects)
    {
        _bursts.Clear();
        foreach (var (centre, age) in effects.Bursts)
        {
            float t = Math.Clamp(age / ItemEffects.BurstSeconds, 0f, 1f);
            _bursts.Add(new CrowdInstance(centre + new Vector3D<float>(0f, 0.2f, 0f), 0f, ItemEffects.BurstRadius * (0.3f + 0.7f * t), Flash: 1f - t));
        }

        window.SetCrowd(BurstModel, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_bursts));
    }

    public void Clear(EngineWindow window) => window.SetCrowd(BurstModel, ReadOnlySpan<CrowdInstance>.Empty);
}
