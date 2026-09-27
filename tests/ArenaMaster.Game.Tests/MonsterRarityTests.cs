using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Items;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

public class MonsterRarityTests
{
    private const float Step = 1f / 60f;

    private static float? FlatGround(float x, float z) => MathF.Abs(x) < 200f && MathF.Abs(z) < 200f ? 0f : null;

    private static List<Strike> Run(EnemyField field, PlayerHealth health, float seconds)
    {
        var strikes = new List<Strike>();
        var condition = new PlayerCondition();
        for (float t = 0f; t < seconds - 1e-4f; t += Step)
        {
            health.Update(Step);
            condition.Update(Step);
            field.Update(Step, new PlayerTarget(Vector3D<float>.Zero, true, health, condition), FlatGround);
            strikes.AddRange(field.Strikes);
        }

        return strikes;
    }

    [Theory]
    [InlineData(0.0, "Legendary")]
    [InlineData(0.0029, "Legendary")]
    [InlineData(0.004, "Rare")]
    [InlineData(0.02, "Magic")]
    [InlineData(0.078, "Normal")]
    [InlineData(0.9, "Normal")]
    public void TheOdds_PickTheRarestBandsFirst(double roll, string expected)
    {
        Assert.Equal(expected, RarityOdds.Standard.Pick(roll).ToString());
    }

    [Fact]
    public void EachRarity_IsTougherThanTheOneBelow()
    {
        var ladder = new[] { RarityTraits.Normal, RarityTraits.Magic, RarityTraits.Rare, RarityTraits.Legendary };
        for (int i = 1; i < ladder.Length; i++)
        {
            Assert.True(ladder[i].Health > ladder[i - 1].Health);
            Assert.True(ladder[i].Experience > ladder[i - 1].Experience);
            Assert.True(ladder[i].Damage >= ladder[i - 1].Damage);
            Assert.True(ladder[i].AttackSpeed >= ladder[i - 1].AttackSpeed);
            Assert.True(ladder[i].Speed >= ladder[i - 1].Speed);
        }

        // Magic is only more life.
        Assert.Equal(1f, RarityTraits.Magic.Damage);
        Assert.Equal(1f, RarityTraits.Magic.AttackSpeed);
        Assert.Equal(1f, RarityTraits.Magic.Speed);
        Assert.Equal(0f, RarityTraits.Magic.ChestChance);
        Assert.True(RarityTraits.Legendary.ChestChance > RarityTraits.Rare.ChestChance);
        Assert.True(RarityTraits.Rare.ChestChance > 0f);
    }

    [Fact]
    public void ALegendaryGhoul_HasItsRaritysNumbers()
    {
        var scaling = new EnemyScaling(2f, 1.5f, 1.1f);
        var ghoul = new Enemy(1, EnemyKind.Ghoul, Vector3D<float>.Zero, scaling, RarityTraits.Legendary);
        var legend = RarityTraits.Legendary;

        Assert.Equal(EnemyKind.Ghoul.MaxHealth * 2f * legend.Health, ghoul.MaxHealth, 3);
        Assert.Equal(ghoul.MaxHealth, ghoul.Health);
        Assert.Equal(EnemyKind.Ghoul.Speed * 1.1f * legend.Speed, ghoul.Speed, 3);
        Assert.Equal(1.5f * legend.Damage, ghoul.DamageScale, 3);
        Assert.Equal((int)MathF.Round(EnemyKind.Ghoul.Experience * legend.Experience), ghoul.Experience);
        Assert.Equal("Legendary Ghoul", ghoul.Name);
        Assert.Equal("Ghoul", new Enemy(2, EnemyKind.Ghoul, Vector3D<float>.Zero, EnemyScaling.None).Name);
    }

    [Fact]
    public void WithoutOdds_EverythingSpawnsNormal()
    {
        var field = new EnemyField(new Random(3)) { TargetCount = 0 };
        for (int i = 0; i < 500; i++)
        {
            Assert.Equal(MonsterRarity.Normal, field.Spawn(Vector3D<float>.Zero).Rarity.Rarity);
        }
    }

    [Fact]
    public void TheStandardOdds_GiveAboutTheirShares()
    {
        var field = new EnemyField(new Random(5)) { TargetCount = 0, RarityOdds = RarityOdds.Standard };
        var counts = new Dictionary<MonsterRarity, int>();
        const int n = 50_000;
        for (int i = 0; i < n; i++)
        {
            var rarity = field.Spawn(Vector3D<float>.Zero).Rarity.Rarity;
            counts[rarity] = counts.GetValueOrDefault(rarity) + 1;
        }

        Assert.InRange(counts[MonsterRarity.Magic] / (float)n, 0.05f, 0.07f);
        Assert.InRange(counts[MonsterRarity.Rare] / (float)n, 0.011f, 0.019f);
        Assert.InRange(counts[MonsterRarity.Legendary] / (float)n, 0.002f, 0.004f);
    }

