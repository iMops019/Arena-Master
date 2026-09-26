using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.Ranger;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

public class ItemTests
{
    private static RunItem Item(string id) => ItemCatalog.All.Single(i => i.Id == id);

    [Fact]
    public void EveryRarity_HasItems_AndIdsAreUnique()
    {
        foreach (var rarity in Enum.GetValues<ItemRarity>())
        {
            Assert.Contains(ItemCatalog.All, i => i.Rarity == rarity);
        }

        Assert.Equal(ItemCatalog.All.Count, ItemCatalog.All.Select(i => i.Id).Distinct().Count());
    }

    [Fact]
    public void Bonuses_AddUp_WhileMultipliers_Compound()
    {
        var inventory = new ItemInventory();
        inventory.Add(Item("whetstone"));
        inventory.Add(Item("whetstone"));
        inventory.Add(Item("rune_of_might"));
        inventory.Add(Item("glass_pendant"));

        var bonuses = inventory.Bonuses;

        Assert.Equal(0.16f, bonuses.Damage, 4);
        Assert.Equal(1.2f * 1.35f, bonuses.DamageMultiplier, 4);
        Assert.Equal(2, inventory.CountOf(Item("whetstone")));
        Assert.Equal(3, inventory.Items.Count);   // one line per kind of item, in the order found
    }

    [Fact]
    public void CritChance_ScalesEachClasssOwnBase()
    {
        Assert.Equal(0.10f, RangerStats.BaseCritChance);
        Assert.Equal(0.08f, Mage.MageStats.BaseCritChance);

        var ranger = new RangerStats { Items = new ItemBonuses { CritChance = 1f } };   // +100% increased
        var mage = new Mage.MageStats { Items = new ItemBonuses { CritChance = 1f } };
        Assert.Equal(0.20f, ranger.CritChance, 4);
        Assert.Equal(0.16f, mage.CritChance, 4);

        for (int i = 0; i < 5; i++)
        {
            ranger.Increase(RangerUpgrade.Deadeye);   // the whole level-up line: +100% more
        }

        Assert.Equal(0.30f, ranger.CritChance, 4);
    }

    [Fact]
    public void CopiesPastAnItemsCap_DontCount()
    {
        var inventory = new ItemInventory();
        for (int i = 0; i < 8; i++)
        {
            inventory.Add(Item("whetstone"));
            inventory.Add(Item("serrated_edge"));
            inventory.Add(Item("rune_of_might"));
        }

        Assert.Equal(8, inventory.CountOf(Item("whetstone")));   // owned, but only the cap's worth count
        Assert.Equal(0.08f * 3, inventory.Bonuses.Damage, 4);
        Assert.Equal(0.30f * 2, inventory.Bonuses.CritDamage, 4);
        Assert.Equal(1.2f, inventory.Bonuses.DamageMultiplier, 4);
    }

    [Fact]
    public void EveryItem_HasACap_CommonsThree_RaresTwo_TheRestOne()
    {
        Assert.All(ItemCatalog.All, i => Assert.InRange(i.MaxStack, 1, 3));
        Assert.All(ItemCatalog.All.Where(i => i.Rarity >= ItemRarity.Epic), i => Assert.Equal(1, i.MaxStack));
        Assert.Equal(3, Item("whetstone").MaxStack);
        Assert.Equal(2, Item("serrated_edge").MaxStack);
        Assert.Equal(1, Item("prism_shard").MaxStack);   // a projectile is a lot: its own cap
    }

    [Fact]
    public void AMaxedItem_DoesntDropAgain()
    {
        var profile = new Progression.Profile();
        var whetstone = Item("whetstone");
        Assert.True(Progression.Bounties.CanDrop(whetstone, profile));
        for (int i = 0; i < whetstone.MaxStack; i++)
        {
            profile.AddToStash(whetstone.Id);
        }

        Assert.False(Progression.Bounties.CanDrop(whetstone, profile));
        var random = new Random(5);
        Assert.All(Enumerable.Range(0, 300), _ => Assert.NotEqual(whetstone, ItemCatalog.Roll(random, RarityWeights.World, i => Progression.Bounties.CanDrop(i, profile))));
    }

    [Fact]
    public void ItemsAndUpgrades_BothShowInTheRangersNumbers()
    {
        var stats = new RangerStats();
        stats.Increase(RangerUpgrade.SharpenedTips);   // +20%
        var inventory = new ItemInventory();
        inventory.Add(Item("whetstone"));                // +8%, added to the upgrade
        inventory.Add(Item("hunters_moon"));             // x1.35 on top, and +40% increased crit
        inventory.Add(Item("troll_heart"));
        inventory.Add(Item("serrated_edge"));
        stats.Items = inventory.Bonuses;

        Assert.Equal(RangerStats.BaseDamage * 1.28f * 1.35f, stats.Damage, 3);
        Assert.Equal(RangerStats.BaseCritChance * 1.4f, stats.CritChance, 4);
        Assert.Equal(RangerStats.BaseCritMultiplier + 0.30f, stats.CritMultiplier, 4);
        Assert.Equal(RangerStats.BaseMaxHealth + 25f, stats.MaxHealth);

        stats.Reset();
        Assert.Equal(RangerStats.BaseDamage, stats.Damage);   // a new run starts empty-handed
    }

