using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.World;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

public class CrateTests
{
    private const float Step = 1f / 60f;

    private static float? FlatGround(float x, float z) => MathF.Abs(x) < 200f && MathF.Abs(z) < 200f ? 0f : null;

    private static EnemyField QuietField() => new(new Random(1)) { TargetCount = 0 };

    private static void Run(EnemyField field, PlayerHealth health, float seconds, Vector3D<float>? feet = null)
    {
        for (float t = 0f; t < seconds; t += Step)
        {
            health.Update(Step);
            field.Update(Step, new PlayerTarget(feet ?? Vector3D<float>.Zero, true, health, new PlayerCondition(), BlockChance: 1f), FlatGround);
        }
    }

    [Fact]
    public void ACrate_JustStandsThere_EvenAgainstThePlayer()
    {
        var field = QuietField();
        var crate = field.Spawn(new Vector3D<float>(0.8f, 0f, 0f), EnemyKind.Crate);
        crate.Yaw = 1f;
        var health = new PlayerHealth(100f);

        Run(field, health, 3f);

        Assert.Equal(0.8f, crate.Position.X, 3);
        Assert.Equal(1f, crate.Yaw);
        Assert.Equal(100f, health.Current);
        Assert.Empty(field.Strikes);   // not even a blocked blow for a shield to answer
    }

    [Fact]
    public void BreakingACrate_IsNotAKill_AndItDoesntCountTowardTheSwarm()
    {
        var field = new EnemyField(new Random(1)) { TargetCount = 3, SpawnInterval = 0f };
        var crate = field.Spawn(new Vector3D<float>(5f, 0f, 0f), EnemyKind.Crate);
        Run(field, new PlayerHealth(1e6f), 0.1f);
        Assert.Equal(3, field.AliveCount);   // three ghouls on top of the crate

        Assert.True(field.Damage(crate, 1000f));

        Assert.Equal(0, field.Kills);
        Assert.Contains(crate, field.TakeNewlyKilled());
    }

    [Fact]
    public void Crates_TurnUpAroundThePlayer_OnATimer_UpToALimit()
    {
        var field = QuietField();
        var crates = new CrateField(new Random(2));

        for (float t = 0f; t < CrateField.FirstCrate + 0.05f; t += 0.1f)
        {
            crates.Update(0.1f, Vector3D<float>.Zero, field, FlatGround);
        }

        var first = Assert.Single(field.Enemies, e => e.Kind.IsProp);
        Geometry.FlatDirection(Vector3D<float>.Zero, first.Position, out float distance);
        Assert.InRange(distance, CrateField.MinDistance, CrateField.MaxDistance);

        for (float t = 0f; t < 20f * CrateField.CrateInterval; t += 0.1f)
        {
            crates.Update(0.1f, Vector3D<float>.Zero, field, FlatGround);
        }

        Assert.Equal(CrateField.MaxCrates, field.Enemies.Count(e => e.Kind.IsProp));
    }

    [Fact]
    public void ACrateLeftFarBehind_Goes()
    {
        var field = QuietField();
        var crate = field.Spawn(new Vector3D<float>(5f, 0f, 0f), EnemyKind.Crate);
        var crates = new CrateField(new Random(2));

        crates.Update(0.1f, new Vector3D<float>(100f, 0f, 0f), field, FlatGround);
        Run(field, new PlayerHealth(100f), 0.1f, new Vector3D<float>(100f, 0f, 0f));

        Assert.DoesNotContain(crate, field.Enemies);
        Assert.Equal(0, field.Kills);
        Assert.Empty(field.TakeNewlyKilled());   // gone quietly: nothing to drop
    }

    [Fact]
    public void ABrokenCrate_LeavesAPickup_TakenByWalkingOverIt()
    {
        var crates = new CrateField(new Random(1));
        PickupKind kind;
        do
        {
            kind = crates.Break(new Vector3D<float>(10f, 0f, 0f));
        }
        while (kind == PickupKind.BigGem);

        var pickup = Assert.Single(crates.Pickups);
        Assert.Equal(kind, pickup.Kind);
        Assert.Empty(crates.Collect(new Vector3D<float>(7f, 0f, 0f)));   // not close enough

        Assert.Equal(new[] { kind }, crates.Collect(new Vector3D<float>(9.2f, 0f, 0f)));
        Assert.Empty(crates.Pickups);
    }

    [Fact]
    public void ABigGem_IsNotLeftAsAPickup()
    {
        var crates = new CrateField(new Random(1));
        for (int i = 0; i < 200; i++)
        {
            if (crates.Break(Vector3D<float>.Zero) == PickupKind.BigGem)
            {
                Assert.Equal(i, crates.Pickups.Count);   // every break before it left one; this one didn't
                return;
            }
        }

        Assert.Fail("a big gem never came up");
    }

    [Fact]
    public void EveryKindOfPickup_TurnsUp_RoughlyAsOftenAsItsOdds()
    {
        var random = new Random(9);
        var counts = new Dictionary<PickupKind, int>();
        const int Rolls = 20_000;
        for (int i = 0; i < Rolls; i++)
        {
            var kind = CrateField.Roll(random);
            counts[kind] = counts.GetValueOrDefault(kind) + 1;
        }

        float total = CrateField.Odds.Sum(o => o.Weight);
        foreach (var (kind, weight) in CrateField.Odds)
        {
            Assert.InRange(counts.GetValueOrDefault(kind) / (float)Rolls, weight / total - 0.02f, weight / total + 0.02f);
        }
    }

    [Fact]
    public void AMagnet_PullsInEveryGem_HoweverFar()
    {
        var gems = new XpGemField();
        gems.Drop(new Vector3D<float>(40f, 0f, 0f), 3);
        gems.Drop(new Vector3D<float>(-25f, 0f, 30f), 2);
        gems.AttractAll();
        int gained = 0;

        for (float t = 0f; t < 5f; t += Step)
        {
            gained += gems.Update(Step, Vector3D<float>.Zero, 3f, new List<XpGem>());
        }

        Assert.Equal(5, gained);
        Assert.Empty(gems.Gems);
    }

    [Fact]
    public void AFrenzy_AddsToAttackSpeed_ForEveryClass()
    {
        var items = new ItemBonuses { AttackSpeed = 0.1f, Frenzy = 0.4f };

        Assert.Equal(0.5f, items.AttackSpeedNow, 4);
        Assert.Equal(Ranger.RangerStats.BaseFireInterval / 1.5f, new Ranger.RangerStats { Items = items }.FireInterval, 4);
    }
}
