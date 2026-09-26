using ArenaMaster.Game.Combat;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

public class GhoulTacticianTests
{
    private const float Step = 1f / 60f;

    private static float? FlatGround(float x, float z) => MathF.Abs(x) < 200f && MathF.Abs(z) < 200f ? 0f : null;

    private static AttackSpec Bomb => EnemyKind.GhoulTactician.Attacks[0];

    private static List<Strike> Run(EnemyField field, PlayerHealth health, float seconds, Vector3D<float>? feet = null, float blockChance = 0f,
        Func<float, float, float?>? ground = null, Action<EnemyField>? watch = null)
    {
        var strikes = new List<Strike>();
        var condition = new PlayerCondition();
        for (float t = 0f; t < seconds - 1e-4f; t += Step)
        {
            health.Update(Step);
            condition.Update(Step);
            field.Update(Step, new PlayerTarget(feet ?? Vector3D<float>.Zero, true, health, condition, blockChance), ground ?? FlatGround);
            strikes.AddRange(field.Strikes);
            watch?.Invoke(field);
        }

        return strikes;
    }

    /// <summary>A Tactician 12 m out, ready to throw; runs until its bomb is away.</summary>
    private static EnemyField Thrown(PlayerHealth health, Func<float, float, float?>? ground = null)
    {
        var field = new EnemyField(new Random(1)) { TargetCount = 0 };
        field.Spawn(new Vector3D<float>(12f, 0f, 0f), EnemyKind.GhoulTactician).AttackCooldown = 0f;
        for (int i = 0; i < 180 && field.Bombs.Count == 0; i++)
        {
            Run(field, health, Step, ground: ground);
        }

        Assert.Single(field.Bombs);
        return field;
    }

    [Fact]
    public void ItKeepsItsDistance()
    {
        var field = new EnemyField(new Random(1)) { TargetCount = 0 };
        var tactician = field.Spawn(new Vector3D<float>(30f, 0f, 0f), EnemyKind.GhoulTactician);

        Run(field, new PlayerHealth(1e9f), 10f);

        Assert.InRange(tactician.Position.X, EnemyKind.GhoulTactician.StandOff - 0.5f, EnemyKind.GhoulTactician.StandOff + 0.1f);
    }

    [Fact]
    public void TheBomb_IsLobbedHigh_LandsShort_BouncesTwice_ThenRollsToAStopAndGoesOff()
    {
        var health = new PlayerHealth(10_000f);
        var field = Thrown(health);
        var bomb = field.Bombs[0];
        Assert.True(bomb.Velocity.Y > 3f);                  // up, in an arc
        Assert.Equal(Bomb.Count, bomb.BouncesLeft);
        Assert.Equal("ghoul_bomb.glb", bomb.Model);

        var landings = new List<float>();
        float top = 0f;
        int bounces = bomb.BouncesLeft;
        bool rolled = false;
        EnemyBlast? blast = null;
        Run(field, health, 4f, feet: new Vector3D<float>(0f, 0f, 30f), watch: f =>   // the player walks well clear once it is thrown
        {
            if (f.Bombs.Count == 1)
            {
                top = MathF.Max(top, bomb.Position.Y);
                if (bomb.BouncesLeft < bounces)
                {
                    bounces = bomb.BouncesLeft;
                    landings.Add(bomb.Position.X);
                }

                rolled |= bomb.Rolling;
            }

            blast ??= f.Blasts.FirstOrDefault();
        });

        Assert.True(top > EnemyField.LobHeight + 1f);
        Assert.Equal(2, landings.Count);
        Assert.InRange(landings[0], 3.5f, 5.5f);            // about 60% of the way to where the player stood
        Assert.True(landings[1] < landings[0]);            // bouncing on toward them
        Assert.True(rolled);
        Assert.Empty(field.Bombs);
        Assert.NotNull(blast);
        Assert.Equal(EnemyView.BombBurstModel, blast!.Model);
        Assert.Equal(Bomb.Splash, blast.Radius);
        Assert.InRange(blast.Centre.X, -1.5f, 2f);          // it came to rest about where the player had stood
        Assert.Equal(10_000f, health.Current);
    }

    [Fact]
    public void StandingStill_TheBombCatchesThePlayer_Once()
    {
        var health = new PlayerHealth(10_000f);
        var field = Thrown(health);

        var strikes = Run(field, health, 4f);

        Assert.Equal(Bomb.Damage, Assert.Single(strikes).Damage, 3);
        Assert.Empty(field.Bombs);
    }

