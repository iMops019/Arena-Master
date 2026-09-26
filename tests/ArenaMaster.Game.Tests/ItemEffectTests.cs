using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.Mage;
using ArenaMaster.Game.Paladin;
using ArenaMaster.Game.Progression;
using ArenaMaster.Game.Ranger;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

public class ItemCatalogTests
{
    private static RunItem Item(string id) => ItemCatalog.All.Single(i => i.Id == id);

    private static ItemBonuses With(params string[] ids)
    {
        var inventory = new ItemInventory();
        foreach (var id in ids)
        {
            inventory.Add(Item(id));
        }

        return inventory.Bonuses;
    }

    [Fact]
    public void ThereAreFortySixItems_EachUnique()
    {
        Assert.Equal(46, ItemCatalog.All.Count);
        Assert.Equal(46, ItemCatalog.All.Select(i => i.Id).Distinct().Count());
    }

    [Fact]
    public void EveryItem_ChangesTheBonuses()
    {
        var options = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
        string none = System.Text.Json.JsonSerializer.Serialize(new ItemBonuses(), options);
        foreach (var item in ItemCatalog.All)
        {
            var bonuses = new ItemBonuses();
            item.ApplyOne(bonuses);
            Assert.True(System.Text.Json.JsonSerializer.Serialize(bonuses, options) != none, $"{item.Name} does nothing");
        }
    }

    [Fact]
    public void EveryEpicAndLegendary_IsLockedBehindExactlyOneBounty()
    {
        foreach (var item in ItemCatalog.All.Where(i => i.Rarity >= ItemRarity.Epic))
        {
            Assert.Single(Bounties.All, b => b.UnlocksItem == item.Id);
        }

        Assert.All(Bounties.All.Where(b => b.UnlocksItem is not null), b => Assert.Contains(ItemCatalog.All, i => i.Id == b.UnlocksItem && i.Rarity >= ItemRarity.Epic));
        Assert.All(ItemCatalog.All.Where(i => i.Rarity < ItemRarity.Epic), i => Assert.True(Bounties.IsUnlocked(i, new Profile())));
    }

    [Fact]
    public void ClassBounties_OnlyCountRunsAsThatClass()
    {
        var profile = new Profile();
        var tree = new TreeProgress(SharpshooterTree.Tree, new TreeSave());
        var rangerWin = new RunRecord(3000, 10, 3, 1800f, true, 40, "ranger");

        var done = Bounties.Settle(rangerWin, profile, tree).Select(b => b.Id).ToList();

        Assert.Contains("deadeye_prize", done);
        Assert.DoesNotContain("dawnbringer", done);
        Assert.DoesNotContain("endless_winter", done);
        Assert.DoesNotContain("martyr", done);

        var mageWin = rangerWin with { ClassId = "mage" };
        Assert.Contains("endless_winter", Bounties.Settle(mageWin, profile, tree).Select(b => b.Id));
        Assert.True(Bounties.IsUnlocked(Item("staff_of_long_night"), profile));
        Assert.False(Bounties.IsUnlocked(Item("aegis_of_dawn"), profile));
    }

    [Fact]
    public void Block_ComesFromItems_ForEveryClass_AndTheSigilMultipliesIt()
    {
        var buckler = With("iron_buckler");
        var both = With("iron_buckler", "bulwark_sigil");

        Assert.Equal(0.04f, new RangerStats { Items = buckler }.BlockChance, 4);
        Assert.Equal(0.04f, new MageStats { Items = buckler }.BlockChance, 4);
        Assert.Equal(PaladinStats.BaseBlockChance + 0.04f, new PaladinStats { Items = buckler }.BlockChance(false, false), 4);
        Assert.Equal((PaladinStats.BaseBlockChance + 0.04f) * 1.25f, new PaladinStats { Items = both }.BlockChance(false, false), 4);
        Assert.Equal(0.05f, new RangerStats { Items = both }.BlockChance, 4);
    }

