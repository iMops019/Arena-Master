using ArenaMaster.Game.Combat;
using ArenaMaster.Game.Ranger;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

public class MajorNodeTests
{
    private static float? FlatGround(float x, float z) => MathF.Abs(x) < 200f && MathF.Abs(z) < 200f ? 0f : null;

    private static EnemyField QuietField() => new(new Random(1)) { TargetCount = 0 };

    private static RangerStats With(params (string Id, int Ranks)[] ranks) =>
        new() { Tree = SharpshooterBonuses.From(ranks.ToDictionary(r => r.Id, r => r.Ranks)) };

    private static List<ArrowHit> Fly(RangerArrows arrows, EnemyField enemies, float seconds = 1f)
    {
        var hits = new List<ArrowHit>();
        for (float t = 0f; t < seconds; t += 1f / 60f)
        {
            hits.AddRange(arrows.Update(1f / 60f, enemies, FlatGround, new List<Arrow>()));
        }

        return hits;
    }

    [Fact]
    public void Fork_SplitsTheArrowOnItsFirstHit_IntoTwoThatDontSplitAgain()
    {
        var enemies = QuietField();
        var first = enemies.Spawn(new Vector3D<float>(5f, 0f, 0f));
        first.Health = 1000f;   // survives, so the arrow stops in it and only the halves fly on
        var arrows = new RangerArrows(new Random(1));
        arrows.Fire(new Vector3D<float>(0f, 1f, 0f), new Vector3D<float>(1f, 0f, 0f), 50f, 60f, damage: 5f, rules: new HitRules(0, 0, 0, 0, 0, Fork: true));

        arrows.Update(0.2f, enemies, FlatGround, new List<Arrow>());   // the hit
        arrows.Update(1f / 60f, enemies, FlatGround, new List<Arrow>());

        Assert.Equal(2, arrows.Arrows.Count);
        Assert.All(arrows.Arrows, a => Assert.True(a.Forked));
        Assert.All(arrows.Arrows, a => Assert.Contains(first, a.AlreadyHit));   // the halves never hit the enemy that split them
        float degrees = MathF.Acos(Math.Clamp(Vector3D.Dot(arrows.Arrows[0].Heading, arrows.Arrows[1].Heading), -1f, 1f)) * 180f / MathF.PI;
        Assert.Equal(20f, degrees, 1);
    }

    [Fact]
    public void Fork_HalvesCanHitOtherEnemies()
    {
        var enemies = QuietField();
        var first = enemies.Spawn(new Vector3D<float>(5f, 0f, 0f));
        first.Health = 1000f;
        var side = enemies.Spawn(new Vector3D<float>(5f + 10f * MathF.Cos(10f * MathF.PI / 180f), 0f, 10f * MathF.Sin(10f * MathF.PI / 180f)));
        var arrows = new RangerArrows(new Random(1));
        arrows.Fire(new Vector3D<float>(0f, 1f, 0f), new Vector3D<float>(1f, 0f, 0f), 50f, 60f, damage: 5f, rules: new HitRules(0, 0, 0, 0, 0, Fork: true));

        var hits = Fly(arrows, enemies);

        Assert.Contains(hits, h => h.Enemy == side);
    }

    [Fact]
    public void StormOfSplinters_BurstsAKillIntoFourHalfDamageSplinters_ThatDontBurstAgain()
    {
        var enemies = QuietField();
        enemies.Spawn(new Vector3D<float>(5f, 0f, 0f)).Health = 1f;   // dies to the arrow
        var arrows = new RangerArrows(new Random(1));
        arrows.Fire(new Vector3D<float>(0f, 1f, 0f), new Vector3D<float>(1f, 0f, 0f), 50f, 60f, damage: 10f, rules: new HitRules(0, 0, 0, 0, 0, Splinters: true));

        arrows.Update(0.2f, enemies, FlatGround, new List<Arrow>());
        arrows.Update(1f / 60f, enemies, FlatGround, new List<Arrow>());

        Assert.Equal(RangerArrows.SplinterCount, arrows.Arrows.Count);
        Assert.All(arrows.Arrows, a =>
        {
            Assert.True(a.IsSplinter);
            Assert.Equal(10f * RangerArrows.SplinterDamage, a.Damage);
            Assert.Equal(0f, a.Heading.Y, 4);   // flat, out from the kill
        });
    }

    [Fact]
    public void ASplinterKill_DoesntSplinterAgain()
    {
        var enemies = QuietField();
        enemies.Spawn(new Vector3D<float>(5f, 0f, 0f)).Health = 1f;
        var ring = Enumerable.Range(0, 8).Select(i => i * MathF.Tau / 8f)
            .Select(a => enemies.Spawn(new Vector3D<float>(5f + 3f * MathF.Sin(a), 0f, 3f * MathF.Cos(a)))).ToList();
        ring.ForEach(e => e.Health = 1f);
        var arrows = new RangerArrows(new Random(1));
        arrows.Fire(new Vector3D<float>(0f, 1f, 0f), new Vector3D<float>(1f, 0f, 0f), 50f, 60f, damage: 10f, rules: new HitRules(0, 0, 0, 0, 0, Splinters: true));

        var hits = Fly(arrows, enemies, 2f);

        Assert.InRange(hits.Count(h => h.Killed), 2, 1 + RangerArrows.SplinterCount);   // the first kill, then at most one per splinter
    }

