using ArenaMaster.Game.Classes;
using ArenaMaster.Game.Mage;
using ArenaMaster.Game.Paladin;
using ArenaMaster.Game.Priest;
using ArenaMaster.Game.Progression;
using ArenaMaster.Game.Ranger;
using ArenaMaster.Game.Shaman;
using ArenaMaster.Game.Warrior;

namespace ArenaMaster.Game.Tests;

/// <summary>A class with more than one passive tree: choosing the active one, and the profile keeping each class's choice.</summary>
public class TreeChoiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"arenamaster-test-{Guid.NewGuid():N}");

    private string ProfilePath => Path.Combine(_directory, "profile.json");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static IEnumerable<IHeroClass> Classes()
    {
        var random = new Random(1);
        return new IHeroClass[]
        {
            new RangerClass(random), new PaladinClass(random), new MageClass(random), new ShamanClass(random), new WarriorClass(random), new PriestClass(random),
        };
    }

    [Fact]
    public void EveryClass_StartsOnItsFirstTree()
    {
        foreach (var hero in Classes())
        {
            Assert.NotEmpty(hero.Trees);
            Assert.Same(hero.Trees[0], hero.Tree);
            Assert.Equal(hero.Trees.Count, hero.Trees.Select(t => t.Id).Distinct().Count());
        }
    }

    [Fact]
    public void EveryClass_HasItsTwoTrees_TheFirstOneFirst()
    {
        var trees = Classes().ToDictionary(c => c.Id, c => c.Trees.Select(t => t.Id).ToArray());
        Assert.Equal(new[] { SharpshooterTree.TreeId, TrapperTree.TreeId }, trees["ranger"]);
        Assert.Equal(new[] { DefianceTree.TreeId, CrusadeTree.TreeId }, trees["paladin"]);
        Assert.Equal(new[] { FrostTree.TreeId, PyromancyTree.TreeId }, trees["mage"]);
        Assert.Equal(new[] { AlignmentTree.TreeId, EarthTree.TreeId }, trees["shaman"]);
        Assert.Equal(new[] { BerserkerTree.TreeId, ReaverTree.TreeId }, trees["warrior"]);
        Assert.Equal(new[] { UnholyTree.TreeId, GraveCallingTree.TreeId }, trees["priest"]);
    }

    [Fact]
    public void ChoosingATree_MakesItActive_AndAnUnknownIdFallsBackToTheFirst()
    {
        foreach (var hero in Classes())
        {
            foreach (var tree in hero.Trees)
            {
                hero.ChooseTree(tree.Id);
                Assert.Same(tree, hero.Tree);
                hero.UseTree(new Dictionary<string, int>());
            }

            hero.ChooseTree("no-such-tree");
            Assert.Same(hero.Trees[0], hero.Tree);
        }
    }

    [Fact]
    public void EachClassesActiveTree_ComesBackAsItWasSaved()
    {
        var profile = new Profile();
        profile.ActiveTrees["mage"] = "pyromancy";
        profile.ActiveTrees["ranger"] = SharpshooterTree.TreeId;

        ProfileStore.Save(profile, ProfilePath);
        var loaded = ProfileStore.Load(ProfilePath);

        Assert.Equal("pyromancy", loaded.ActiveTreeOf("mage"));
        Assert.Equal(SharpshooterTree.TreeId, loaded.ActiveTreeOf("ranger"));
        Assert.Null(loaded.ActiveTreeOf("priest"));
    }

    [Fact]
    public void AnOldSave_WithoutActiveTrees_Loads_WithEveryClassOnItsFirstTree()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(ProfilePath, """{ "Version": 1, "ActiveClass": "ranger", "ActiveTree": "sharpshooter" }""");

        var loaded = ProfileStore.Load(ProfilePath);

        Assert.Empty(loaded.ActiveTrees);
        Assert.Null(loaded.ActiveTreeOf("ranger"));
    }
}
