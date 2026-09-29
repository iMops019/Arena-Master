using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Delve;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

public class FiendTests
{
    private const float Step = 1f / 60f;

    private static readonly EnemyKind Fiend = DelveBosses.Fiend;

    private static float? FlatGround(float x, float z) => MathF.Abs(x) < 200f && MathF.Abs(z) < 200f ? 0f : null;

    private static EnemyField QuietField() => new(new Random(5)) { TargetCount = 0 };

    private static void Tick(EnemyField field, PlayerHealth player, Vector3D<float> feet, bool grounded = true) =>
        field.Update(Step, new PlayerTarget(feet, grounded, player, new PlayerCondition()), FlatGround);

    /// <summary>A Fiend that only ever does <paramref name="attack"/>, and does it at once.</summary>
    private static Enemy OnlyThis(EnemyField field, AttackSpec attack)
    {
        var kind = Fiend with { Attacks = new[] { attack }, Phases = Array.Empty<BossPhase>(), AttackCooldown = 99f };
        var enemy = field.Spawn(Vector3D<float>.Zero, kind);
        enemy.AttackCooldown = 0f;
        return enemy;
    }

    private static void RunThrough(EnemyField field, PlayerHealth player, Vector3D<float> feet, float seconds, bool grounded = true)
    {
        for (int i = 0; i < seconds / Step; i++)
        {
            player.Update(Step);
            Tick(field, player, feet, grounded);
        }
    }

    private static IEnumerable<AttackSpec> Every => Fiend.Attacks.Concat(Fiend.Phases.SelectMany(p => p.Attacks));

    private static AttackSpec Attack(AttackType type) => Every.First(a => a.Type == type);

    private static AttackSpec Triple => Every.First(a => a.Type == AttackType.Shoot && a.Chain == 2);

    private static AttackSpec Volley => Every.First(a => a is { Type: AttackType.Barrage, Pattern: BarragePattern.Scatter });

    [Fact]
    public void HeHasEveryAttackTheUserAskedFor_AndIsATierUpFromTheMarauder()
    {
        // The volley: bolts from the sky round the player. Three shots in a row. Grenades, then a charge onto the player, a slam, a leap back and a shot.
        Assert.Equal("fiend_bolt.glb", Volley.ProjectileModel);
        Assert.Equal(2, Triple.Chain);
        var grenades = Attack(AttackType.Lob);
        Assert.True(grenades.Salvo > 1);
        var chain = new List<AttackType>();
        for (var next = grenades.FollowUp; next is not null; next = next.FollowUp)
        {
            chain.Add(next.Type);
        }

        Assert.Equal(new[] { AttackType.Lunge, AttackType.Stomp, AttackType.Retreat, AttackType.Shoot }, chain);
        Assert.True(grenades.FollowUp!.StopAtPlayer);
        Assert.True(grenades.FollowUp.FollowUp!.WindUp <= 0.3f);   // the slam comes almost at once

        Assert.True(BossHunt.Fiend.Health > BossHunt.Marauder.Health);
        Assert.True(BossHunt.Fiend.Scaling.Damage > BossHunt.Marauder.Scaling.Damage);
        Assert.True(BossHunt.Fiend.StartLevel > BossHunt.Marauder.StartLevel);
        Assert.True(Fiend.StandOff > 0f);   // he keeps his distance
        Assert.Contains(Fiend, EnemyKind.Foes);
    }

    [Fact]
    public void ThreeShots_OneAfterAnother_EachAimedWhereThePlayerIsThen()
    {
        var field = QuietField();
        OnlyThis(field, Triple);
        var player = new PlayerHealth(1_000_000f);
        var shots = new List<EnemyBolt>();
        for (int i = 0; i < 3f / Step; i++)
        {
            // The player walks sideways the whole time.
            var feet = new Vector3D<float>(-6f + i * Step * 5f, 0f, 14f);
            player.Update(Step);
            Tick(field, player, feet);
            shots.AddRange(field.Bolts.Where(b => !shots.Contains(b)));
        }

        Assert.Equal(3, shots.Count);
        Assert.True(shots[1].Velocity.X > shots[0].Velocity.X && shots[2].Velocity.X > shots[1].Velocity.X);   // each after the player as they moved
    }

