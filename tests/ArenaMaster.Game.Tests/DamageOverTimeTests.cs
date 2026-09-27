using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Gear;
using ArenaMaster.Game.Items;
using ArenaMaster.Game.Mage;
using ArenaMaster.Game.Paladin;
using ArenaMaster.Game.Priest;
using ArenaMaster.Game.Progression;
using ArenaMaster.Game.Ranger;
using ArenaMaster.Game.Shaman;
using ArenaMaster.Game.Warrior;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

public class DamageOverTimeNumberTests
{
    private static Enemy Ghoul(int id) => new(id, EnemyKind.Ghoul, Vector3D<float>.Zero, EnemyScaling.None);

    [Fact]
    public void Ticks_AreAddedUpForEachEnemy_AndShownOnceASecond()
    {
        var numbers = new DamageNumbers();
        var a = Ghoul(1);
        var b = Ghoul(2);
        for (int i = 0; i < 4; i++)
        {
            numbers.AddOverTime(a, Vector3D<float>.Zero, 3.6f, killed: false);
            numbers.AddOverTime(b, Vector3D<float>.Zero, 1f, killed: false);
            numbers.Update(0.2f);
        }

        Assert.Empty(numbers.Shown);   // 0.8 s: still adding up

        numbers.Update(0.25f);
        Assert.Equal(new[] { (14, true), (4, true) }, numbers.Shown.OrderByDescending(n => n.Amount));

        numbers.AddOverTime(a, Vector3D<float>.Zero, 5f, killed: false);
        numbers.Update(0.5f);
        Assert.Equal(2, numbers.Count);   // a fresh second started
    }

    [Fact]
    public void AKill_ShowsWhatWasAddedUp_AtOnce()
    {
        var numbers = new DamageNumbers();
        var ghoul = Ghoul(1);
        numbers.AddOverTime(ghoul, Vector3D<float>.Zero, 6f, killed: false);
        numbers.AddOverTime(ghoul, Vector3D<float>.Zero, 4f, killed: true);
        Assert.Equal(new[] { (10, true) }, numbers.Shown);
    }

    [Fact]
    public void AHit_IsStillShownAtOnce_AndIsntOverTime()
    {
        var numbers = new DamageNumbers();
        numbers.Add(Vector3D<float>.Zero, 12f, kill: false);
        Assert.Equal(new[] { (12, false) }, numbers.Shown);
    }
}

public class DamageOverTimeStatTests
{
    private static ItemBonuses Dot(float share) => new() { DotDamage = share };

    [Fact]
    public void EveryClassWithDamageOverTime_TakesTheStat()
    {
        Assert.Equal(new PriestStats().PlagueDamage * 1.5f, new PriestStats { Items = Dot(0.5f) }.PlagueDamage, 2);
        Assert.Equal(new PriestStats().DecayScale * 1.5f, new PriestStats { Items = Dot(0.5f) }.DecayScale, 3);
        Assert.Equal(new PaladinStats().CircleDps * 1.5f, new PaladinStats { Items = Dot(0.5f) }.CircleDps, 2);
        Assert.Equal(new ShamanStats().ZapDamage * 1.5f, new ShamanStats { Items = Dot(0.5f) }.ZapDamage, 2);
        Assert.Equal(new ShamanStats().RodDamage * 1.5f, new ShamanStats { Items = Dot(0.5f) }.RodDamage, 2);
        Assert.Equal(1.5f, new MageStats { Items = Dot(0.5f) }.Items.OverTime, 4);   // the Blizzard's bite is multiplied by it
    }

    [Fact]
    public void TheRangerAndWarrior_WhoHaveNone_GetNothingFromIt()
    {
        Assert.Equal(new RangerStats().Damage, new RangerStats { Items = Dot(0.5f) }.Damage, 3);
        Assert.Equal(new WarriorStats().CleaveDamage, new WarriorStats { Items = Dot(0.5f) }.CleaveDamage, 3);
    }

    [Fact]
    public void TheTome_MultipliesEveryonesDamageOverTime()
    {
        var tome = new ItemBonuses();
        ItemCatalog.All.Single(i => i.Id == "tome_of_pestilence").ApplyOne(tome);
        Assert.Equal(new PaladinStats().CircleDps * 1.12f * 1.25f, new PaladinStats { Items = tome }.CircleDps, 2);
    }

    [Fact]
    public void TheNewDamageOverTimeItemsAndGear_SayIt()
    {
        foreach (string id in new[] { "grave_dust", "storm_glass", "plague_doctors_mask" })
        {
            var item = ItemCatalog.All.Single(i => i.Id == id);
            var bonuses = new ItemBonuses();
            item.ApplyOne(bonuses);
            Assert.True(bonuses.DotDamage > 0f, id);
            Assert.Contains("damage over time", item.Description);
        }

        foreach (string id in new[] { "reliquary_of_rot", "sash_of_lingering_rites", "rotwood_band" })
        {
            var bonuses = new ItemBonuses();
            GearTesting.Best(id)(bonuses);
            Assert.True(bonuses.DotDamage > 0f, id);
        }
    }

    [Fact]
    public void AnOldReliquary_KeepsHowWellItRolled_OnItsNewStat()
    {
        var profile = new Profile();
        profile.Gear.Items.Add(new GearItem { Id = "old", Piece = "reliquary_of_rot", Rolls = new List<float> { 1.13f, 10f } });   // half-way wounded damage, best area
        profile.Gear.Items.Add(GearCatalog.RollCopy(GearCatalog.Find("reliquary_of_rot")!, new Random(3)));                        // a new one: left alone
        var fresh = profile.Gear.Items[1].Rolls.ToList();

        GearCatalog.Migrate(profile);

        var old = profile.Gear.Items[0];
        Assert.Equal(GearCatalog.Revision, old.Revision);
        Assert.Equal(18f, old.Rolls[0]);   // half-way up 10-25% damage over time (17.5), rounded to a whole percent
        Assert.Equal(10f, old.Rolls[1]);
        Assert.Equal(fresh, profile.Gear.Items[1].Rolls);

        GearCatalog.Migrate(profile);   // twice changes nothing more
        Assert.Equal(18f, old.Rolls[0]);
    }
}
