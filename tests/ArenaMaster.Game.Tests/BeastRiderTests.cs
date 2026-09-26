using ArenaMaster.Game.Combat;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

public class BeastRiderTests
{
    private const float Step = 1f / 60f;

    private static float? FlatGround(float x, float z) => MathF.Abs(x) < 200f && MathF.Abs(z) < 200f ? 0f : null;

    private static AttackSpec Charge => EnemyKind.BeastRider.Attacks[0];

    /// <summary>Runs the field for <paramref name="seconds"/> with the player at <paramref name="feet"/>, returning the blows that reached them.</summary>
    private static List<Strike> Run(EnemyField field, PlayerHealth health, float seconds, Vector3D<float> feet, float blockChance = 0f)
    {
        var strikes = new List<Strike>();
        var condition = new PlayerCondition();
        for (float t = 0f; t < seconds - 1e-4f; t += Step)
        {
            health.Update(Step);
            condition.Update(Step);
            field.Update(Step, new PlayerTarget(feet, true, health, condition, blockChance), FlatGround);
            strikes.AddRange(field.Strikes);
        }

        return strikes;
    }

    /// <summary>A rider 8 m out, ready to charge, and the first frame of its wind-up.</summary>
    private static (EnemyField Field, Enemy Rider, PlayerHealth Health) WindingUp()
    {
        var field = new EnemyField(new Random(1)) { TargetCount = 0 };
        var rider = field.Spawn(new Vector3D<float>(8f, 0f, 0f), EnemyKind.BeastRider);
        rider.AttackCooldown = 0f;
        var health = new PlayerHealth(10_000f);
        Run(field, health, Step, Vector3D<float>.Zero);
        Assert.Equal(AttackType.Lunge, rider.Attack?.Type);
        return (field, rider, health);
    }

    private static float Heading(Enemy rider)
    {
        var way = rider.AttackTarget - rider.AttackOrigin;
        return MathF.Atan2(way.Z, -way.X);   // 0 straight at the origin, positive toward +Z
    }

    [Fact]
    public void ItIsFarTougherThanAGhoulOnFoot_AndQuicker()
    {
        Assert.True(EnemyKind.BeastRider.MaxHealth >= 3f * EnemyKind.Ghoul.MaxHealth);
        Assert.True(EnemyKind.BeastRider.Speed > EnemyKind.Ghoul.Speed);
        Assert.Equal(EnemyTier.Fodder, EnemyKind.BeastRider.Tier);
    }

    [Fact]
    public void ItWindsUpForOneAndAHalfSeconds_BeforeTheCharge()
    {
        var (field, rider, health) = WindingUp();

        Run(field, health, Charge.WindUp - 0.1f, new Vector3D<float>(0f, 0f, 20f));   // well out of the way
        Assert.Equal(AttackPhase.WindUp, rider.AttackPhase);
        Assert.Equal(8f, rider.Position.X, 3);                                         // standing its ground while it winds up

        Run(field, health, 0.2f, new Vector3D<float>(0f, 0f, 20f));
        Assert.Equal(AttackPhase.Active, rider.AttackPhase);
        Assert.True(rider.Position.X < 8f);
    }

    [Fact]
    public void TheLane_FollowsThePlayer_ThenLocks()
    {
        var (field, rider, health) = WindingUp();
        var aside = new Vector3D<float>(0f, 0f, 4f);

        Run(field, health, Charge.Tracking * 0.5f, aside);
        Assert.Equal(MathF.Atan2(4f, 8f), Heading(rider), 2);   // turned to follow

        Run(field, health, Charge.Tracking * 0.5f + 0.05f, aside);
        float locked = Heading(rider);
        Run(field, health, 0.3f, new Vector3D<float>(0f, 0f, -4f));
        Assert.Equal(locked, Heading(rider), 4);                  // locked: it no longer follows
        Assert.Equal(Charge.Reach, (rider.AttackTarget - rider.AttackOrigin).Length, 3);
    }

    [Fact]
    public void StandingInTheLane_TheChargeHits()
    {
        var (field, _, health) = WindingUp();

        var strikes = Run(field, health, Charge.WindUp + Charge.Active, Vector3D<float>.Zero);

        Assert.Equal(Charge.Damage, Assert.Single(strikes).Damage, 3);
    }

    [Fact]
    public void SteppingOutOfTheLaneOnceItLocks_TheChargeMisses()
    {
        var (field, rider, health) = WindingUp();

        Run(field, health, Charge.Tracking + 0.05f, Vector3D<float>.Zero);
        var strikes = Run(field, health, Charge.WindUp - Charge.Tracking + Charge.Active, new Vector3D<float>(0f, 0f, 3f));

        Assert.Empty(strikes);
        Assert.Equal(AttackPhase.Recover, rider.AttackPhase);
        Assert.True(rider.Position.X < -4f);   // it thundered on past
    }

    [Fact]
    public void TheDirector_AddsRidersFromFourMinutes()
    {
        Assert.Equal(0f, RunDirector.RiderShareAt(3.9f * 60f));
        Assert.Equal(0.02f, RunDirector.RiderShareAt(4f * 60f), 4);
        Assert.Equal(0.06f, RunDirector.RiderShareAt(25f * 60f), 4);
        Assert.Contains(RunDirector.MixAt(10f * 60f), m => m.Kind == EnemyKind.BeastRider && m.Share > 0f);
        Assert.True(RunDirector.MixAt(30f * 60f).Sum(m => m.Share) < 0.5f);
    }
}
