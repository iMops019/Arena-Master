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
        inventory.Add(Item("rune_of_might"));

        var bonuses = inventory.Bonuses;

        Assert.Equal(0.16f, bonuses.Damage, 4);
        Assert.Equal(1.44f, bonuses.DamageMultiplier, 4);
        Assert.Equal(2, inventory.CountOf(Item("whetstone")));
        Assert.Equal(2, inventory.Items.Count);   // one line per kind of item, in the order found
    }

    [Fact]
    public void DamageReduction_StacksWithoutEverReachingZero()
    {
        var inventory = new ItemInventory();
        for (int i = 0; i < 50; i++)
        {
            inventory.Add(Item("brigandine"));
        }

        Assert.InRange(inventory.Bonuses.DamageTaken, 0.01f, 0.1f);
    }

    [Fact]
    public void ItemsAndUpgrades_BothShowInTheRangersNumbers()
    {
        var stats = new RangerStats();
        stats.Increase(RangerUpgrade.SharpenedTips);   // +20%
        var inventory = new ItemInventory();
        inventory.Add(Item("whetstone"));                // +8%, added to the upgrade
        inventory.Add(Item("hunters_moon"));             // x1.35 on top, and +10% crit
        inventory.Add(Item("troll_heart"));
        inventory.Add(Item("serrated_edge"));
        stats.Items = inventory.Bonuses;

        Assert.Equal(RangerStats.BaseDamage * 1.28f * 1.35f, stats.Damage, 3);
        Assert.Equal(RangerStats.BaseCritChance + 0.10f, stats.CritChance, 4);
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
    public void ChestsTurnUpAroundThePlayer_OnATimer_UpToALimit()
    {
        var loot = new LootField(new Random(2));

        for (float t = 0f; t < LootField.FirstChestAt + LootField.ChestInterval * 10f; t += 0.5f)
        {
            loot.Update(0.5f, new Vector3D<float>(0f, 0f, 0f), FlatGround, new List<Chest>(), new List<ItemPickup>());
        }

        Assert.Equal(LootField.MaxWorldChests, loot.Chests.Count);
        Assert.All(loot.Chests, c => Assert.InRange(new Vector2D<float>(c.Position.X, c.Position.Z).Length, LootField.ChestMinDistance, LootField.ChestMaxDistance));
    }
}