    [Fact]
    public void TheVolley_RainsBoltsRoundThePlayer_EachLandingMarked_AndStandingStillIsHit()
    {
        var field = QuietField();
        OnlyThis(field, Volley);
        var player = new PlayerHealth(1_000_000f);
        var feet = new Vector3D<float>(0f, 0f, 14f);
        RunThrough(field, player, feet, Volley.WindUp + 0.05f);

        Assert.Equal(Volley.Count, field.Bolts.Count);
        Assert.All(field.Bolts, b =>
        {
            Assert.Equal("fiend_bolt_mark.glb", b.MarkModel);
            Assert.Equal("fiend_bolt_burst.glb", b.BurstModel);
            Assert.True(Vector3D.Distance(new Vector3D<float>(b.Target.X, 0f, b.Target.Z), feet) <= Volley.Reach + 0.01f);
        });

        bool burst = false;
        for (int i = 0; i < 2f / Step; i++)
        {
            player.Update(Step);
            Tick(field, player, feet);
            burst |= field.Blasts.Any(b => b.Model == "fiend_bolt_burst.glb");
        }

        Assert.True(player.Current < player.Max);
        Assert.True(burst);
    }

    [Fact]
    public void TheGrenades_ThenACharge_StoppingOnThePlayer_ASlam_ALeapBack_AndAShot()
    {
        var grenades = Attack(AttackType.Lob);
        var field = QuietField();
        var fiend = OnlyThis(field, grenades);
        var player = new PlayerHealth(1_000_000f);
        var feet = new Vector3D<float>(0f, 0f, 14f);

        var seen = new List<AttackType>();
        float closest = float.MaxValue;
        int bombs = 0, bolts = 0;
        Vector3D<float>? shotFrom = null;
        for (int i = 0; i < 6f / Step; i++)
        {
            player.Update(Step);
            Tick(field, player, feet);
            bombs = Math.Max(bombs, field.Bombs.Count);
            bolts = Math.Max(bolts, field.Bolts.Count);
            if (fiend.Attack is { } attack && (seen.Count == 0 || seen[^1] != attack.Type))
            {
                seen.Add(attack.Type);
                if (attack.Type == AttackType.Shoot)
                {
                    shotFrom = fiend.Position;
                }
            }

            if (fiend.Attack?.Type == AttackType.Stomp)
            {
                closest = MathF.Min(closest, Vector3D.Distance(fiend.Position, feet));
            }
        }

        Assert.Equal(new[] { AttackType.Lob, AttackType.Lunge, AttackType.Stomp, AttackType.Retreat, AttackType.Shoot }, seen);
        Assert.Equal(grenades.Salvo, bombs);                                               // the salvo, all at once
        Assert.InRange(closest, Fiend.Radius, Fiend.Radius + EnemyField.PlayerRadius + 0.1f);   // the charge stopped on the player
        Assert.True(Vector3D.Distance(shotFrom!.Value, Vector3D<float>.Zero) < 0.1f);         // back where he threw from
        Assert.Equal(1, bolts);                                                            // and a shot from there
        Assert.Null(fiend.Attack);
        Assert.True(player.Current < player.Max);
    }

    [Fact]
    public void TheCharge_StopsAtThePlayersEdge_NotItsWholeReach()
    {
        var charge = Attack(AttackType.Lob).FollowUp! with { FollowUp = null };
        var field = QuietField();
        var fiend = OnlyThis(field, charge);
        var player = new PlayerHealth(1_000_000f);
        var feet = new Vector3D<float>(0f, 0f, 8f);
        RunThrough(field, player, feet, charge.WindUp + charge.Active + 0.05f);
        Assert.Equal(8f - Fiend.Radius - EnemyField.PlayerRadius, fiend.Position.Z, 1);
        Assert.True(charge.Reach > 8f);
    }

    [Fact]
    public void TheSlam_HitsInItsCircle_AndNotOutsideIt()
    {
        var stomp = Attack(AttackType.Lob).FollowUp!.FollowUp! with { FollowUp = null };

        var field = QuietField();
        OnlyThis(field, stomp);
        var near = new PlayerHealth(1_000_000f);
        RunThrough(field, near, new Vector3D<float>(0f, 0f, 3f), stomp.WindUp + stomp.Active);
        Assert.True(near.Current < near.Max);

        var dodge = QuietField();
        OnlyThis(dodge, stomp);
        var clear = new PlayerHealth(1_000_000f);
        RunThrough(dodge, clear, new Vector3D<float>(0f, 0f, stomp.Reach + 1f), stomp.WindUp + stomp.Active);
        Assert.Equal(clear.Max, clear.Current);
    }