    [Fact]
    public void Area_WidensEachClassesAreas()
    {
        var censer = With("pilgrims_censer");

        Assert.Equal(PaladinStats.BaseNovaRadius * 1.2f, new PaladinStats { Items = censer }.NovaRadius, 3);
        Assert.Equal(PaladinStats.BaseCircleRadius * 1.2f, new PaladinStats { Items = censer }.CircleRadius, 3);
        Assert.Equal(MageStats.BaseBlastRadius * 1.2f, new MageStats { Items = censer }.BlastRadius, 3);
        Assert.Equal(RangerStats.RainRadius * 1.2f, new RangerStats { Items = censer }.RainPatch, 3);
    }

    [Fact]
    public void Duration_LengthensLingeringEffects()
    {
        var reliquary = With("blessed_reliquary");

        Assert.Equal(PaladinStats.BaseCircleDuration + 1f, new PaladinStats { Items = reliquary }.CircleDuration, 3);
        Assert.Equal(MageStats.BaseChillDuration + 1f, new MageStats { Items = reliquary }.ChillDuration, 3);
        Assert.Equal(MageStats.BaseShieldDuration + 1f, new MageStats { Items = reliquary }.ShieldDuration, 3);
    }

    [Fact]
    public void Projectiles_AreArrowsOrBolts_OrNovaDamageForThePaladin()
    {
        var prism = With("prism_shard");
        var crown = With("splintered_crown");

        Assert.Equal(2, new RangerStats { Items = prism }.ArrowsPerShot);
        Assert.Equal(MageStats.BaseProjectiles + 1, new MageStats { Items = prism }.Projectiles);
        Assert.Equal(PaladinStats.BaseNovaDamage * 1.12f, new PaladinStats { Items = prism }.NovaDamage, 3);

        Assert.Equal(3, new RangerStats { Items = crown }.ArrowsPerShot);
        Assert.Equal(RangerStats.BaseDamage * 0.85f, new RangerStats { Items = crown }.Damage, 3);
        Assert.Equal(MageStats.BaseBoltDamage * 0.85f, new MageStats { Items = crown }.BoltDamage, 3);
        Assert.Equal(PaladinStats.BaseNovaDamage * 1.24f, new PaladinStats { Items = crown }.NovaDamage, 3);
    }

    [Fact]
    public void WindWalkerBoots_RechargeEveryClassesShiftMove()
    {
        var boots = With("windwalker_boots");

        Assert.Equal(RangerStats.BaseDashCooldown / 1.25f, new RangerStats { Items = boots }.DashCooldown, 3);
        Assert.Equal(PaladinStats.BaseRushCooldown / 1.25f, new PaladinStats { Items = boots }.RushCooldown, 3);
        Assert.Equal(MageStats.BlinkCooldown / 1.25f, new MageStats { Items = boots }.BlinkRecharge, 3);
    }

    [Fact]
    public void WardingCrystal_StrengthensTheFrostShield()
    {
        var mage = new MageStats { Items = With("warding_crystal") };

        Assert.Equal((MageStats.ShieldFlat + MageStats.ShieldShare * MageStats.BaseMaxHealth) * 1.25f, mage.ShieldAmount, 3);
    }

    [Fact]
    public void Berserk_GrowsWithTheHealthMissing()
    {
        var band = With("berserkers_band");
        band.MissingHealth = 0.5f;

        Assert.Equal(0.175f, band.AttackSpeedNow, 4);
        Assert.Equal(RangerStats.BaseFireInterval / 1.175f, new RangerStats { Items = band }.FireInterval, 4);
    }
}

public class HitEffectTests
{
    private static EnemyField Field(HitEffects effects) => new(new Random(1)) { TargetCount = 0, HitEffects = effects };

    [Fact]
    public void AnItemsChill_SlowsWhatEveryHitLandsOn()
    {
        var field = Field(new HitEffects(ChillSlow: 0.1f, ChillSeconds: 1f));
        var enemy = field.Spawn(Vector3D<float>.Zero);

        field.Damage(enemy, 1f);

        Assert.True(enemy.IsChilled);
        Assert.Equal(0.1f, enemy.ChillSlow);
    }

