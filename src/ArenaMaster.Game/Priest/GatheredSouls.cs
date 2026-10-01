using ArenaMaster.Game.Combat;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Priest;

/// <summary>A soul a kill left on the ground (Soul Siphon), waiting to be gathered, fading in time.</summary>
internal sealed class Soul
{
    public Vector3D<float> Position { get; init; }

    public float Age { get; set; }

    public float Life { get; init; }
}

/// <summary>A gathered soul flying out at an enemy (Vengeful Spirits): it turns onto its enemy and hurts it on reaching it, or fades.</summary>
internal sealed class SoulSpirit
{
    public Vector3D<float> Position { get; set; }

    public Enemy? Target { get; set; }

    public float Damage { get; init; }

    public float Age { get; set; }
}

/// <summary>
/// The Grave Calling tree's souls: Soul Siphon (every kill leaves a soul; walking over one heals and adds a charge, and the next cast spends the charges for more
/// damage), Soul Well (a cast spends only a few), Vengeful Spirits (a gathered soul flies out at an enemy) and Lich Form (at a full count of charges, a spell of
/// three skulls a cast, spending nothing). Pure - no engine calls; <see cref="GraveView"/> draws it.
/// </summary>
internal sealed class GatheredSouls
{
    /// <summary>How near a spirit gets to its enemy's middle to strike it, and how high it flies.</summary>
    public const float SpiritStrike = 0.35f;
    public const float SpiritHeight = 1f;

    /// <summary>How far round a spirit looks for another enemy when its own dies first.</summary>
    public const float SpiritRetarget = 6f;

    private readonly List<Soul> _souls = new();
    private readonly List<SoulSpirit> _spirits = new();

    public IReadOnlyList<Soul> Souls => _souls;

    public IReadOnlyList<SoulSpirit> Spirits => _spirits;

    /// <summary>The charges gathered, spent by the next cast.</summary>
    public int Charges { get; private set; }

    /// <summary>Seconds left as a Lich (Lich Form), 0 when not one.</summary>
    public float LichLeft { get; private set; }

    public bool IsLich => LichLeft > 0f;

    /// <summary>Where souls were gathered during the last <see cref="Update"/> (for the view's flashes).</summary>
    public List<Vector3D<float>> Gathered { get; } = new();

    /// <summary>The HUD's gauge: the charges, or the Lich's time running down; null without Soul Siphon.</summary>
    public (string Label, float Fill)? Meter(PriestStats stats) =>
        !stats.Grave.SoulSiphon ? null
        : IsLich ? ("LICH FORM", LichLeft / PriestStats.LichSeconds)
        : ($"SOULS {Charges} / {PriestStats.MaxCharges}", Charges / (float)PriestStats.MaxCharges);

    /// <summary>A kill at <paramref name="at"/>: with Soul Siphon it leaves a soul (the oldest goes, past the most there can be).</summary>
    public void OnKill(Vector3D<float> at, PriestStats stats)
    {
        if (!stats.Grave.SoulSiphon)
        {
            return;
        }

        if (_souls.Count >= PriestStats.MaxSouls)
        {
            _souls.RemoveAt(0);
        }

        _souls.Add(new Soul { Position = at, Life = stats.SoulLife });
    }

    /// <summary>
    /// What the cast going now is raised by, spending what it spends: as a Lich, three times the skulls and nothing spent; at a full count with Lich Form, the
    /// Lich begins (nothing spent); with Lich Form short of a full count, nothing (the charges are saved up for the Lich); otherwise every charge (at most
    /// <see cref="PriestStats.SoulWellSpend"/> with Soul Well) spent for more damage.
    /// </summary>
    public (float Damage, int Volleys) Empower(PriestStats stats)
    {
        if (!stats.Grave.SoulSiphon)
        {
            return (1f, 1);
        }

        if (IsLich)
        {
            return (1f, PriestStats.LichVolleys);
        }

        if (stats.Grave.LichForm && Charges >= PriestStats.MaxCharges)
        {
            LichLeft = PriestStats.LichSeconds;
            return (1f, PriestStats.LichVolleys);
        }

        if (stats.Grave.LichForm)
        {
            return (1f, 1);   // saved up: casting one at a time would never let the count fill between two casts
        }

        int spent = stats.Grave.SoulWell ? Math.Min(Charges, PriestStats.SoulWellSpend) : Charges;
        Charges -= spent;
        return (1f + spent * stats.ChargeDamage, 1);
    }

