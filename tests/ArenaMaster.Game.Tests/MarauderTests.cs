using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Delve;
using ArenaMaster.Game.Progression;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

public class MarauderTests
{
    private const float Step = 1f / 60f;

    private static readonly EnemyKind Marauder = DelveBosses.MarauderUnbound;

    private static float? FlatGround(float x, float z) => MathF.Abs(x) < 200f && MathF.Abs(z) < 200f ? 0f : null;

    private static EnemyField QuietField() => new(new Random(3)) { TargetCount = 0 };

    private static void Tick(EnemyField field, PlayerHealth player, Vector3D<float> feet, bool grounded = true) =>
        field.Update(Step, new PlayerTarget(feet, grounded, player, new PlayerCondition()), FlatGround);

    /// <summary>An enemy that only ever does <paramref name="attack"/>, and does it at once.</summary>
    private static Enemy OnlyThis(EnemyField field, AttackSpec attack)
    {
        var kind = Marauder with { Attacks = new[] { attack }, Phases = Array.Empty<BossPhase>(), AttackCooldown = 99f };
        var enemy = field.Spawn(Vector3D<float>.Zero, kind);
        enemy.AttackCooldown = 0f;
        return enemy;
    }

    /// <summary>Runs until the attack has gone through its wind-up and blow (or <paramref name="seconds"/> pass).</summary>
    private static void RunThrough(EnemyField field, PlayerHealth player, Vector3D<float> feet, float seconds, bool grounded = true)
    {
        for (int i = 0; i < seconds / Step; i++)
        {
            player.Update(Step);
            Tick(field, player, feet, grounded);
        }
    }

    private static AttackSpec Attack(AttackType type) =>
        Marauder.Attacks.Concat(Marauder.Phases.SelectMany(p => p.Attacks)).First(a => a.Type == type);

    [Fact]
    public void HeHasEveryAttackTheUserAskedFor_AndIsATierUpFromTheKing()
    {
        var types = Marauder.Attacks.Concat(Marauder.Phases.SelectMany(p => p.Attacks)).Select(a => a.Type).ToHashSet();
        Assert.Superset(new HashSet<AttackType> { AttackType.Swing, AttackType.Cleave, AttackType.LeapSlam, AttackType.LineSlam, AttackType.Whirlwind }, types);

        var king = DelveBosses.HollowKingUnbound;
        Assert.True(BossHunt.Marauder.Health > BossHunt.HollowKing.Health);
        Assert.True(BossHunt.Marauder.Scaling.Damage > BossHunt.HollowKing.Scaling.Damage);
        Assert.True(Marauder.AttackCooldown < king.AttackCooldown);
        float Quickest(EnemyKind kind) => kind.Attacks.Concat(kind.Phases.SelectMany(p => p.Attacks)).Min(a => a.WindUp);
        Assert.True(Quickest(Marauder) < Quickest(king));
        Assert.True(Attack(AttackType.Cleave).WindUp < 0.5f);   // quick: time it and dash
    }

    [Fact]
    public void ASwing_HitsInItsWedge_AndNotBehindIt()
    {
        var swing = Attack(AttackType.Swing) with { Chain = 0 };

        var field = QuietField();
        OnlyThis(field, swing);
        var inFront = new PlayerHealth(1_000_000f);
        RunThrough(field, inFront, new Vector3D<float>(0f, 0f, 4f), swing.WindUp + swing.Active + 0.1f);
        Assert.True(inFront.Current < inFront.Max);

        // Stepping behind it once the wedge has locked: the blow passes by.
        var dodger = QuietField();
        OnlyThis(dodger, swing);
        var behind = new PlayerHealth(1_000_000f);
        RunThrough(dodger, behind, new Vector3D<float>(0f, 0f, 4f), swing.Tracking + 0.05f);
        RunThrough(dodger, behind, new Vector3D<float>(0f, 0f, -4.5f), swing.WindUp + swing.Active);
        Assert.Equal(behind.Max, behind.Current);
    }

    [Fact]
    public void TheCleave_LocksAlmostAtOnce_SoADashAsideAvoidsIt()
    {
        var cleave = Attack(AttackType.Cleave);
        Assert.True(cleave.Tracking <= 0.15f);
        Assert.True(cleave.HitWidth > Attack(AttackType.Swing).HitWidth);   // wider than a swing

        var field = QuietField();
        OnlyThis(field, cleave);
        var player = new PlayerHealth(1_000_000f);
        RunThrough(field, player, new Vector3D<float>(0f, 0f, 5f), cleave.Tracking + 0.05f);
        RunThrough(field, player, new Vector3D<float>(0f, 0f, cleave.Reach + 2f), cleave.WindUp + cleave.Active);   // dashed out of reach
        Assert.Equal(player.Max, player.Current);
    }

