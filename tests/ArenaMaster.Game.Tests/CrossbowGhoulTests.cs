using ArenaMaster.Game.Combat;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

public class CrossbowGhoulTests
{
    private const float Step = 1f / 60f;

    private static float? FlatGround(float x, float z) => MathF.Abs(x) < 200f && MathF.Abs(z) < 200f ? 0f : null;

    private static EnemyField QuietField() => new(new Random(1)) { TargetCount = 0 };

    private static AttackSpec Shot => EnemyKind.CrossbowGhoul.Attacks[0];

    /// <summary>Runs the field for <paramref name="seconds"/> with the player at <paramref name="feet"/>, collecting the blows that reach them.</summary>
    private static List<Strike> Run(EnemyField field, PlayerHealth health, float seconds, Func<float, Vector3D<float>>? feet = null, float blockChance = 0f,
        PlayerCondition? condition = null, float start = 0f)
    {
        var strikes = new List<Strike>();
        condition ??= new PlayerCondition();
        for (float t = 0f; t < seconds; t += Step)
        {
            health.Update(Step);
            condition.Update(Step);
            var at = feet?.Invoke(start + t) ?? Vector3D<float>.Zero;
            field.Update(Step, new PlayerTarget(at, true, health, condition, blockChance), FlatGround);
            strikes.AddRange(field.Strikes);
        }

        return strikes;
    }

    [Fact]
    public void ItWalksIn_ThenStopsAtItsStandOff()
    {
        var field = QuietField();
        var shooter = field.Spawn(new Vector3D<float>(25f, 0f, 0f), EnemyKind.CrossbowGhoul);

        Run(field, new PlayerHealth(10_000f), 8f);

        Assert.InRange(shooter.Position.X, EnemyKind.CrossbowGhoul.StandOff - 0.5f, EnemyKind.CrossbowGhoul.StandOff + 0.1f);
    }

    [Fact]
    public void ItShoots_AtAPlayerStandingInRange_WithACooldownBetweenShots()
    {
        var field = QuietField();
        field.Spawn(new Vector3D<float>(10f, 0f, 0f), EnemyKind.CrossbowGhoul);
        var health = new PlayerHealth(10_000f);
        float cycle = EnemyKind.CrossbowGhoul.AttackCooldown + Shot.WindUp + Shot.Active + Shot.Recover;

        var strikes = Run(field, health, 2f * cycle);

        Assert.Equal(2, strikes.Count);   // the first after its short breather on arrival, then one per cycle
        Assert.All(strikes, s => Assert.Equal(Shot.Damage, s.Damage, 3));
        Assert.Equal(10_000f - 2f * Shot.Damage, health.Current, 3);
    }

    [Fact]
    public void ItsCrossbow_GlowsFromFaintToBright_ThroughTheWindUp()
    {
        var field = QuietField();
        var shooter = field.Spawn(new Vector3D<float>(10f, 0f, 0f), EnemyKind.CrossbowGhoul);
        shooter.AttackCooldown = 0f;
        var health = new PlayerHealth(10_000f);
        Assert.Equal(0f, EnemyView.Glow(shooter));

        var glows = new List<float>();
        for (int i = 0; i < 120 && field.Bolts.Count == 0; i++)
        {
            Run(field, health, Step);
            glows.Add(EnemyView.Glow(shooter));
        }

        Assert.NotEmpty(field.Bolts);                       // it fired
        Assert.InRange(glows[0], 0.14f, 0.2f);              // faint at first
        Assert.True(glows[^2] > 0.9f);                      // bright just before the shot
        for (int i = 1; i < glows.Count - 1; i++)
        {
            Assert.True(glows[i] >= glows[i - 1], "the glow only ever brightens during the wind-up");
        }
    }

    [Fact]
    public void AStepAside_AfterTheShot_DodgesTheBolt()
    {
        var field = QuietField();
        var shooter = field.Spawn(new Vector3D<float>(12f, 0f, 0f), EnemyKind.CrossbowGhoul);
        shooter.AttackCooldown = 0f;
        var health = new PlayerHealth(10_000f);
        for (float t = 0f; t < 3f && field.Bolts.Count == 0; t += Step)
        {
            Run(field, health, Step);
        }

        Assert.Single(field.Bolts);
        var strikes = Run(field, health, 1.6f, feet: _ => new Vector3D<float>(0f, 0f, 2f));   // two steps to the side

        Assert.Empty(strikes);
        Assert.Equal(10_000f, health.Current);
        Assert.Empty(field.Bolts);   // it flew on past and was spent
    }

    [Fact]
    public void AShield_BlocksTheBolt_ShoveAndAll()
    {
        var field = QuietField();
        field.Spawn(new Vector3D<float>(10f, 0f, 0f), EnemyKind.CrossbowGhoul).AttackCooldown = 0f;
        var health = new PlayerHealth(100f);
        var condition = new PlayerCondition();

        var strikes = Run(field, health, 2.5f, blockChance: 1f, condition: condition);

        Assert.True(Assert.Single(strikes).Blocked);
        Assert.Equal(100f, health.Current);
        Assert.Equal(Vector3D<float>.Zero, condition.Knockback);
    }

    [Fact]
    public void ALandedBolt_ShovesThePlayer()
    {
        var field = QuietField();
        field.Spawn(new Vector3D<float>(10f, 0f, 0f), EnemyKind.CrossbowGhoul).AttackCooldown = 0f;
        var condition = new PlayerCondition();
        bool shoved = false;

        for (float t = 0f; t < 2.5f && !shoved; t += Step)
        {
            Run(field, new PlayerHealth(10_000f), Step, condition: condition);
            shoved = condition.Knockback.X < 0f;   // away from the shooter, toward -X
        }

        Assert.True(shoved);
    }

    [Fact]
    public void AFrozenShooter_HoldsItsShot()
    {
        var field = QuietField();
        var shooter = field.Spawn(new Vector3D<float>(10f, 0f, 0f), EnemyKind.CrossbowGhoul);
        shooter.AttackCooldown = 0f;
        var health = new PlayerHealth(100f);
        Run(field, health, 0.5f);   // winding up
        Assert.Equal(AttackPhase.WindUp, shooter.AttackPhase);

        shooter.Freeze(3f);
        Run(field, health, 2.5f);

        Assert.Empty(field.Bolts);
        Assert.Equal(100f, health.Current);
    }

    [Fact]
    public void TheDirector_MixesCrossbowsIntoTheSwarm_AfterTheFirstMinuteAndAHalf()
    {
        Assert.Equal(0f, RunDirector.RangedShareAt(60f));
        Assert.Equal(0.06f, RunDirector.RangedShareAt(90f), 4);
        Assert.Equal(0.15f, RunDirector.RangedShareAt(29f * 60f), 4);

        var field = new EnemyField(new Random(4)) { TargetCount = 200, SpawnInterval = 0f, RangedKind = EnemyKind.CrossbowGhoul, RangedShare = 0.5f };
        Run(field, new PlayerHealth(1e9f), 1.5f);

        int crossbows = field.Enemies.Count(e => e.Kind == EnemyKind.CrossbowGhoul);
        Assert.Equal(200, field.AliveCount);
        Assert.InRange(crossbows, 70, 130);
    }
}