    [Fact]
    public void RunningIntoTheBomb_SetsItOffAtOnce()
    {
        var health = new PlayerHealth(10_000f);
        var field = Thrown(health);
        var bomb = field.Bombs[0];
        var underIt = new Vector3D<float>(4.6f, 0f, 0f);   // where it first comes down
        float flight = 0f;

        var strikes = Run(field, health, 3f, feet: underIt, watch: f => flight += f.Bombs.Count > 0 ? Step : 0f);

        Assert.Single(strikes);
        Assert.Equal(Bomb.Count, bomb.BouncesLeft);        // it never bounced
        Assert.True(flight < 1.1f);
    }

    [Fact]
    public void ATreeInTheWay_SetsItOff()
    {
        var health = new PlayerHealth(10_000f);
        var field = Thrown(health);
        field.Obstacles = (centre, radius) => MathF.Abs(centre.X - 3f) < radius + 0.3f && MathF.Abs(centre.Z) < 1f;   // a trunk 3 m out, between them
        EnemyBlast? blast = null;

        var strikes = Run(field, health, 3f, watch: f => blast ??= f.Blasts.FirstOrDefault());

        Assert.Empty(strikes);                              // over 3 m off: clear of the blast
        Assert.Empty(field.Bombs);
        Assert.InRange(blast!.Centre.X, 3f, 4f);
    }

    [Fact]
    public void ASteepHillside_SetsItOff()
    {
        // Once it is thrown, a bank rises steeply 3 m in front of the player, who stands on top of it: the bomb bounces into it.
        static float? Bank(float x, float z) => MathF.Min(3f, MathF.Max(0f, (3f - x) * 3f));
        var health = new PlayerHealth(10_000f);
        var field = Thrown(health);
        EnemyBlast? blast = null;

        var strikes = Run(field, health, 4f, feet: new Vector3D<float>(0f, 3f, 0f), ground: Bank, watch: f => blast ??= f.Blasts.FirstOrDefault());

        Assert.Empty(field.Bombs);
        Assert.NotNull(blast);
        Assert.InRange(blast!.Centre.X, 2.3f, 3.2f);        // against the bank, not up on the top
        Assert.Empty(strikes);
    }

    [Fact]
    public void AShield_BlocksTheBlast()
    {
        var health = new PlayerHealth(100f);
        var field = Thrown(health);

        var strikes = Run(field, health, 4f, blockChance: 1f);

        Assert.True(Assert.Single(strikes).Blocked);
        Assert.Equal(100f, health.Current);
    }

    [Fact]
    public void ItsBomb_GlowsThroughTheWindUp()
    {
        var field = new EnemyField(new Random(1)) { TargetCount = 0 };
        var tactician = field.Spawn(new Vector3D<float>(12f, 0f, 0f), EnemyKind.GhoulTactician);
        tactician.AttackCooldown = 0f;
        var health = new PlayerHealth(10_000f);
        var glows = new List<float>();
        for (int i = 0; i < 180 && field.Bombs.Count == 0; i++)
        {
            Run(field, health, Step);
            glows.Add(EnemyView.Glow(tactician));
        }

        Assert.InRange(glows[0], 0.14f, 0.2f);
        Assert.True(glows[^2] > 0.9f);
    }

    [Fact]
    public void GroundingCharm_SoftensTheBomb()
    {
        var health = new PlayerHealth(10_000f);
        var field = new EnemyField(new Random(1)) { TargetCount = 0, RangedDamageTaken = 0.75f };
        field.Spawn(new Vector3D<float>(12f, 0f, 0f), EnemyKind.GhoulTactician).AttackCooldown = 0f;

        var strikes = Run(field, health, 6f);

        Assert.Equal(Bomb.Damage * 0.75f, Assert.Single(strikes).Damage, 3);
    }

    [Fact]
    public void TheDirector_AddsTacticiansFromSevenMinutes()
    {
        Assert.Equal(0f, RunDirector.TacticianShareAt(6.9f * 60f));
        Assert.Equal(0.02f, RunDirector.TacticianShareAt(7f * 60f), 4);
        Assert.Equal(0.06f, RunDirector.TacticianShareAt(28f * 60f), 4);
        Assert.Contains(RunDirector.MixAt(10f * 60f), m => m.Kind == EnemyKind.GhoulTactician && m.Share > 0f);
    }
}
