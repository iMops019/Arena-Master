using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Gear;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.Progression;
using ArenaMaster.Game.Warrior;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

public class WarriorItemTests
{
    private static RunItem Item(string id) => ItemCatalog.All.Single(i => i.Id == id);

    private static ItemBonuses With(params string[] ids)
    {
        var inventory = new ItemInventory();
        foreach (string id in ids)
        {
            inventory.Add(Item(id));
        }

        return inventory.Bonuses;
    }

    private static EnemyField Field() => new(new Random(1)) { TargetCount = 0 };

    [Fact]
    public void ExecutionersHood_FinishesALowNonBoss_WhoeverHits()
    {
        var field = Field();
        field.HitEffects = ItemEffects.HitEffectsOf(With("executioners_hood"));
        var ghoul = field.Spawn(new Vector3D<float>(1f, 0f, 0f));
        ghoul.Health = ghoul.MaxHealth * 0.07f;
        var king = field.Spawn(new Vector3D<float>(2f, 0f, 0f), EnemyKind.HollowKing);
        king.Health = king.MaxHealth * 0.05f;

        Assert.True(field.Damage(ghoul, 0.01f));
        Assert.False(field.Damage(king, 0.01f));
    }

    [Fact]
    public void ButchersCleaver_HitsTheWoundedHarder()
    {
        var field = Field();
        field.HitEffects = ItemEffects.HitEffectsOf(With("butchers_cleaver"));
        var fresh = field.Spawn(new Vector3D<float>(1f, 0f, 0f));
        fresh.Health = 10_000f;
        var wounded = field.Spawn(new Vector3D<float>(2f, 0f, 0f));
        wounded.Health = wounded.MaxHealth * 0.4f;
        float before = wounded.Health;

        field.Damage(fresh, 1f);
        field.Damage(wounded, 1f);

        Assert.Equal(10_000f - 1f, fresh.Health, 3);   // above half its health: no bonus
        Assert.Equal(before - 1.3f, wounded.Health, 3);
    }

    [Fact]
    public void Bloodfury_KillsRaiseDamage_ForAWhile_UpToACap()
    {
        var items = With("bloodfury");
        var effects = new ItemEffects();
        effects.Begin();
        var field = Field();
        var health = new PlayerHealth(100f);
        for (int i = 0; i < 10; i++)
        {
            effects.OnKill(items);
        }

        effects.Update(0.1f, Vector3D<float>.Zero, items, field, health, false, new List<ItemHit>());
        Assert.Equal(1.1f, field.DamageBoost, 4);

        for (int i = 0; i < 50; i++)
        {
            effects.OnKill(items);
        }

        effects.Update(0.1f, Vector3D<float>.Zero, items, field, health, false, new List<ItemHit>());
        Assert.Equal(1f + ItemEffects.KillDamageCap, field.DamageBoost, 4);

        effects.Update(ItemEffects.KillSeconds, Vector3D<float>.Zero, items, field, health, false, new List<ItemHit>());
        Assert.Equal(1f, field.DamageBoost, 4);
    }

    [Fact]
    public void BerserkersHide_MoreDamage_AsHealthDrops()
    {
        var items = new ItemBonuses();
        GearCatalog.Find("berserkers_hide")!.Apply(items);
        var field = Field();
        var health = new PlayerHealth(100f);
        health.TakeDamage(50f);

        new ItemEffects().Update(0.01f, Vector3D<float>.Zero, items, field, health, false, new List<ItemHit>());

        Assert.Equal(1.15f, field.DamageBoost, 3);
    }

    [Fact]
    public void ParryingDagger_ABlockHeals()
    {
        var field = Field();
        var attacker = field.Spawn(new Vector3D<float>(1f, 0f, 0f));
        var health = new PlayerHealth(100f);
        health.TakeDamage(30f);

        new ItemEffects().Answer(new[] { new Strike(attacker, 10f, Blocked: true) }, Vector3D<float>.Zero, With("parrying_dagger"), field, health, new List<ItemHit>());

        Assert.Equal(72f, health.Current, 3);
    }

    [Fact]
    public void BerserkersTorc_IsAttackSpeedForOthers_AndRageForTheWarrior()
    {
        var items = With("berserkers_torc");
        Assert.Equal(0.08f, items.AttackSpeedNow, 4);   // what any other class reads

        var plain = new WarriorStats { Tree = BerserkerBonuses.From(new Dictionary<string, int> { [BerserkerTree.Berserking] = 1 }) };
        var torc = new WarriorStats { Tree = plain.Tree, Items = items };
        Assert.Equal(plain.SwingInterval, torc.SwingInterval, 5);   // no attack speed for him...
        Assert.Equal(plain.MaxRage + 5, torc.MaxRage);              // ...rage instead
        Assert.Equal(plain.RageDuration + 1f, torc.RageDuration, 4);
    }

    [Fact]
    public void WarPaint_AndLeatherGrips_GiveTheWarriorMore()
    {
        var plain = new WarriorStats();
        var kit = new WarriorStats { Items = With("war_paint", "leather_grips") };
        Assert.Equal(plain.MaxRage + 2, kit.MaxRage);
        Assert.Equal(plain.Reach * 1.05f, kit.Reach, 3);
        Assert.Equal(plain.CleaveDamage * 1.04f, kit.CleaveDamage, 3);
    }

    [Fact]
    public void HornOfFury_RageDrainsAPointAtATime()
    {
        var stats = new WarriorStats
        {
            Tree = BerserkerBonuses.From(new Dictionary<string, int> { [BerserkerTree.Berserking] = 1 }),
            Items = With("horn_of_fury"),
        };
        var fury = new Fury();
        fury.Gain(10, stats);

        fury.Update(stats.RageDuration + 0.01f, stats.RageDrains);
        Assert.Equal(9, fury.Rage);
        fury.Update(WarriorStats.DrainStep * 4f, stats.RageDrains);
        Assert.Equal(5, fury.Rage);

        var without = new Fury();
        without.Gain(10, stats);
        without.Update(stats.RageDuration + 0.01f);
        Assert.Equal(0, without.Rage);
    }

    [Fact]
    public void Bloodfury_GivesTheWarriorRage_PerKill()
    {
        var warrior = new WarriorClass(new Random(1));
        warrior.UseTree(new Dictionary<string, int> { [BerserkerTree.Berserking] = 1 });
        warrior.BeginRun(With("bloodfury"), new PlayerHealth(100f));
        var field = Field();

        warrior.OnKill(field.Spawn(new Vector3D<float>(1f, 0f, 0f)), 0f);
        warrior.OnKill(field.Spawn(new Vector3D<float>(2f, 0f, 0f)), 0f);

        Assert.Equal(2, warrior.Fury.Rage);
    }

    [Fact]
    public void TheWarriorBounties_UnlockItsEpicsAndLegendary()
    {
        var profile = new Profile();
        var tree = new TreeProgress(BerserkerTree.Tree, new TreeSave());
        Assert.False(Bounties.IsUnlocked(Item("butchers_cleaver"), profile));

        Bounties.Settle(new RunRecord(1600, 0, 0, 600f, false, 20, "shaman"), profile, tree);
        Assert.False(Bounties.IsUnlocked(Item("butchers_cleaver"), profile));   // not as the Warrior

        Bounties.Settle(new RunRecord(1600, 0, 0, 600f, false, 20, "warrior"), profile, tree);
        Assert.True(Bounties.IsUnlocked(Item("butchers_cleaver"), profile));
        Assert.False(Bounties.IsUnlocked(Item("bloodfury"), profile));
        Assert.False(Bounties.IsUnlocked(Item("horn_of_fury"), profile));
    }
}
