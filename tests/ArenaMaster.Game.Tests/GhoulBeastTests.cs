using ArenaMaster.Game.Combat;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

public class GhoulBeastTests
{
    private const float Step = 1f / 60f;

    private static readonly Vector3D<float> North = new(0f, 0f, 1f);

    private static float? FlatGround(float x, float z) => MathF.Abs(x) < 200f && MathF.Abs(z) < 200f ? 0f : null;

    private static AttackSpec Pounce => EnemyKind.GhoulBeast.Attacks[0];

    /// <summary>
    /// Runs the field for <paramref name="seconds"/> with the player at the origin looking the way <paramref name="facing"/> says each frame (given the beast),
    /// returning the blows that reached them.
    /// </summary>
    private static List<Strike> Run(EnemyField field, Enemy beast, float seconds, Func<Enemy, Vector3D<float>> facing, PlayerCondition? condition = null,
        Action? watch = null)
    {
        var health = new PlayerHealth(1e9f);
        condition ??= new PlayerCondition();
        var strikes = new List<Strike>();
        for (float t = 0f; t < seconds - 1e-4f; t += Step)
        {
            health.Update(Step);
            condition.Update(Step);
            field.Update(Step, new PlayerTarget(Vector3D<float>.Zero, true, health, condition, 0f, facing(beast)), FlatGround);
            strikes.AddRange(field.Strikes);
            watch?.Invoke();
        }

        return strikes;
    }

    private static (EnemyField Field, Enemy Beast) Beast(Vector3D<float> at)
    {
        var field = new EnemyField(new Random(2)) { TargetCount = 0 };
        return (field, field.Spawn(at, EnemyKind.GhoulBeast));
    }

    private static Vector3D<float> LookAt(Enemy beast) => Geometry.FlatDirection(Vector3D<float>.Zero, beast.Position, out _);

    private static float Distance(Enemy beast) => new Vector2D<float>(beast.Position.X, beast.Position.Z).Length;

    [Fact]
    public void ItHas150Life_AndStalks()
    {
        Assert.Equal(150f, EnemyKind.GhoulBeast.MaxHealth);
        Assert.Equal(EnemyBehaviour.Stalk, EnemyKind.GhoulBeast.Behaviour);
        Assert.Equal(EnemyBehaviour.Chase, EnemyKind.Ghoul.Behaviour);
    }

    [Fact]
    public void KeptInSight_ItCirclesAtADistance_AndNeverPounces()
    {
        var (field, beast) = Beast(new Vector3D<float>(20f, 0f, 0f));
        float nearest = float.MaxValue;

        var strikes = Run(field, beast, 20f, LookAt, watch: () => nearest = MathF.Min(nearest, Distance(beast)));

        Assert.Empty(strikes);
        Assert.Null(beast.Attack);
        Assert.True(nearest > 6f, $"it came within {nearest:0.0} m");
    }

    [Fact]
    public void WithThePlayersBackTurned_ItComesInAndPounces()
    {
        var (field, beast) = Beast(new Vector3D<float>(0f, 0f, -20f));   // right behind a player looking north

        var strikes = Run(field, beast, 8f, _ => North);

        Assert.Contains(strikes, s => s.Attacker == beast && MathF.Abs(s.Damage - Pounce.Damage) < 1e-3f);
    }

    [Fact]
    public void ItWorksItsWayRoundBehind_ThenPounces()
    {
        var (field, beast) = Beast(new Vector3D<float>(12f, 0f, 0f));   // off to the side of a player looking north
        Vector3D<float>? pouncedFrom = null;

        var strikes = Run(field, beast, 20f, _ => North, watch: () => pouncedFrom ??= beast.Attack is null ? null : beast.AttackOrigin);

        Assert.NotEmpty(strikes);
        Assert.True(pouncedFrom!.Value.Z < 0f, "it pounced from behind");
    }

    [Fact]
    public void AfterAPounce_ItRunsOff()
    {
        var (field, beast) = Beast(new Vector3D<float>(0f, 0f, -20f));
        bool pounced = false;
        Run(field, beast, 8f, _ => North, watch: () => pounced |= beast.Attack is not null);
        Assert.True(pounced);

        // Straight after its recovery it is fleeing, and gets further off.
        while (beast.Attack is not null)
        {
            Run(field, beast, Step, _ => North);
        }

        Assert.Equal(StalkMood.Flee, beast.Mood);
        float before = Distance(beast);
        Run(field, beast, 1f, _ => North);
        Assert.True(Distance(beast) > before + 3f);
    }

    [Fact]
    public void TurningToFaceItUpClose_SpooksIt()
    {
        var (field, beast) = Beast(new Vector3D<float>(0f, 0f, 6f));
        beast.AttackCooldown = 0f;

        var strikes = Run(field, beast, 1f, LookAt);

        Assert.Empty(strikes);
        Assert.Equal(StalkMood.Flee, beast.Mood);
        Assert.True(Distance(beast) > 9f);
    }

    [Fact]
    public void AHit_SendsItRunning()
    {
        var (field, beast) = Beast(new Vector3D<float>(8f, 0f, 0f));
        Run(field, beast, Step, _ => Vector3D<float>.Zero);   // not knowing where the player looks: no opening, no spooking by sight
        Assert.Equal(StalkMood.Stalk, beast.Mood);

        field.Damage(beast, 10f);
        Run(field, beast, 1f, _ => Vector3D<float>.Zero);

        Assert.Equal(StalkMood.Flee, beast.Mood);
        Assert.True(Distance(beast) > 12f);
    }

    [Fact]
    public void AStunnedPlayer_IsAnOpening_EvenFacingIt()
    {
        var (field, beast) = Beast(new Vector3D<float>(0f, 0f, 12f));
        var condition = new PlayerCondition();
        condition.Stun(10f);

        var strikes = Run(field, beast, 5f, LookAt, condition);

        Assert.Contains(strikes, s => s.Attacker == beast);
    }

    [Fact]
    public void ItFacesTheWayItGoes()
    {
        var (field, beast) = Beast(new Vector3D<float>(20f, 0f, 0f));
        Run(field, beast, 3f, LookAt);
        var before = beast.Position;
        Run(field, beast, Step, LookAt);

        var moved = beast.Position - before;
        Assert.True(moved.Length > 1e-3f);
        Assert.Equal(MathF.Atan2(moved.X, moved.Z), beast.Yaw, 1);
    }

    [Fact]
    public void TheDirector_AddsBeastsFromSixMinutes()
    {
        Assert.Equal(0f, RunDirector.BeastShareAt(5.9f * 60f));
        Assert.Equal(0.015f, RunDirector.BeastShareAt(6f * 60f), 4);
        Assert.Equal(0.04f, RunDirector.BeastShareAt(25f * 60f), 4);
        Assert.Contains(RunDirector.MixAt(10f * 60f), m => m.Kind == EnemyKind.GhoulBeast && m.Share > 0f);
    }
}