    [Fact]
    public void ACertainFreeze_FreezesFodder_ButNeverABoss()
    {
        var field = Field(new HitEffects(FreezeChance: 1f, FreezeSeconds: 1f));
        var ghoul = field.Spawn(Vector3D<float>.Zero);
        var king = field.Spawn(new Vector3D<float>(5f, 0f, 0f), EnemyKind.HollowKing);

        field.Damage(ghoul, 1f);
        field.Damage(king, 1f);

        Assert.True(ghoul.IsFrozen);
        Assert.False(king.IsFrozen);
    }

    [Fact]
    public void ChilledFrozenAndEliteEnemies_TakeTheirMultipliers()
    {
        var field = Field(new HitEffects(ChilledMultiplier: 1.25f, FrozenMultiplier: 1.4f, EliteMultiplier: 1.5f));
        var plain = field.Spawn(Vector3D<float>.Zero);
        var chilled = field.Spawn(Vector3D<float>.Zero);
        chilled.Chill(5f, 0.1f);
        var frozen = field.Spawn(Vector3D<float>.Zero);
        frozen.Freeze(5f);
        var brute = field.Spawn(Vector3D<float>.Zero, EnemyKind.Brute);

        foreach (var enemy in new[] { plain, chilled, frozen, brute })
        {
            field.Damage(enemy, 10f);
        }

        Assert.Equal(EnemyKind.Ghoul.MaxHealth - 10f, plain.Health, 3);
        Assert.Equal(EnemyKind.Ghoul.MaxHealth - 12.5f, chilled.Health, 3);
        Assert.Equal(EnemyKind.Ghoul.MaxHealth - 17.5f, frozen.Health, 3);
        Assert.Equal(EnemyKind.Brute.MaxHealth - 15f, brute.Health, 3);
    }

    [Fact]
    public void DamageDealt_CountsWhatWasTaken_NotTheOverkill()
    {
        var field = Field(HitEffects.None);
        var enemy = field.Spawn(Vector3D<float>.Zero);

        field.Damage(enemy, 1000f);

        Assert.Equal(EnemyKind.Ghoul.MaxHealth, field.DamageDealt, 3);
    }

    [Fact]
    public void ACursedField_SpawnsTougherEnemies()
    {
        var field = Field(HitEffects.None);
        field.HealthBonus = 0.15f;

        Assert.Equal(EnemyKind.Ghoul.MaxHealth * 1.15f, field.Spawn(Vector3D<float>.Zero).MaxHealth, 3);
    }
}

public class ItemEffectsTests
{
    private static EnemyField Quiet() => new(new Random(1)) { TargetCount = 0 };

    private static void Run(ItemEffects effects, float seconds, ItemBonuses items, EnemyField field, PlayerHealth health, bool keepsBarrier = false,
        List<ItemHit>? hits = null)
    {
        for (float t = 0f; t < seconds - 1e-4f; t += 0.1f)
        {
            effects.Update(0.1f, Vector3D<float>.Zero, items, field, health, keepsBarrier, hits ?? new List<ItemHit>());
        }
    }

    [Fact]
    public void Thorns_StrikeWhateverTouchesThePlayer()
    {
        var field = Quiet();
        var touching = field.Spawn(new Vector3D<float>(EnemyKind.Ghoul.Radius + EnemyField.PlayerRadius, 0f, 0f));
        touching.Health = 1000f;
        var away = field.Spawn(new Vector3D<float>(3f, 0f, 0f));
        var hits = new List<ItemHit>();

        Run(new ItemEffects(), 0.75f, new ItemBonuses { Thorns = 4f }, field, new PlayerHealth(100f), hits: hits);

        Assert.Equal(2, hits.Count);
        Assert.All(hits, h => Assert.Equal(touching, h.Enemy));
        Assert.All(hits, h => Assert.Equal(4f, h.Damage));
        Assert.Equal(EnemyKind.Ghoul.MaxHealth, away.Health);
    }

    [Fact]
    public void TheWard_FormsHoldsAndFades()
    {
        var health = new PlayerHealth(100f);
        var effects = new ItemEffects();
        var items = new ItemBonuses { Ward = 20f };

        Run(effects, ItemEffects.FirstWard + 0.05f, items, Quiet(), health);
        Assert.Equal(20f, health.Barrier);
        Assert.True(effects.WardUp);

        Run(effects, ItemEffects.WardSeconds + 0.05f, items, Quiet(), health);
        Assert.Equal(0f, health.Barrier);
        Assert.False(effects.WardUp);
    }