    [Fact]
    public void OneShotOneKill_TheFirstHitOnAnEnemyAlwaysCrits_ButNotTheNext()
    {
        var enemies = QuietField();
        enemies.Scaling = new EnemyScaling(Health: 100f, Damage: 1f, Speed: 1f);   // tough enough to take two hits
        enemies.Spawn(new Vector3D<float>(5f, 0f, 0f));
        var arrows = new RangerArrows(new Random(1));
        var rules = new HitRules(0, 0, 0, 0, 0, FirstHitCrits: true);
        arrows.Fire(new Vector3D<float>(0f, 1f, 0f), new Vector3D<float>(1f, 0f, 0f), 50f, 60f, damage: 5f, critChance: 0f, rules: rules);
        var first = Fly(arrows, enemies, 0.3f);
        arrows.Fire(new Vector3D<float>(0f, 1f, 0f), new Vector3D<float>(1f, 0f, 0f), 50f, 60f, damage: 5f, critChance: 0f, rules: rules);
        var second = Fly(arrows, enemies, 0.3f);

        Assert.True(Assert.Single(first).Crit);
        Assert.False(Assert.Single(second).Crit);
    }

    [Fact]
    public void SnipersFocus_ComesAfterStandingStill_AndIsSpentByAShot()
    {
        var stats = With((SharpshooterTree.SnipersFocus, 1));
        var cadence = new BowCadence();

        cadence.Update(RangerStats.FocusTime * 0.5f, standingStill: true);
        Assert.False(cadence.FocusReady(stats));
        cadence.Update(RangerStats.FocusTime * 0.6f, standingStill: true);
        Assert.True(cadence.FocusReady(stats));

        Assert.True(cadence.Shoot(stats).Focused);
        Assert.False(cadence.Shoot(stats).Focused);   // stand still again for the next
    }

    [Fact]
    public void SnipersFocus_IsLostByMoving_AndNeedsTheNode()
    {
        var cadence = new BowCadence();
        cadence.Update(2f, standingStill: true);
        Assert.False(cadence.FocusReady(new RangerStats()));

        var stats = With((SharpshooterTree.SnipersFocus, 1));
        cadence.Update(0.01f, standingStill: false);
        Assert.False(cadence.FocusReady(stats));
    }

    [Fact]
    public void EndlessQuiver_AddsTheRingOnEveryTenthShot()
    {
        var stats = With((SharpshooterTree.EndlessQuiver, 1));
        var cadence = new BowCadence();

        var rings = Enumerable.Range(1, 30).Where(_ => cadence.Shoot(stats).Ring).Count();

        Assert.Equal(3, rings);
        Assert.Equal(0, Enumerable.Range(1, 30).Count(_ => new BowCadence().Shoot(new RangerStats()).Ring));
    }

    [Fact]
    public void TheRing_IsEvenAndFlat()
    {
        var ring = RangerBow.Ring(RangerStats.QuiverRing).ToList();

        Assert.Equal(16, ring.Count);
        Assert.All(ring, d => Assert.Equal(0f, d.Y));
        Assert.Equal(0f, ring.Aggregate(Vector3D<float>.Zero, (a, b) => a + b).Length, 3);   // they balance out: evenly spread
    }

    [Fact]
    public void RainOfArrows_ComesEverySixSeconds_WithArrowstormAddingArrows()
    {
        var stats = With((SharpshooterTree.RainOfArrows, 1), ("storm", 2));
        var cadence = new BowCadence();
        int rains = 0;
        for (float t = 0f; t < 20f; t += 0.1f)
        {
            cadence.Update(0.1f, standingStill: false);
            if (cadence.RainDue(stats))
            {
                rains++;
            }
        }

        Assert.Equal(3, rains);   // at 6, 12 and 18 s
        Assert.Equal(RangerStats.BaseRainArrows + 8, stats.RainArrows);
        Assert.Equal(0, new RangerStats().RainArrows);
    }

    [Fact]
    public void RainDrops_FallOntoThePatchUnderTheCrosshair()
    {
        var centre = new Vector3D<float>(10f, 2f, -4f);

        var drops = RangerBow.RainDrops(centre, 12, new Random(3)).ToList();

        Assert.Equal(12, drops.Count);
        Assert.All(drops, d =>
        {
            Assert.True(d.Direction.Y < -0.9f, "falling steeply");
            float toGround = (d.Origin.Y - centre.Y) / -d.Direction.Y;
            var landing = d.Origin + d.Direction * toGround;
            Assert.True(Vector2D.Distance(new Vector2D<float>(landing.X, landing.Z), new Vector2D<float>(centre.X, centre.Z)) <= RangerStats.RainRadius + 0.01f);
        });
    }

    [Fact]
    public void TheMajors_TurnOnTheirEffects()
    {
        var stats = With((SharpshooterTree.Fork, 1), (SharpshooterTree.StormOfSplinters, 1), (SharpshooterTree.OneShotOneKill, 1));

        Assert.True(stats.HitRules.Fork);
        Assert.True(stats.HitRules.Splinters);
        Assert.True(stats.HitRules.FirstHitCrits);
        Assert.False(new RangerStats().HitRules.Fork);
    }
}
