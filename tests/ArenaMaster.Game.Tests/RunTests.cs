using ArenaMaster.Game.Combat;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

public class EliteAndBossAttackTests
{
    private const float Step = 1f / 60f;

    private static float? FlatGround(float x, float z) => MathF.Abs(x) < 200f && MathF.Abs(z) < 200f ? 0f : null;

    private static EnemyKind Only(EnemyKind kind, AttackType type) => kind with { Attacks = kind.Attacks.Where(a => a.Type == type).ToArray() };

    private sealed class Scene
    {
        public EnemyField Field { get; } = new(new Random(1)) { TargetCount = 0 };
        public PlayerHealth Health { get; } = new(1000f);
        public PlayerCondition Condition { get; } = new();

        public float Taken => Health.Max - Health.Current;

        public Enemy Spawn(EnemyKind kind, float x)
        {
            var enemy = Field.Spawn(new Vector3D<float>(x, 0f, 0f), kind);
            enemy.AttackCooldown = 0f;
            return enemy;
        }

        /// <summary>Runs the field for <paramref name="seconds"/> with the player wherever <paramref name="playerAt"/> says at each moment.</summary>
        public void Run(float seconds, Func<float, Vector3D<float>> playerAt, bool grounded = true)
        {
            for (float t = 0f; t < seconds; t += Step)
            {
                Health.Update(Step);
                Condition.Update(Step);
                Field.Update(Step, new PlayerTarget(playerAt(t), grounded, Health, Condition), FlatGround);
            }
        }
    }

    private static readonly Func<float, Vector3D<float>> StandingAtOrigin = _ => Vector3D<float>.Zero;

    [Fact]
    public void ALunge_WindsUpInPlace_ThenChargesDownItsLane_AndHitsOnce()
    {
        var scene = new Scene();
        var brute = scene.Spawn(Only(EnemyKind.Brute, AttackType.Lunge), 6f);
        var lunge = brute.Kind.Attacks[0];

        scene.Run(lunge.WindUp * 0.9f, StandingAtOrigin);
        Assert.Equal(AttackType.Lunge, brute.Attack?.Type);
        Assert.Equal(6f, brute.Position.X, 3);   // standing still, telegraphing
        Assert.Equal(0f, scene.Taken);

        scene.Run(lunge.WindUp * 0.1f + lunge.Active + 0.05f, StandingAtOrigin);
        Assert.Equal(lunge.Damage, scene.Taken);
        Assert.True(scene.Condition.Knockback.X < 0f, "knocked back along the lunge (toward -X)");
    }

    [Fact]
    public void ALunge_KeepsToTheLaneItShowed_SoSteppingAsideDodgesIt()
    {
        var scene = new Scene();
        var brute = scene.Spawn(Only(EnemyKind.Brute, AttackType.Lunge), 6f);
        var lunge = brute.Kind.Attacks[0];

        scene.Run(0.1f, StandingAtOrigin);                                    // the lane is laid down toward the origin
        scene.Run(lunge.WindUp + lunge.Active, _ => new Vector3D<float>(0f, 0f, 4f));   // then the player steps 4 m aside

        Assert.Equal(0f, scene.Taken);
        Assert.InRange(brute.Position.X, 6f - lunge.Reach - 0.1f, 6f - lunge.Reach + 0.1f);   // it charged the full lane anyway
    }

    [Fact]
    public void ALeapSlam_LandsOnTheMarkedSpot_StunningAndShovingWhoeverIsInside()
    {
        var scene = new Scene();
        var brute = scene.Spawn(Only(EnemyKind.Brute, AttackType.LeapSlam), 8f);
        var slam = brute.Kind.Attacks[0];

        scene.Run(slam.WindUp + slam.Active * 0.5f, StandingAtOrigin);
        Assert.True(brute.Position.Y > 1f, "it should be in the air mid-leap");

        scene.Run(slam.Active * 0.5f + 0.05f, StandingAtOrigin);
        Assert.Equal(slam.Damage, scene.Taken);
        Assert.True(scene.Condition.IsStunned);
        Assert.Equal(0f, brute.Position.Length, 3);   // landed where the circle was
    }