    [Fact]
    public void TheWard_StaysAway_WhenTheClassKeepsItsOwnBarrier()
    {
        var health = new PlayerHealth(100f);

        Run(new ItemEffects(), 30f, new ItemBonuses { Ward = 20f }, Quiet(), health, keepsBarrier: true);

        Assert.Equal(0f, health.Barrier);
    }

    [Fact]
    public void Bloodstone_HealsFromTheDamageDealt()
    {
        var field = Quiet();
        var enemy = field.Spawn(Vector3D<float>.Zero);
        enemy.Health = 1000f;
        var health = new PlayerHealth(100f);
        health.TakeDamage(50f);
        var effects = new ItemEffects();
        var items = new ItemBonuses { LifePerDamage = 1f / 60f };

        field.Damage(enemy, 120f);
        effects.Update(0.1f, Vector3D<float>.Zero, items, field, health, false, new List<ItemHit>());

        Assert.Equal(52f, health.Current, 3);
    }

    [Fact]
    public void MartyrsCrown_PaysBlowsBack_AndHeals()
    {
        var field = Quiet();
        var attacker = field.Spawn(Vector3D<float>.Zero);
        attacker.Health = 1000f;
        var health = new PlayerHealth(100f);
        health.TakeDamage(50f);

        new ItemEffects().Answer(new[] { new Strike(attacker, 10f, false) }, Vector3D<float>.Zero, new ItemBonuses { Retaliation = 1.5f, HealPerBlow = 0.01f },
            field, health, new List<ItemHit>());

        Assert.Equal(985f, attacker.Health, 3);
        Assert.Equal(51f, health.Current, 3);
    }

    [Fact]
    public void TheAegis_BurstsOnABlock_AndOnlyOnABlock()
    {
        var field = Quiet();
        var near = field.Spawn(new Vector3D<float>(2f, 0f, 0f));
        near.Health = 1000f;
        var effects = new ItemEffects();
        var items = new ItemBonuses { BlockBurst = 25f };

        effects.Answer(new[] { new Strike(near, 10f, false) }, Vector3D<float>.Zero, items, field, new PlayerHealth(100f), new List<ItemHit>());
        Assert.Equal(1000f, near.Health);

        effects.Answer(new[] { new Strike(near, 10f, true) }, Vector3D<float>.Zero, items, field, new PlayerHealth(100f), new List<ItemHit>());
        Assert.Equal(975f, near.Health, 3);
        Assert.Single(effects.Bursts);
    }

    [Fact]
    public void ThePhoenix_BringsThePlayerBack_AfterTheClassesOwnLastStand()
    {
        var field = Quiet();
        var paladin = new PaladinClass(new Random(1));
        paladin.UseTree(new Dictionary<string, int> { [DefianceTree.UnbrokenVow] = 1 });
        var health = new PlayerHealth(100f);
        paladin.BeginRun(new ItemBonuses(), health);
        health.LastStands += 1;   // the Phoenix Feather
        var effects = new ItemEffects();

        health.TakeDamage(1000f);
        paladin.AnswerStrikes(Array.Empty<Strike>(), Vector3D<float>.Zero, field, health, new DamageNumbers());
        effects.Answer(Array.Empty<Strike>(), Vector3D<float>.Zero, new ItemBonuses(), field, health, new List<ItemHit>());
        Assert.Equal(1f + health.Max * PaladinStats.VowHeal, health.Current, 3);   // the vow answered the first

        health.Update(1f);
        health.TakeDamage(1000f);
        paladin.AnswerStrikes(Array.Empty<Strike>(), Vector3D<float>.Zero, field, health, new DamageNumbers());
        effects.Answer(Array.Empty<Strike>(), Vector3D<float>.Zero, new ItemBonuses(), field, health, new List<ItemHit>());
        Assert.Equal(1f + health.Max * ItemEffects.PhoenixHeal, health.Current, 3);   // the feather the second

        health.Update(1f);
        health.TakeDamage(1000f);
        Assert.True(health.IsDead);
    }
}