    [Fact]
    public void TheJumpSlam_LandsWhereThePlayerStood_InABiggerCircle()
    {
        var slam = Attack(AttackType.LeapSlam);
        Assert.Equal(9f, slam.Reach);   // bigger than the 7 m slam it replaced
        Assert.True(slam.LeapHeight > 0f);

        var field = QuietField();
        var marauder = OnlyThis(field, slam);
        var stood = new Vector3D<float>(0f, 0f, 12f);
        var player = new PlayerHealth(1_000_000f);
        RunThrough(field, player, stood, slam.WindUp + slam.Active + 0.1f);
        Assert.True(player.Current < player.Max);
        Assert.True(Vector3D.Distance(marauder.Position, stood) < 0.5f);   // he came down on the spot

        // Out of the circle before he lands: nothing.
        var dodged = QuietField();
        OnlyThis(dodged, slam);
        var runner = new PlayerHealth(1_000_000f);
        RunThrough(dodged, runner, stood, 0.1f);
        RunThrough(dodged, runner, stood + new Vector3D<float>(slam.Reach + 1.5f, 0f, 0f), slam.WindUp + slam.Active);
        Assert.Equal(runner.Max, runner.Current);
    }

    [Fact]
    public void TheJumpSlam_IsFollowedAtOnce_ByThreeRiftsFannedAtThePlayer()
    {
        var slam = Attack(AttackType.LeapSlam);
        var rifts = slam.FollowUp!;
        Assert.Equal(AttackType.LineSlam, rifts.Type);
        Assert.Equal(3, rifts.Count);
        Assert.True(rifts.WindUp < 0.5f);   // quick off the landing

        // Out of the circle, but in line with him: the landing misses, the rifts don't.
        var field = QuietField();
        var marauder = OnlyThis(field, slam);
        var player = new PlayerHealth(1_000_000f);
        var stood = new Vector3D<float>(0f, 0f, 10f);
        RunThrough(field, player, stood, 0.1f);
        var clear = stood + new Vector3D<float>(0f, 0f, slam.Reach + 3f);   // straight on past the circle
        RunThrough(field, player, clear, slam.WindUp + slam.Active);   // just past the landing
        Assert.Equal(player.Max, player.Current);
        Assert.Equal(AttackType.LineSlam, marauder.Attack!.Type);   // no recovery: straight into the rifts
        RunThrough(field, player, clear, rifts.WindUp + rifts.Active);
        Assert.True(player.Current < player.Max);
    }

    [Fact]
    public void TheRiftsFan_SoASidestepFromTheMiddleOneCanLandInTheNext()
    {
        var rifts = Attack(AttackType.LeapSlam).FollowUp!;
        var enemy = new Enemy(1, Marauder, Vector3D<float>.Zero, EnemyScaling.None)
        {
            AttackOrigin = Vector3D<float>.Zero, AttackTarget = new Vector3D<float>(0f, 0f, 10f),
        };
        var lanes = EnemyField.RiftLanes(enemy, rifts);
        Assert.Equal(3, lanes.Count);
        Assert.Contains(lanes, l => MathF.Abs(l.X) < 1e-4f);                  // one straight at the player
        Assert.Equal(rifts.Spread, MathF.Acos(Vector3D.Dot(lanes[0], lanes[1])), 3);
        Assert.Single(EnemyField.RiftLanes(enemy, Attack(AttackType.LineSlam) with { Count = 0 }));   // a plain rift is one lane
    }

    [Fact]
    public void TheWhirlwind_ChasesFasterThanHeWalks()
    {
        var spin = Attack(AttackType.Whirlwind);
        Assert.Equal(1.35f, spin.ProjectileSpeed);
        Assert.Equal(4f, Marauder.Speed);

        var field = QuietField();
        var marauder = OnlyThis(field, spin);
        var far = new Vector3D<float>(0f, 0f, 22f);   // within its range (and still far enough to chase for a second)
        var player = new PlayerHealth(1_000_000f);
        RunThrough(field, player, far, spin.WindUp + 0.02f);
        float from = marauder.Position.Z;
        RunThrough(field, player, far, 1f);
        Assert.Equal(Marauder.Speed * 1.35f, marauder.Position.Z - from, 1);
    }

    [Fact]
    public void TheRift_HitsWhoIsInItsLane_NotWhoStepsOutOfIt_NorWhoJumpsIt()
    {
        var rift = Attack(AttackType.LineSlam) with { Chain = 0 };

        var field = QuietField();
        OnlyThis(field, rift);
        var inLane = new PlayerHealth(1_000_000f);
        RunThrough(field, inLane, new Vector3D<float>(0f, 0f, 14f), rift.WindUp + rift.Active + 0.1f);
        Assert.True(inLane.Current < inLane.Max);

        var stepped = QuietField();
        OnlyThis(stepped, rift);
        var aside = new PlayerHealth(1_000_000f);
        RunThrough(stepped, aside, new Vector3D<float>(0f, 0f, 14f), rift.Tracking + 0.05f);
        RunThrough(stepped, aside, new Vector3D<float>(rift.HitWidth + 1.5f, 0f, 14f), rift.WindUp + rift.Active);
        Assert.Equal(aside.Max, aside.Current);

        var jumped = QuietField();
        OnlyThis(jumped, rift);
        var air = new PlayerHealth(1_000_000f);
        RunThrough(jumped, air, new Vector3D<float>(0f, 0f, 14f), rift.WindUp + rift.Active + 0.1f, grounded: false);
        Assert.Equal(air.Max, air.Current);
    }