    [Fact]
    public void ALeapSlam_MissesAPlayerWhoLeftTheCircle()
    {
        var scene = new Scene();
        var brute = scene.Spawn(Only(EnemyKind.Brute, AttackType.LeapSlam), 8f);
        var slam = brute.Kind.Attacks[0];

        scene.Run(0.1f, StandingAtOrigin);
        scene.Run(slam.WindUp + slam.Active, _ => new Vector3D<float>(0f, 0f, -(slam.Reach + 1.5f)));

        Assert.Equal(0f, scene.Taken);
        Assert.False(scene.Condition.IsStunned);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void AShockwave_HitsAPlayerOnTheGround_AndPassesUnderOneInTheAir(bool grounded, bool expectHit)
    {
        var scene = new Scene();
        var king = scene.Spawn(Only(EnemyKind.HollowKing, AttackType.Shockwave), 10f);
        var wave = king.Kind.Attacks[0];

        scene.Run(wave.WindUp + wave.Active + 0.05f, StandingAtOrigin, grounded);

        Assert.Equal(expectHit ? wave.Damage : 0f, scene.Taken);
    }

    [Fact]
    public void ASummon_CallsUpARingOfFodder()
    {
        var scene = new Scene();
        var king = scene.Spawn(Only(EnemyKind.HollowKing, AttackType.Summon), 20f);
        var summon = king.Kind.Attacks[0];

        scene.Run(summon.WindUp + 0.05f, StandingAtOrigin);

        Assert.Equal((int)summon.Reach, scene.Field.AliveCount);
    }

    [Fact]
    public void AnEliteOutOfRangeOfEveryAttack_JustChases()
    {
        var scene = new Scene();
        var brute = scene.Spawn(EnemyKind.Brute, 30f);

        scene.Run(0.5f, StandingAtOrigin);

        Assert.Null(brute.Attack);
        Assert.True(brute.Position.X < 30f);
    }

    [Fact]
    public void Scaling_AppliesToEnemiesAsTheySpawn()
    {
        var field = new EnemyField(new Random(1)) { TargetCount = 0, Scaling = new EnemyScaling(Health: 2f, Damage: 1.5f, Speed: 1.1f) };

        var ghoul = field.Spawn(Vector3D<float>.Zero);

        Assert.Equal(EnemyKind.Ghoul.MaxHealth * 2f, ghoul.MaxHealth);
        Assert.Equal(ghoul.MaxHealth, ghoul.Health);
        Assert.Equal(EnemyKind.Ghoul.Speed * 1.1f, ghoul.Speed, 4);
        Assert.Equal(1.5f, ghoul.DamageScale);
    }
}

public class RunDirectorTests
{
    [Fact]
    public void TheHordeGrows_AndToughensOverTheRun()
    {
        Assert.Equal(16, RunDirector.FodderCount(0f));
        Assert.InRange(RunDirector.FodderCount(10f * 60f), 50, 80);
        Assert.InRange(RunDirector.FodderCount(RunDirector.RunLength), 280, RunDirector.MaxFodder);
        Assert.True(RunDirector.SpawnInterval(20f * 60f) < RunDirector.SpawnInterval(0f));
        Assert.Equal(1f, RunDirector.ScalingAt(0f).Health);
        Assert.True(RunDirector.ScalingAt(20f * 60f).Health > 3f);
    }

    [Fact]
    public void Elites_ComeOnSchedule_InGrowingWaves()
    {
        var director = new RunDirector();
        var field = new EnemyField(new Random(1));
        var waves = new List<(float Time, int Count)>();

        for (float t = 0f; t < RunDirector.RunLength; t += 0.5f)
        {
            if (director.Update(t, field).Elites is > 0 and var count)
            {
                waves.Add((t, count));
            }
        }

        Assert.Equal(RunDirector.FirstEliteAt, waves[0].Time);
        Assert.Equal(1, waves[0].Count);
        Assert.Equal(RunDirector.FirstEliteAt + RunDirector.EliteInterval, waves[1].Time);
        Assert.Contains(waves, w => w.Count == 3);
    }

    [Fact]
    public void Bosses_ArriveOnceEach_AndTheLastIsTheFinal()
    {
        var director = new RunDirector();
        var field = new EnemyField(new Random(1));
        var bosses = new List<(float Time, bool Final)>();

        for (float t = 0f; t < RunDirector.RunLength; t += 0.5f)
        {
            var orders = director.Update(t, field);
            if (orders.Boss)
            {
                bosses.Add((t, orders.FinalBoss));
            }
        }

        Assert.Equal(RunDirector.BossTimes, bosses.Select(b => b.Time));
        Assert.Equal(new[] { false, false, true }, bosses.Select(b => b.Final));
    }

    [Fact]
    public void AClockSkip_FiresEachMissedEventOnce_NotAPileOfThem()
    {
        var director = new RunDirector();
        var field = new EnemyField(new Random(1));

        var orders = director.Update(25f * 60f, field);   // straight past the first elite and two bosses

        Assert.Equal(RunDirector.ElitesPerWave(25f * 60f), orders.Elites);
        Assert.True(orders.Boss);
        Assert.Equal(0, director.Update(25f * 60f + 1f, field).Elites);
    }

    [Fact]
    public void TheRunIsWonAtThirtyMinutes()
    {
        Assert.False(RunDirector.IsWon(RunDirector.RunLength - 0.1f));
        Assert.True(RunDirector.IsWon(RunDirector.RunLength));
    }
}

public class PlayerConditionTests
{
    [Fact]
    public void AKnockback_DiesAway()
    {
        var condition = new PlayerCondition();
        condition.Knock(new Vector3D<float>(1f, 5f, 0f), 12f);

        Assert.Equal(12f, condition.Knockback.X, 3);
        Assert.Equal(0f, condition.Knockback.Y);   // flat: never up into the air

        for (int i = 0; i < 120; i++)
        {
            condition.Update(1f / 60f);
        }

        Assert.Equal(Vector3D<float>.Zero, condition.Knockback);
    }

    [Fact]
    public void AShorterStun_DoesNotCutALongerOneShort()
    {
        var condition = new PlayerCondition();
        condition.Stun(1f);
        condition.Stun(0.3f);

        condition.Update(0.5f);

        Assert.True(condition.IsStunned);
    }
}