    [Fact]
    public void BossesAndCrates_NeverRollARarity_ButElitesDo()
    {
        var field = new EnemyField(new Random(1)) { TargetCount = 0, RarityOdds = new RarityOdds(0f, 0f, 1f) };

        Assert.Equal(MonsterRarity.Normal, field.Spawn(Vector3D<float>.Zero, EnemyKind.HollowKing).Rarity.Rarity);
        Assert.Equal(MonsterRarity.Normal, field.Spawn(Vector3D<float>.Zero, DelveBosses.HollowKingUnbound).Rarity.Rarity);
        Assert.Equal(MonsterRarity.Normal, field.Spawn(Vector3D<float>.Zero, EnemyKind.Crate).Rarity.Rarity);
        Assert.Equal(MonsterRarity.Legendary, field.Spawn(Vector3D<float>.Zero, EnemyKind.Brute).Rarity.Rarity);
        Assert.Equal(MonsterRarity.Legendary, field.Spawn(Vector3D<float>.Zero, EnemyKind.BeastRider).Rarity.Rarity);
    }

    [Fact]
    public void ALegendarySpawn_IsReportedOnce_ForItsAnnouncement()
    {
        var field = new EnemyField(new Random(1)) { TargetCount = 0 };
        field.Spawn(Vector3D<float>.Zero, EnemyKind.Ghoul, MonsterRarity.Rare);
        var legend = field.Spawn(Vector3D<float>.Zero, EnemyKind.Ghoul, MonsterRarity.Legendary);

        Assert.Equal(legend, Assert.Single(field.TakeLegendarySpawns()));
        Assert.Empty(field.TakeLegendarySpawns());
    }

    [Fact]
    public void ALegendaryShooter_WindsUpFaster()
    {
        float LoosesAfter(MonsterRarity rarity)
        {
            var field = new EnemyField(new Random(1)) { TargetCount = 0 };
            field.Spawn(new Vector3D<float>(10f, 0f, 0f), EnemyKind.CrossbowGhoul, rarity).AttackCooldown = 0f;
            var health = new PlayerHealth(1e9f);
            float t = 0f;
            while (field.Bolts.Count == 0 && t < 5f)
            {
                Run(field, health, Step);
                t += Step;
            }

            return t;
        }

        float wind = EnemyKind.CrossbowGhoul.Attacks[0].WindUp;
        Assert.InRange(LoosesAfter(MonsterRarity.Normal), wind, wind + 0.05f);
        Assert.InRange(LoosesAfter(MonsterRarity.Legendary), wind / RarityTraits.Legendary.AttackSpeed, wind / RarityTraits.Legendary.AttackSpeed + 0.05f);
        Assert.InRange(LoosesAfter(MonsterRarity.Magic), wind, wind + 0.05f);   // Magic is no quicker
    }

    [Fact]
    public void ARareGhoul_ClawsHarderAndMoreOften()
    {
        List<Strike> Claws(MonsterRarity rarity)
        {
            var field = new EnemyField(new Random(1)) { TargetCount = 0 };
            field.Spawn(new Vector3D<float>(0.8f, 0f, 0f), EnemyKind.Ghoul, rarity);
            var health = new PlayerHealth(1e9f);
            var strikes = new List<Strike>();
            var condition = new PlayerCondition();
            for (float t = 0f; t < 10f; t += Step)
            {
                health.Update(Step);
                condition.Update(Step);
                field.Update(Step, new PlayerTarget(Vector3D<float>.Zero, true, health, condition), FlatGround);
                strikes.AddRange(field.Strikes);
            }

            return strikes;
        }

        var normal = Claws(MonsterRarity.Normal);
        var rare = Claws(MonsterRarity.Rare);
        Assert.Equal(EnemyKind.Ghoul.ContactDamage * RarityTraits.Rare.Damage, rare[0].Damage, 3);
        Assert.True(rare.Count >= normal.Count);
    }

    [Fact]
    public void TheDirectors_TurnTheOddsOn()
    {
        var field = new EnemyField(new Random(1));
        new RunDirector().Update(60f, field);
        Assert.Equal(RarityOdds.Standard, field.RarityOdds);
    }

    [Fact]
    public void OnlyRareAndLegendaryKills_CanLeaveAChest_TheLegendaryMoreOften()
    {
        var loot = new LootField(new Random(9));
        int Chests(RarityTraits rarity) => Enumerable.Range(0, 40_000).Count(_ => loot.RollRarityDrop(rarity));

        Assert.Equal(0, Chests(RarityTraits.Normal));
        Assert.Equal(0, Chests(RarityTraits.Magic));
        Assert.InRange(Chests(RarityTraits.Rare), 120, 290);          // 0.5%
        Assert.InRange(Chests(RarityTraits.Legendary), 1_800, 2_200);  // 5%
    }
}
