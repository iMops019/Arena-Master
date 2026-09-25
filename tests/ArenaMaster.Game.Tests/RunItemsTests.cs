using ArenaMaster.Game.Items;
using ArenaMaster.Game.Progression;
using ArenaMaster.Game.Ui;

namespace ArenaMaster.Game.Tests;

public class RunItemsTests
{
    private static RunItem Item(string id) => ItemCatalog.All.Single(i => i.Id == id);

    [Fact]
    public void AnItemFoundOnARun_GivesNothingInThatRun_ButGoesToTheChest()
    {
        var profile = new Profile();
        var run = new RunItems();
        run.Begin(Array.Empty<RunItem>());

        run.Find(Item("hunters_moon"), profile);

        Assert.Equal(1f, run.Carried.Bonuses.DamageMultiplier);   // no legendary boost mid-run
        Assert.Empty(run.Carried.Items);                           // and not on the HUD's carried list
        Assert.Equal(1, profile.CountOf("hunters_moon"));          // but it's in the chest
        Assert.Equal(new[] { Item("hunters_moon") }, run.Found);
    }

    [Fact]
    public void FindingMoreOfAnItemAlreadyBrought_DoesntStrengthenIt_ThisRun()
    {
        var profile = new Profile();
        profile.AddToStash("whetstone", 2);
        Loadout.Toggle(profile, "whetstone");
        var run = new RunItems();
        run.Begin(Loadout.ItemsToBring(profile));

        run.Find(Item("whetstone"), profile);
        run.Find(Item("whetstone"), profile);

        Assert.Equal(2, run.Carried.CountOf(Item("whetstone")));   // the two it set out with
        Assert.Equal(0.16f, run.Carried.Bonuses.Damage, 4);
        Assert.Equal(4, profile.CountOf("whetstone"));              // all four for next time
    }

    [Fact]
    public void TheNextRun_BringsWhatWasFound_OnceChosen()
    {
        var profile = new Profile();
        var first = new RunItems();
        first.Begin(Loadout.ItemsToBring(profile));
        first.Find(Item("rune_of_might"), profile);

        Loadout.Toggle(profile, "rune_of_might");
        var next = new RunItems();
        next.Begin(Loadout.ItemsToBring(profile));

        Assert.Equal(1.2f, next.Carried.Bonuses.DamageMultiplier, 4);
        Assert.Empty(next.Found);
    }

    [Fact]
    public void ARunsCarriedItems_AreOnlyTheLoadout()
    {
        var profile = new Profile();
        profile.AddToStash("whetstone", 3);
        profile.AddToStash("troll_heart", 1);   // owned but not chosen
        Loadout.Toggle(profile, "whetstone");
        var run = new RunItems();

        run.Begin(Loadout.ItemsToBring(profile));

        Assert.Equal(3, run.Carried.CountOf(Item("whetstone")));
        Assert.Equal(0, run.Carried.CountOf(Item("troll_heart")));
        Assert.Equal(0f, run.Carried.Bonuses.MaxHealth);
    }
}