    [Fact]
    public void TheWhirlwind_ChasesThePlayer_HittingAgainAndAgain_ThenHeIsDizzy()
    {
        var spin = Attack(AttackType.Whirlwind);
        var field = QuietField();
        var marauder = OnlyThis(field, spin);
        var player = new PlayerHealth(1_000_000f);
        var feet = new Vector3D<float>(0f, 0f, 12f);
        RunThrough(field, player, feet, spin.WindUp + spin.Active * 0.9f);

        Assert.True(Vector3D.Distance(marauder.Position, feet) < 12f - 5f);   // it came after the player
        Assert.True(player.Current < player.Max - spin.Damage * BossHunt.HollowKing.Scaling.Damage);   // more than one hit landed
        RunThrough(field, player, new Vector3D<float>(0f, 0f, 60f), spin.Active * 0.2f);
        Assert.Equal(AttackPhase.Recover, marauder.AttackPhase);
    }

    [Fact]
    public void At35Percent_HeRoars_ImmuneAndStill_ThenFliesIntoAFrenziedRage()
    {
        var field = QuietField();
        var marauder = field.Spawn(Vector3D<float>.Zero, Marauder);
        var player = new PlayerHealth(1_000_000f);
        var far = new Vector3D<float>(0f, 0f, 30f);
        float damage = marauder.DamageScale, speed = marauder.AttackSpeed;

        marauder.Health = marauder.MaxHealth * 0.3f;
        Tick(field, player, far);
        var rage = Marauder.Phases[^1];
        Assert.True(marauder.IsRoaring);
        Assert.Null(marauder.Attack);
        Assert.Contains(field.TakePhaseChanges(), c => c.Phase == rage);

        float health = marauder.Health;
        Assert.False(field.Damage(marauder, 5000f));   // nothing hurts him while he roars
        Assert.Equal(health, marauder.Health);
        var standing = marauder.Position;
        RunThrough(field, player, far, rage.Roar * 0.9f);
        Assert.Equal(standing, marauder.Position);
        Assert.Null(marauder.Attack);
        Assert.False(marauder.IsEnraged);

        RunThrough(field, player, far, rage.Roar * 0.2f);
        Assert.False(marauder.IsRoaring);
        Assert.True(marauder.IsEnraged);
        Assert.Equal(20, marauder.Rage);
        Assert.Equal(damage * 1.2f, marauder.DamageScale, 4);   // 20 rage: +20% damage
        Assert.Equal(speed * 1.3f, marauder.AttackSpeed, 4);    // frenzy: 30% quicker attacks
        Assert.True(field.Damage(marauder, 1f) || marauder.Health < health);
    }

    [Fact]
    public void TheMarauder_IsLockedUntilTheKingIsSlain_AndPaysMoreGear()
    {
        var save = new DelveSave();
        Assert.True(BossHunt.IsOpen(save, BossHunt.HollowKing));
        Assert.False(BossHunt.IsOpen(save, BossHunt.Marauder));
        Assert.NotNull(BossHunt.WhyLocked(save, BossHunt.Marauder));

        BossHunt.CountKill(save, BossHunt.HollowKing);
        Assert.True(BossHunt.IsOpen(save, BossHunt.Marauder));

        BossHunt.CountKill(save, BossHunt.Marauder);
        Assert.Equal(1, save.MaraudersSlain);
        Assert.Equal(1, save.BossesSlain);   // the King's count (the Unbound and Kingbreaker bounties) is his alone

        Assert.Equal(0.35f, BossHunt.Marauder.GearChance);
        Assert.Equal(0.25f, BossHunt.HollowKing.GearChance);
        Assert.True(BossHunt.Marauder.Silver > BossHunt.HollowKing.Silver && BossHunt.Marauder.Marks == 2);
        var random = new Random(8);
        int drops = Enumerable.Range(0, 4000).Count(_ => BossHunt.Marauder.RollsGear(random));
        Assert.InRange(drops / 4000f, 0.32f, 0.38f);
    }

    [Fact]
    public void AHuntPlan_KnowsItsBoss()
    {
        var plan = RunPlan.Hunt(BossHunt.Marauder);
        Assert.Equal(RunKind.Arena, plan.Kind);
        Assert.Equal(BossHunt.Marauder, plan.HuntBoss);
        Assert.Contains("Marauder", plan.Title);
        Assert.Equal(BossHunt.HollowKing, new RunPlan(RunKind.Arena, null).HuntBoss);
    }

    [Fact]
    public void TheMarauder_IsInTheBestiary_AndItsRoarPlaysTheRoarClip()
    {
        Assert.Contains(Marauder, EnemyKind.Foes);

        var enemy = new Enemy(1, Marauder, Vector3D<float>.Zero, EnemyScaling.None) { RoarSeconds = 3f, RoarLeft = 1.5f };
        var pose = new EnemyMotion().Update(enemy, Step, new Dictionary<string, float> { ["Idle"] = 3f, ["Roar"] = 1f });
        Assert.Equal("Roar", pose.Clip);
        Assert.Equal(0.5f, pose.Time, 2);
    }
}
