using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Gear;
using ArenaMaster.Game.Items;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

public class ArmourTests
{
    [Fact]
    public void Armour_CutsBlowsByAShare_ThatNeverReachesAll()
    {
        Assert.Equal(1f, Armour.Cut(0f));
        Assert.Equal(2f / 3f, Armour.Cut(100f), 4);   // a third off
        Assert.Equal(0.5f, Armour.Cut(200f), 4);      // half
        Assert.Equal(1f / 3f, Armour.Cut(400f), 4);   // two thirds
        Assert.True(Armour.Cut(100_000f) > 0f);
        Assert.Equal(1f, Armour.Cut(-50f));           // no negative armour
    }

    [Fact]
    public void EveryClassHasADefense_ThatGrowsWithItsTreeLevel()
    {
        Assert.Equal(0f, Armour.Defense(0));
        Assert.Equal(56f, Armour.Defense(7));
        Assert.True(Armour.Defense(28) > Armour.Defense(10));
        Assert.InRange(Armour.Reduction(Armour.Defense(7)), 0.2f, 0.25f);   // a level-7 tree: about a fifth off every blow
    }

    [Fact]
    public void EveryBodyArmour_GivesArmour_AndSaysSo()
    {
        foreach (var piece in GearCatalog.All.Where(p => p.Slot == GearSlot.BodyArmour))
        {
            var bonuses = new ItemBonuses();
            GearCatalog.Apply(GearCatalog.Perfect(piece), bonuses);
            Assert.True(bonuses.Armour > 0f, $"{piece.Name} gives no armour");
            Assert.Contains(piece.Stats, stat => stat.Line(stat.Max) == $"+{bonuses.Armour:0} armour");
        }

        Assert.All(GearCatalog.All.Where(p => p.Slot != GearSlot.BodyArmour), piece =>
        {
            var bonuses = new ItemBonuses();
            GearCatalog.Apply(GearCatalog.Perfect(piece), bonuses);
            Assert.Equal(0f, bonuses.Armour);
        });
    }

    [Fact]
    public void ArmouredHealth_TakesLessFromABlow()
    {
        var health = new PlayerHealth(100f) { DamageTaken = Armour.Cut(200f) };
        health.TakeDamage(40f);
        Assert.Equal(80f, health.Current, 3);
    }
}

public class FodderAttackCapTests
{
    private const float Step = 1f / 60f;

    private static float? FlatGround(float x, float z) => MathF.Abs(x) < 200f && MathF.Abs(z) < 200f ? 0f : null;

    private static void Run(EnemyField field, float seconds, Action? watch = null)
    {
        var health = new PlayerHealth(1e9f);
        var condition = new PlayerCondition();
        for (float t = 0f; t < seconds - 1e-4f; t += Step)
        {
            health.Update(Step);
            condition.Update(Step);
            field.Update(Step, new PlayerTarget(Vector3D<float>.Zero, true, health, condition), FlatGround);
            watch?.Invoke();
        }
    }

    [Theory]
    [InlineData("Crossbow Ghoul", 9f)]
    [InlineData("Ghoul Tactician", 10f)]
    [InlineData("Ghoul Beast Rider", 8f)]
    public void OnlySoManyFodder_WindUpTheSameAttackAtOnce(string name, float distance)
    {
        var kind = new[] { EnemyKind.CrossbowGhoul, EnemyKind.GhoulTactician, EnemyKind.BeastRider }.Single(k => k.Name == name);
        var type = kind.Attacks[0].Type;
        var field = new EnemyField(new Random(1)) { TargetCount = 0 };
        for (int i = 0; i < 40; i++)
        {
            float angle = MathF.Tau * i / 40f;
            field.Spawn(new Vector3D<float>(MathF.Sin(angle) * distance, 0f, MathF.Cos(angle) * distance), kind).AttackCooldown = 0f;
        }

        int most = 0;
        Run(field, 3f, () => most = Math.Max(most, field.Enemies.Count(e => e.Attack?.Type == type)));

        Assert.Equal(EnemyField.FodderAttackCap(type), most);   // as many as it allows, and no more
    }

    [Fact]
    public void ElitesAreNeverHeldBack()
    {
        var field = new EnemyField(new Random(1)) { TargetCount = 0 };
        for (int i = 0; i < 12; i++)
        {
            float angle = MathF.Tau * i / 12f;
            field.Spawn(new Vector3D<float>(MathF.Sin(angle) * 6f, 0f, MathF.Cos(angle) * 6f), EnemyKind.Brute).AttackCooldown = 0f;
        }

        Run(field, Step * 2);

        Assert.Equal(12, field.Enemies.Count(e => e.Attack is not null));
    }
}
