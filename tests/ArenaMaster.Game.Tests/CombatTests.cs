using ArenaMaster.Game.Combat;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

public class EnemyFieldTests
{
    private const float Step = 1f / 60f;

    private static float? FlatGround(float x, float z) => MathF.Abs(x) < 200f && MathF.Abs(z) < 200f ? 0f : null;

    private static EnemyField QuietField() => new(new Random(1)) { TargetCount = 0 };   // nothing spawns on its own

    private static void Run(EnemyField field, PlayerHealth player, Vector3D<float> playerFeet, float seconds)
    {
        for (float t = 0f; t < seconds; t += Step)
        {
            player.Update(Step);
            field.Update(Step, new PlayerTarget(playerFeet, true, player, new PlayerCondition()), FlatGround);
        }
    }

    [Fact]
    public void AnEnemy_WalksTowardThePlayer()
    {
        var field = QuietField();
        var enemy = field.Spawn(new Vector3D<float>(20f, 0f, 0f));

        Run(field, new PlayerHealth(100f), Vector3D<float>.Zero, 1f);

        Assert.InRange(enemy.Position.X, 20f - EnemyKind.Ghoul.Speed * 1.05f, 20f - EnemyKind.Ghoul.Speed * 0.95f);
        Assert.Equal(-MathF.PI / 2f, enemy.Yaw, 3);   // facing -X, toward the player
    }

    [Fact]
    public void AnEnemy_StopsAtThePlayersEdge_AndClawsOnlyEveryContactInterval()
    {
        var field = QuietField();
        var player = new PlayerHealth(100f);
        var enemy = field.Spawn(new Vector3D<float>(3f, 0f, 0f));

        Run(field, player, Vector3D<float>.Zero, 3f);

        float reach = EnemyKind.Ghoul.Radius + EnemyField.PlayerRadius;
        Assert.True(enemy.Position.Length >= reach - 1e-3f, $"enemy walked into the player: {enemy.Position.Length} < {reach}");

        // About 2.5 s of contact at one claw per 0.8 s: 3 or 4 claws, not one per frame.
        float taken = player.Max - player.Current;
        Assert.InRange(taken, 2 * EnemyKind.Ghoul.ContactDamage, 4 * EnemyKind.Ghoul.ContactDamage);
    }

    [Fact]
    public void EnemiesOnTopOfEachOther_SpreadApart()
    {
        var field = QuietField();
        var a = field.Spawn(new Vector3D<float>(10f, 0f, 0f));
        var b = field.Spawn(new Vector3D<float>(10f, 0f, 0.01f));

        Run(field, new PlayerHealth(100f), new Vector3D<float>(-50f, 0f, 0f), 1f);

        float apart = Vector3D.Distance(a.Position, b.Position);
        Assert.True(apart > EnemyKind.Ghoul.Radius, $"still stacked: {apart} m apart");
    }

    [Fact]
    public void Damage_KillsAtZero_CountsTheKillOnce_AndTheBodyGoesAfterItsDeath()
    {
        var field = QuietField();
        var enemy = field.Spawn(new Vector3D<float>(10f, 0f, 0f));

        Assert.False(field.Damage(enemy, 20f));
        Assert.True(field.Damage(enemy, 20f));
        Assert.False(field.Damage(enemy, 20f));   // already dead
        Assert.Equal(1, field.Kills);
        Assert.Contains(enemy, field.Enemies);    // still sinking

        var gone = new List<Enemy>();
        for (float t = 0f; t < EnemyField.DeathDuration + 0.1f; t += Step)
        {
            gone.AddRange(field.Update(Step, new PlayerTarget(Vector3D<float>.Zero, true, new PlayerHealth(100f), new PlayerCondition()), FlatGround));
        }

        Assert.Contains(enemy, gone);
        Assert.DoesNotContain(enemy, field.Enemies);
    }

    [Fact]
    public void FirstHit_FindsTheNearerEnemyAlongTheLine_AndMissesOverTheirHeads()
    {
        var field = QuietField();
        var near = field.Spawn(new Vector3D<float>(5f, 0f, 0f));
        field.Spawn(new Vector3D<float>(10f, 0f, 0f));

        var hit = field.FirstHit(new Vector3D<float>(0f, 1f, 0f), new Vector3D<float>(20f, 1f, 0f), 0.1f, out float along);
        Assert.Same(near, hit);
        Assert.InRange(along, 0.2f, 0.25f);

        Assert.Null(field.FirstHit(new Vector3D<float>(0f, 3f, 0f), new Vector3D<float>(20f, 3f, 0f), 0.1f, out _));
    }

    [Fact]
    public void TheSpawner_KeepsTheTargetCount_InTheRingAroundThePlayer()
    {
        var field = new EnemyField(new Random(7)) { TargetCount = 5, SpawnInterval = 0.1f };
        var player = new PlayerHealth(100f);

        for (int i = 0; i < 20; i++)
        {
            field.Update(0.1f, new PlayerTarget(Vector3D<float>.Zero, true, player, new PlayerCondition()), FlatGround);
        }

        Assert.Equal(5, field.AliveCount);
        Assert.All(field.Enemies, e => Assert.InRange(e.Position.Length, field.SpawnMinDistance - 8f, field.SpawnMaxDistance));
    }

    [Fact]
    public void AnEnemyLeftFarBehind_IsBroughtBackIntoTheRing()
    {
        var field = QuietField();
        var straggler = field.Spawn(new Vector3D<float>(150f, 0f, 0f));

        field.Update(Step, new PlayerTarget(Vector3D<float>.Zero, true, new PlayerHealth(100f), new PlayerCondition()), FlatGround);

        Assert.True(straggler.Position.Length <= field.SpawnMaxDistance + 0.01f);
    }
}

public class PlayerHealthTests
{
    [Fact]
    public void AHit_IsFollowedByAShortGrace_ThenHitsLandAgain()
    {
        var health = new PlayerHealth(100f);

        Assert.True(health.TakeDamage(10f));
        Assert.False(health.TakeDamage(10f));
        health.Update(PlayerHealth.HitGrace + 0.01f);
        Assert.True(health.TakeDamage(10f));
        Assert.Equal(80f, health.Current);
    }

    [Fact]
    public void Health_StopsAtZero_AndRestoreFillsItBackUp()
    {
        var health = new PlayerHealth(30f);

        health.TakeDamage(500f);
        Assert.True(health.IsDead);
        Assert.Equal(0f, health.Current);

        health.Reset(30f);
        Assert.False(health.IsDead);
        Assert.Equal(30f, health.Current);
    }
}

public class GeometryTests
{
    [Theory]
    [InlineData(0f, 0f, 1f)]
    [InlineData(1f, 0f, 0f)]
    [InlineData(0.3f, -0.6f, -0.7f)]
    [InlineData(-0.5f, 0.8f, 0.2f)]
    public void YawPitch_TurnsAPlusZModelToFaceTheDirection_TheWayTheEngineDrawsIt(float x, float y, float z)
    {
        var direction = Vector3D.Normalize(new Vector3D<float>(x, y, z));
        var (yaw, pitch) = Geometry.YawPitch(direction);

        // The engine's placement: scale, then pitch about X, then yaw about Y (row vectors).
        var matrix = Matrix4X4.CreateRotationX(pitch) * Matrix4X4.CreateRotationY(yaw);
        var nose = Vector3D.Transform(new Vector3D<float>(0f, 0f, 1f), matrix);

        Assert.Equal(direction.X, nose.X, 4);
        Assert.Equal(direction.Y, nose.Y, 4);
        Assert.Equal(direction.Z, nose.Z, 4);
    }
}