    [Fact]
    public void TheDeadeye_LocksItsLane_ThenFiresStraightDownIt()
    {
        var snipe = Attack(AttackType.Snipe);
        Assert.True(snipe.WindUp - snipe.Tracking >= 0.3f);   // time to step out once it locks
        var stood = new Vector3D<float>(0f, 0f, 20f);

        var field = QuietField();
        OnlyThis(field, snipe);
        var still = new PlayerHealth(1_000_000f);
        RunThrough(field, still, stood, snipe.WindUp + 0.6f);
        Assert.True(still.Current < still.Max);

        var stepped = QuietField();
        OnlyThis(stepped, snipe);
        var aside = new PlayerHealth(1_000_000f);
        RunThrough(stepped, aside, stood, snipe.Tracking + 0.05f);
        RunThrough(stepped, aside, stood + new Vector3D<float>(2f, 0f, 0f), snipe.WindUp + 0.6f);
        Assert.Equal(aside.Max, aside.Current);
    }

    [Fact]
    public void TheFan_LoosesFiveBolts_SpreadEvenly_TheMiddleOneAtThePlayer()
    {
        var fan = Every.First(a => a.Type == AttackType.Shoot && a.Salvo == 5) with { Chain = 0 };
        var field = QuietField();
        OnlyThis(field, fan);
        var player = new PlayerHealth(1_000_000f);
        RunThrough(field, player, new Vector3D<float>(0f, 0f, 15f), fan.WindUp + 0.02f);

        Assert.Equal(5, field.Bolts.Count);
        var yaws = field.Bolts.Select(b => MathF.Atan2(b.Velocity.X, b.Velocity.Z)).Order().ToList();
        Assert.Equal(0f, yaws[2], 3);
        for (int i = 1; i < yaws.Count; i++)
        {
            Assert.Equal(fan.Spread, yaws[i] - yaws[i - 1], 3);
        }
    }

    [Fact]
    public void TooClose_HeKicksThePlayerAway_LeapsBack_ThenShoots()
    {
        var kick = Attack(AttackType.Swing);
        Assert.True(kick.MaxRange < Fiend.StandOff && kick.Knockback >= 15f);
        Assert.Equal(AttackType.Retreat, kick.FollowUp!.Type);
        Assert.Equal(AttackType.Shoot, kick.FollowUp.FollowUp!.Type);

        var field = QuietField();
        var fiend = OnlyThis(field, kick);
        var player = new PlayerHealth(1_000_000f);
        RunThrough(field, player, new Vector3D<float>(0f, 0f, 2.5f), kick.WindUp + kick.Active + 0.05f);
        Assert.True(player.Current < player.Max);
        Assert.Equal(AttackType.Retreat, fiend.Attack!.Type);
    }

    private static AttackSpec Disengage => Every.First(a => a.Type == AttackType.Retreat && a.Reach > 0f);

    [Fact]
    public void InMeleeRange_HeOnlyKicksOrJumpsAway()
    {
        foreach (var attacks in new[] { Fiend.Attacks }.Concat(Fiend.Phases.Select(p => p.Attacks)))
        {
            var close = attacks.Where(a => a.MinRange <= 3f).Select(a => a.Type).ToHashSet();
            Assert.Equal(new HashSet<AttackType> { AttackType.Swing, AttackType.Retreat }, close);
        }
    }

    [Fact]
    public void InMeleeRange_HeJumpsBackAwayFromThePlayer_ThenShoots()
    {
        var leap = Disengage;
        var field = QuietField();
        var fiend = OnlyThis(field, leap);
        var player = new PlayerHealth(1_000_000f);
        var feet = new Vector3D<float>(0f, 0f, 2f);
        RunThrough(field, player, feet, leap.WindUp + leap.Active + 0.02f);

        Assert.Equal(-leap.Reach, fiend.Position.Z, 1);     // straight back, away from the player
        Assert.Equal(0f, fiend.Position.X, 2);
        Assert.Equal(player.Max, player.Current);           // harmless in itself
        Assert.Equal(AttackType.Shoot, fiend.Attack!.Type);
    }

    [Fact]
    public void AgainstTheArenaWall_TheJumpBackTurnsToStayInside()
    {
        var field = QuietField();
        field.Arena = (Vector2D<float>.Zero, 19f);
        var fiend = OnlyThis(field, Disengage);
        fiend.Position = new Vector3D<float>(0f, 0f, -16f);   // the wall just behind him
        var feet = new Vector3D<float>(0f, 0f, -13f);

        var landing = field.LeapAway(fiend, Disengage.Reach, feet);
        Assert.True(new Vector2D<float>(landing.X, landing.Z).Length <= 19f + 1e-3f);
        Assert.True(Vector3D.Distance(landing, feet) > Vector3D.Distance(fiend.Position, feet) + 3f);   // still well away from the player

        fiend.Position = new Vector3D<float>(0f, 0f, 0f);
        Assert.Equal(new Vector3D<float>(0f, 0f, -Disengage.Reach), field.LeapAway(fiend, Disengage.Reach, new Vector3D<float>(0f, 0f, 2f)));   // room: straight back
    }

