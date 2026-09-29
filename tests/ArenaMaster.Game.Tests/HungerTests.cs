using ArenaMaster.Game.World;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

/// <summary>The Hungering Maw (World/Hunger).</summary>
public class HungerTests
{
    private static readonly Vector3D<float> Here = new(10f, 18f, 10f);

    private static Hunger Opened(float runSeconds = 600f)
    {
        var hunger = new Hunger(new Random(3));
        Assert.Equal(HungerNews.None, hunger.Update(hunger.NextIn - 1f, runSeconds, true, () => Here));
        Assert.Equal(HungerNews.None, hunger.Update(2f, runSeconds, allowed: false, () => Here));   // due, but not allowed
        Assert.Equal(HungerNews.Opened, hunger.Update(0.1f, runSeconds, true, () => Here));
        return hunger;
    }

    [Fact]
    public void ItOpens_AFewMinutesIn_WantingMoreKillsTheLaterItIs()
    {
        var hunger = new Hunger(new Random(1));
        Assert.InRange(hunger.NextIn, Hunger.FirstMin, Hunger.FirstMax);
        Assert.Equal(30, Hunger.NeedAt(0f));
        Assert.Equal(50, Hunger.NeedAt(600f));
        Assert.Equal(90, Hunger.NeedAt(3600f));

        var open = Opened(600f);
        Assert.True(open.IsOpen);
        Assert.Equal(Here, open.Centre);
        Assert.Equal(50, open.Need);
        Assert.Equal(Hunger.OpenSeconds, open.Left);
        Assert.Equal((int)(100 * Hunger.CrowdMultiplier) + Hunger.CrowdExtra, open.Crowd(100));
    }

    [Fact]
    public void KillsInItsRing_FeedIt_AndFilledInTime_ItIsSated()
    {
        var hunger = Opened(0f);
        Assert.False(hunger.Feed(Here + new Vector3D<float>(Hunger.Reach + 1f, 0f, 0f)));   // too far
        for (int i = 0; i < hunger.Need; i++)
        {
            Assert.True(hunger.Feed(Here + new Vector3D<float>(5f, 0f, 0f)));
        }

        Assert.Equal(hunger.Need, hunger.Fed);
        Assert.NotEmpty(hunger.Motes);
        Assert.False(hunger.Feed(Here));   // full
        Assert.Equal(HungerNews.Sated, hunger.Update(0.1f, 60f, true, () => Here));
        Assert.False(hunger.IsOpen);
        Assert.InRange(hunger.NextIn, Hunger.GapMin, Hunger.GapMax);
        Assert.False(hunger.Feed(Here));   // shut
    }

    [Fact]
    public void LeftHungry_ItStarves()
    {
        var hunger = Opened();
        hunger.Feed(Here);
        Assert.Equal(HungerNews.None, hunger.Update(Hunger.OpenSeconds - 1f, 600f, true, () => Here));
        Assert.Equal(HungerNews.Starved, hunger.Update(1.5f, 600f, true, () => Here));
        Assert.False(hunger.IsOpen);
    }

    [Fact]
    public void AMote_FliesIntoTheMaw_AndIsGone()
    {
        var hunger = Opened();
        hunger.Feed(Here + new Vector3D<float>(3f, 0f, 0f));
        Assert.Single(hunger.Motes);
        hunger.Update(Hunger.MoteSeconds * 0.5f, 600f, true, () => Here);
        Assert.Equal(0.5f, hunger.Motes.Single().Through, 2);
        hunger.Update(Hunger.MoteSeconds, 600f, true, () => Here);
        Assert.Empty(hunger.Motes);
    }
}