    [Fact]
    public void RarityOdds_FollowTheirWeights()
    {
        var random = new Random(5);
        var counts = new Dictionary<ItemRarity, int>();
        for (int i = 0; i < 20000; i++)
        {
            var rarity = RarityWeights.World.Roll(random);
            counts[rarity] = counts.GetValueOrDefault(rarity) + 1;
        }

        Assert.InRange(counts[ItemRarity.Common] / 20000f, 0.57f, 0.63f);
        Assert.InRange(counts[ItemRarity.Legendary] / 20000f, 0.01f, 0.03f);
        Assert.All(Enumerable.Range(0, 500), _ => Assert.True(RarityWeights.Boss.Roll(random) >= ItemRarity.Epic));
    }

    [Fact]
    public void DamageTaken_ScalesEveryHit()
    {
        var health = new PlayerHealth(100f) { DamageTaken = 0.5f };

        health.TakeDamage(20f);

        Assert.Equal(90f, health.Current);
    }
}

public class LootTests
{
    private static float? FlatGround(float x, float z) => MathF.Abs(x) < 200f && MathF.Abs(z) < 200f ? 0f : null;

    [Fact]
    public void WalkingIntoAChest_OpensIt_ForOneItem_ThenItGoes()
    {
        var loot = new LootField(new Random(1));
        loot.DropChest(new Vector3D<float>(1f, 0f, 0f), RarityWeights.Elite);

        var goneChests = new List<Chest>();
        var got = loot.Update(0.016f, Vector3D<float>.Zero, FlatGround, goneChests, new List<ItemPickup>());
        var more = loot.Update(0.016f, Vector3D<float>.Zero, FlatGround, goneChests, new List<ItemPickup>());

        var item = Assert.Single(got);
        Assert.True(item.Rarity >= ItemRarity.Rare);   // an elite's chest is never common
        Assert.Empty(more);                            // opened once only

        for (int i = 0; i < 40; i++)
        {
            loot.Update(0.016f, Vector3D<float>.Zero, FlatGround, goneChests, new List<ItemPickup>());
        }

        Assert.Empty(loot.Chests);
        Assert.Single(goneChests);
    }

    [Fact]
    public void AChestOutOfReach_StaysShut()
    {
        var loot = new LootField(new Random(1));
        loot.DropChest(new Vector3D<float>(5f, 0f, 0f), RarityWeights.World);

        var got = loot.Update(0.016f, Vector3D<float>.Zero, FlatGround, new List<Chest>(), new List<ItemPickup>());

        Assert.Empty(got);
        Assert.False(loot.Chests[0].Opened);
    }

    [Fact]
    public void ADroppedItem_IsPickedUpOnTouch()
    {
        var loot = new LootField(new Random(1));
        var pickup = loot.DropItem(new Vector3D<float>(0.5f, 0f, 0.5f));

        var gone = new List<ItemPickup>();
        var got = loot.Update(0.016f, Vector3D<float>.Zero, FlatGround, new List<Chest>(), gone);

        Assert.Equal(pickup.Item, Assert.Single(got));
        Assert.Single(gone);
        Assert.Empty(loot.Pickups);
    }

    [Fact]
    public void ChestsTurnUpAroundThePlayer_NowAndThen_UpToALimit()
    {
        var loot = new LootField(new Random(2));

        for (float t = 0f; t < LootField.FirstChestAt + LootField.ChestInterval * 400f; t += 1f)
        {
            loot.Update(0.5f, new Vector3D<float>(0f, 0f, 0f), FlatGround, new List<Chest>(), new List<ItemPickup>());
        }

        Assert.Equal(LootField.MaxWorldChests, loot.Chests.Count);
        Assert.All(loot.Chests, c => Assert.InRange(new Vector2D<float>(c.Position.X, c.Position.Z).Length, LootField.ChestMinDistance, LootField.ChestMaxDistance));
    }

    [Fact]
    public void Drops_AreAChance_NeverASureThing()
    {
        var loot = new LootField(new Random(3));
        int elites = Enumerable.Range(0, 20_000).Count(_ => loot.RollEliteDrop());
        int bosses = Enumerable.Range(0, 20_000).Count(_ => loot.RollBossDrop());
        Assert.InRange(elites / 20_000f, LootField.EliteDropChance * 0.7f, LootField.EliteDropChance * 1.3f);
        Assert.InRange(bosses / 20_000f, LootField.BossDropChance * 0.85f, LootField.BossDropChance * 1.15f);

        // One chance at a world chest: mostly none.
        int chests = Enumerable.Range(0, 2000).Count(seed =>
        {
            var field = new LootField(new Random(seed));
            field.Update(LootField.FirstChestAt + 0.1f, Vector3D<float>.Zero, FlatGround, new List<Chest>(), new List<ItemPickup>());
            return field.Chests.Count > 0;
        });
        Assert.InRange(chests / 2000f, LootField.WorldChestChance * 0.5f, LootField.WorldChestChance * 1.5f);
    }
}