    [Fact]
    public void TheGrenades_LandSpreadOutRoundThePlayer()
    {
        var grenades = Attack(AttackType.Lob) with { FollowUp = null };
        var field = QuietField();
        OnlyThis(field, grenades);
        var player = new PlayerHealth(1_000_000f);
        var feet = new Vector3D<float>(0f, 0f, 14f);
        var landed = new Dictionary<EnemyBomb, Vector3D<float>>();
        var bounced = new HashSet<EnemyBomb>();
        for (int i = 0; i < 3f / Step; i++)
        {
            player.Update(Step);
            Tick(field, player, feet);
            foreach (var bomb in field.Bombs.Where(b => !bounced.Contains(b)))
            {
                landed[bomb] = bomb.Position;   // where each first came down (or went off on the player before it could)
                if (bomb.BouncesLeft < grenades.Count)
                {
                    bounced.Add(bomb);
                }
            }
        }

        Assert.Equal(grenades.Salvo, landed.Count);
        var spots = landed.Values.ToList();
        float widest = spots.SelectMany(a => spots.Select(b => Vector3D.Distance(a, b))).Max();
        Assert.True(widest > 9f, $"the salvo came down only {widest:0.0} m across");
        Assert.All(spots.Skip(1).Zip(spots), p => Assert.True(Vector3D.Distance(p.First, p.Second) > 2f));
    }

    [Fact]
    public void HisStages_TakeAim_ThenCornered_QuickerAndHarder_WithoutARoar()
    {
        var field = QuietField();
        var fiend = field.Spawn(Vector3D<float>.Zero, Fiend);
        var player = new PlayerHealth(1_000_000f);
        var far = new Vector3D<float>(0f, 0f, 60f);
        float damage = fiend.DamageScale, speed = fiend.AttackSpeed;

        fiend.Health = fiend.MaxHealth * 0.6f;
        Tick(field, player, far);
        Assert.Contains(field.TakePhaseChanges(), c => c.Phase == Fiend.Phases[0]);
        Assert.Contains(fiend.AttacksNow, a => a.Type == AttackType.Snipe);

        fiend.Health = fiend.MaxHealth * 0.25f;
        Tick(field, player, far);
        Assert.False(fiend.IsRoaring);
        Assert.Equal(damage * 1.1f, fiend.DamageScale, 4);
        Assert.Equal(speed * 1.2f, fiend.AttackSpeed, 4);
        Assert.Contains(fiend.AttacksNow, a => a.Pattern == BarragePattern.Ring);   // the cage of bolts
    }

    [Fact]
    public void TheFiend_IsLockedUntilTheMarauderIsSlain_AndPaysTheMostGear()
    {
        var save = new DelveSave();
        BossHunt.CountKill(save, BossHunt.HollowKing);
        Assert.False(BossHunt.IsOpen(save, BossHunt.Fiend));
        Assert.Equal("Locked: slay the Marauder Unbound first.", BossHunt.WhyLocked(save, BossHunt.Fiend));
        Assert.Equal("Locked: slay the Hollow King Unbound first.", BossHunt.WhyLocked(new DelveSave(), BossHunt.Marauder));

        BossHunt.CountKill(save, BossHunt.Marauder);
        Assert.True(BossHunt.IsOpen(save, BossHunt.Fiend));
        Assert.Null(BossHunt.WhyLocked(save, BossHunt.Fiend));

        BossHunt.CountKill(save, BossHunt.Fiend);
        Assert.Equal(1, save.FiendsSlain);
        Assert.Equal(1, save.MaraudersSlain);
        Assert.Equal(1, BossHunt.Slain(save, BossHunt.Fiend));

        Assert.Equal(0.45f, BossHunt.Fiend.GearChance);
        Assert.True(BossHunt.All.Zip(BossHunt.All.Skip(1)).All(p => p.Second.GearChance > p.First.GearChance && p.Second.Silver > p.First.Silver));
        Assert.Contains("Fiend", RunPlan.Hunt(BossHunt.Fiend).Title);
    }
}