    /// <summary>
    /// One frame: the souls fading, those within reach of <paramref name="feet"/> gathered (healing <paramref name="health"/>, a charge each, and a spirit each with
    /// Vengeful Spirits), the spirits flying, and the Lich's time running down (the charges start again from nothing when it ends).
    /// </summary>
    public void Update(float deltaSeconds, Vector3D<float> feet, PriestStats stats, PlayerHealth health, EnemyField enemies, PlagueSkulls skulls, List<PriestHit> hits)
    {
        Gathered.Clear();
        if (LichLeft > 0f)
        {
            LichLeft = MathF.Max(0f, LichLeft - deltaSeconds);
            if (LichLeft <= 0f)
            {
                Charges = 0;
            }
        }

        float reach = stats.SoulReach;
        for (int i = _souls.Count - 1; i >= 0; i--)
        {
            var soul = _souls[i];
            soul.Age += deltaSeconds;
            if (soul.Age >= soul.Life)
            {
                _souls.RemoveAt(i);
                continue;
            }

            Geometry.FlatDirection(feet, soul.Position, out float distance);
            if (distance > reach)
            {
                continue;
            }

            _souls.RemoveAt(i);
            Gathered.Add(soul.Position);
            health.Heal(health.Max * stats.SoulHeal);
            Charges = Math.Min(PriestStats.MaxCharges, Charges + 1);
            if (stats.Grave.VengefulSpirits && NearestFoe(enemies, feet, PriestStats.SpiritRange) is { } foe)
            {
                _spirits.Add(new SoulSpirit
                {
                    Position = new Vector3D<float>(feet.X, feet.Y + SpiritHeight, feet.Z), Target = foe, Damage = PriestStats.SpiritShare * stats.SkullDamage,
                });
            }
        }

        MoveSpirits(deltaSeconds, stats, enemies, skulls, hits);
    }

    /// <summary>Everything out of the world (a run's end): no souls, no charges, no Lich.</summary>
    public void Reset()
    {
        _souls.Clear();
        _spirits.Clear();
        Gathered.Clear();
        Charges = 0;
        LichLeft = 0f;
    }

    private void MoveSpirits(float deltaSeconds, PriestStats stats, EnemyField enemies, PlagueSkulls skulls, List<PriestHit> hits)
    {
        for (int i = _spirits.Count - 1; i >= 0; i--)
        {
            var spirit = _spirits[i];
            spirit.Age += deltaSeconds;
            if (spirit.Target is not { IsAlive: true })
            {
                spirit.Target = NearestFoe(enemies, spirit.Position, SpiritRetarget);
            }

            if (spirit.Age >= PriestStats.SpiritLife || spirit.Target is not { } target)
            {
                _spirits.RemoveAt(i);
                continue;
            }

            var aim = target.Position + new Vector3D<float>(0f, target.Kind.Height * 0.6f, 0f);
            var toward = aim - spirit.Position;
            float distance = toward.Length;
            float step = PriestStats.SpiritSpeed * deltaSeconds;
            if (distance <= step + target.Kind.Radius + SpiritStrike)
            {
                skulls.Hurt(target, spirit.Damage, crit: false, PriestSource.Spirit, stats, enemies, hits);
                _spirits.RemoveAt(i);
                continue;
            }

            spirit.Position += toward / distance * step;
        }
    }

    /// <summary>The nearest enemy (never a crate) within <paramref name="range"/> of <paramref name="from"/>.</summary>
    private static Enemy? NearestFoe(EnemyField enemies, Vector3D<float> from, float range)
    {
        Enemy? best = null;
        float bestDistance = float.MaxValue;
        foreach (var enemy in enemies.Within(from, range))
        {
            if (enemy.Kind.IsProp)
            {
                continue;
            }

            Geometry.FlatDirection(from, enemy.Position, out float distance);
            if (distance < bestDistance)
            {
                best = enemy;
                bestDistance = distance;
            }
        }

        return best;
    }
}
