using ArenaMaster.Game.Combat;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

public class GhoulMageTests
{
    private const float Step = 1f / 60f;

    private static float? FlatGround(float x, float z) => MathF.Abs(x) < 200f && MathF.Abs(z) < 200f ? 0f : null;

    private static EnemyField QuietField() => new(new Random(1)) { TargetCount = 0 };

    private static AttackSpec Fireball => EnemyKind.GhoulMage.Attacks[0];

    private static List<Strike> Run(EnemyField field, PlayerHealth health, float seconds, Vector3D<float>? feet = null, float blockChance = 0f)
    {
        var strikes = new List<Strike>();
        var condition = new PlayerCondition();
        for (float t = 0f; t < seconds; t += Step)
        {
            health.Update(Step);
            condition.Update(Step);
            field.Update(Step, new PlayerTarget(feet ?? Vector3D<float>.Zero, true, health, condition, blockChance), FlatGround);
            strikes.AddRange(field.Strikes);
        }

        return strikes;
    }

    /// <summary>A Ghoul Mage 12 m out, ready to cast; runs until its fireball is loosed.</summary>
    private static (EnemyField Field, Enemy Mage) Loosed(PlayerHealth health)
    {
        var field = QuietField();
        var mage = field.Spawn(new Vector3D<float>(12f, 0f, 0f), EnemyKind.GhoulMage);
        mage.AttackCooldown = 0f;
        for (int i = 0; i < 180 && field.Bolts.Count == 0; i++)
        {
            Run(field, health, Step);
        }

        return (field, mage);
    }

    [Fact]
    public void ItKeepsFurtherOff_ThanTheCrossbow()
    {
        var field = QuietField();
        var mage = field.Spawn(new Vector3D<float>(30f, 0f, 0f), EnemyKind.GhoulMage);

        Run(field, new PlayerHealth(10_000f), 12f);

        Assert.True(EnemyKind.GhoulMage.StandOff > EnemyKind.CrossbowGhoul.StandOff);
        Assert.InRange(mage.Position.X, EnemyKind.GhoulMage.StandOff - 0.5f, EnemyKind.GhoulMage.StandOff + 0.1f);
    }

    [Fact]
    public void TheFireball_IsAimedAtTheGroundWhereThePlayerStood_AndMarked()
    {
        var health = new PlayerHealth(10_000f);
        var (field, _) = Loosed(health);

        var fireball = Assert.Single(field.Bolts);
        Assert.Equal(Fireball.Splash, fireball.Splash);
        Assert.Equal("ghoul_fireball.glb", fireball.Model);
        Assert.Equal(0f, fireball.Target.X, 3);
        Assert.Equal(0f, fireball.Target.Y, 3);   // the ground under the player, not their middle
        Assert.Equal(0f, fireball.Target.Z, 3);
    }

    [Fact]
    public void StandingStill_TheFireballBurstsOnThePlayer()
    {
        var health = new PlayerHealth(10_000f);
        var (field, _) = Loosed(health);

        var strikes = Run(field, health, 1f);   // it lands after about 0.85 s; its burst shows for a moment after

        Assert.Equal(Fireball.Damage, Assert.Single(strikes).Damage, 3);
        Assert.Equal(10_000f - Fireball.Damage, health.Current, 3);
        Assert.NotEmpty(field.Blasts);
    }

    [Fact]
    public void AHalfStep_IsNotEnough_ItsSplashStillCatches()
    {
        var health = new PlayerHealth(10_000f);
        var (field, _) = Loosed(health);

        var strikes = Run(field, health, 1.5f, feet: new Vector3D<float>(0f, 0f, 1f));

        Assert.Single(strikes);
    }

    [Fact]
    public void ARealSidestep_GetsClearOfTheBurst()
    {
        var health = new PlayerHealth(10_000f);
        var (field, _) = Loosed(health);

        var strikes = Run(field, health, 1.5f, feet: new Vector3D<float>(0f, 0f, 3f));

        Assert.Empty(strikes);
        Assert.Equal(10_000f, health.Current);
        Assert.Empty(field.Bolts);   // it burst on the ground where the player had been
    }

    [Fact]
    public void AShield_BlocksTheBurst()
    {
        var health = new PlayerHealth(100f);
        var (field, _) = Loosed(health);

        var strikes = Run(field, health, 1.5f, blockChance: 1f);

        Assert.True(Assert.Single(strikes).Blocked);
        Assert.Equal(100f, health.Current);
    }

    [Fact]
    public void ItsFlame_GlowsFromFaintToBright_ThroughTheLongerWindUp()
    {
        var field = QuietField();
        var mage = field.Spawn(new Vector3D<float>(12f, 0f, 0f), EnemyKind.GhoulMage);
        mage.AttackCooldown = 0f;
        var health = new PlayerHealth(10_000f);
        var glows = new List<float>();

        for (int i = 0; i < 180 && field.Bolts.Count == 0; i++)
        {
            Run(field, health, Step);
            glows.Add(EnemyView.Glow(mage));
        }

        Assert.True(Fireball.WindUp > EnemyKind.CrossbowGhoul.Attacks[0].WindUp);
        Assert.InRange(glows[0], 0.14f, 0.2f);
        Assert.True(glows[^2] > 0.9f);
        Assert.InRange(glows.Count, (int)(Fireball.WindUp * 60f) - 1, (int)(Fireball.WindUp * 60f) + 2);
    }

    [Fact]
    public void TheDirector_AddsMagesFromFiveMinutes_AlongsideTheCrossbows()
    {
        Assert.Equal(0f, RunDirector.MageShareAt(4.9f * 60f));
        Assert.Equal(0.03f, RunDirector.MageShareAt(5f * 60f), 4);
        Assert.Equal(0.08f, RunDirector.MageShareAt(25f * 60f), 4);

        var field = new EnemyField(new Random(4))
        {
            TargetCount = 300,
            SpawnInterval = 0f,
            Mix = new[] { (EnemyKind.CrossbowGhoul, 0.3f), (EnemyKind.GhoulMage, 0.2f) },
        };
        Run(field, new PlayerHealth(1e9f), 1.5f);

        int crossbows = field.Enemies.Count(e => e.Kind == EnemyKind.CrossbowGhoul);
        int mages = field.Enemies.Count(e => e.Kind == EnemyKind.GhoulMage);
        Assert.InRange(crossbows, 60, 120);
        Assert.InRange(mages, 35, 85);
        Assert.InRange(300 - crossbows - mages, 110, 190);
    }
}
