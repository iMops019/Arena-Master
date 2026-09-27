using System.Numerics;
using ArenaMaster.Game.Classes;
using ArenaMaster.Game.Mage;
using ArenaMaster.Game.Paladin;
using ArenaMaster.Game.Priest;
using ArenaMaster.Game.Ranger;
using ArenaMaster.Game.Shaman;
using ArenaMaster.Game.Warrior;
using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

/// <summary>The hero models (made by tools/hero_models.py) have what the game drives them with, and their run keeps its planted feet on the ground.</summary>
public class HeroModelTests
{
    public static readonly TheoryData<string> Models = new()
    {
        RangerController.BodyModel, PaladinController.BodyModel, MageController.BodyModel, ShamanController.BodyModel, WarriorController.BodyModel,
        PriestController.BodyModel,
    };

    public static readonly TheoryData<string, string> Attacks = new()
    {
        { PaladinController.BodyModel, PaladinController.AttackClip },
        { MageController.BodyModel, MageController.AttackClip },
        { ShamanController.BodyModel, ShamanController.AttackClip },
        { WarriorController.BodyModel, WarriorController.AttackClip },
        { PriestController.BodyModel, PriestController.AttackClip },
    };

    private static SkinnedModel Load(string file) => SkinnedModel.Load(Path.Combine(EngineAssets.RepoRoot, "assets", "models", file));

    [Theory]
    [MemberData(nameof(Models))]
    public void EveryHero_HasTheMovingClips_AndAChestToLayerOver(string file)
    {
        var model = Load(file);

        Assert.Contains(HeroMotion.IdleClip, model.Clips.Keys);
        Assert.Contains(HeroMotion.AirClip, model.Clips.Keys);
        foreach (var (clip, _) in HeroMotion.Runs)
        {
            Assert.Contains(clip, model.Clips.Keys);
            Assert.Equal(model.Clips["Run"].Duration, model.Clips[clip].Duration, 3);   // one stride, the same length whichever way
        }

        model.JointIndex(HeroBody.LayerJoint);
        Assert.InRange(model.Clips["Run"].Duration, 1.5f, 4f);                         // a stride of a few metres
    }

    [Theory]
    [MemberData(nameof(Models))]
    public void TheRun_NeverSinksAFootIntoTheGround_AndAlwaysHasOneNearIt(string file)
    {
        var model = Load(file);
        foreach (var (clip, _) in HeroMotion.Runs)
        {
            var run = model.Clips[clip];
            for (float t = 0f; t < run.Duration; t += 0.02f)
            {
                float lowest = MathF.Min(Lowest(model, run, t, "foot_l").Y, Lowest(model, run, t, "foot_r").Y);
                Assert.True(lowest > -0.015f, $"{clip} at {t:0.00}: a foot is {-lowest:0.000} m under the ground");
                Assert.True(lowest < 0.12f, $"{clip} at {t:0.00}: both feet are {lowest:0.000} m up");   // a run's flight is short and low
            }
        }
    }

    [Theory]
    [MemberData(nameof(Models))]
    public void APlantedFoot_GoesBackUnderTheBody_ExactlyAsFarAsTheBodyGoesForward(string file)
    {
        // The clip's time is the ground covered, in metres; a foot on the ground must move back relative to the body by just that much, or it slides.
        var model = Load(file);
        var run = model.Clips["Run"];
        var planted = new List<float>();
        for (float t = 0f; t < run.Duration; t += 0.01f)
        {
            if (Lowest(model, run, t, "foot_l").Y < 0.004f)
            {
                planted.Add(t);
            }
        }

        // The longest stretch of planted samples, and the middle of it (clear of the heel landing and the toe pushing off)
        var stretches = new List<(float Start, float End)>();
        foreach (float t in planted)
        {
            if (stretches.Count > 0 && t - stretches[^1].End < 0.015f)
            {
                stretches[^1] = (stretches[^1].Start, t);
            }
            else
            {
                stretches.Add((t, t));
            }
        }

        var (start, end) = stretches.MaxBy(s => s.End - s.Start);
        Assert.True(end - start > 0.4f, $"the left foot is only planted for {end - start:0.00} m of the stride");
        float t1 = start + 0.2f * (end - start), t2 = start + 0.6f * (end - start);
        float moved = Centre(model, run, t2, "foot_l").Z - Centre(model, run, t1, "foot_l").Z;
        Assert.Equal(-(t2 - t1), moved, 2);
    }

    [Theory]
    [MemberData(nameof(Attacks))]
    public void AnAttack_StartsAndEndsReady_AndMovesTheArmsInBetween(string file, string clip)
    {
        // The game holds the clip at 0 between blows and runs it to 1 through one, so both ends must be the same pose, and the blow (the middle) must not be.
        var model = Load(file);
        var attack = model.Clips[clip];
        Assert.Equal(1f, attack.Duration, 3);
        var start = new Matrix4x4[model.JointCount];
        var end = new Matrix4x4[model.JointCount];
        var blow = new Matrix4x4[model.JointCount];
        attack.Sample(0f, start);
        attack.Sample(1f, end);
        attack.Sample(0.55f, blow);
        var hand = new Vector3(0.3f, 0.82f, 0f);   // about where the right fist rests
        int j = model.JointIndex("hand_r");
        Assert.True(Vector3.Distance(Vector3.Transform(hand, start[j]), Vector3.Transform(hand, end[j])) < 0.01f);
        Assert.True(Vector3.Distance(Vector3.Transform(hand, start[j]), Vector3.Transform(hand, blow[j])) > 0.15f
            || Vector3.Distance(Vector3.Transform(new Vector3(-0.3f, 0.82f, 0f), start[model.JointIndex("hand_l")]),
                Vector3.Transform(new Vector3(-0.3f, 0.82f, 0f), blow[model.JointIndex("hand_l")])) > 0.15f);
    }

    [Fact]
    public void TheRangersShoot_NocksAnArrowOnlyWhileDrawing()
    {
        var model = Load(RangerController.BodyModel);
        var shoot = model.Clips[RangerController.ShootClip];
        Assert.Equal(1f, shoot.Duration, 3);

        float Size(float t)
        {
            var bones = new Matrix4x4[model.JointCount];
            shoot.Sample(t, bones);
            return new Vector3(bones[model.JointIndex("arrow")].M11, bones[model.JointIndex("arrow")].M12, bones[model.JointIndex("arrow")].M13).Length();
        }

        Assert.True(Size(0.05f) < 0.01f);   // just loosed: no arrow on the string
        Assert.True(Size(0.95f) > 0.99f);   // at full draw: an arrow on it

        // At full draw the string's middle is back by the jaw, well behind the bow hand
        var drawn = new Matrix4x4[model.JointCount];
        shoot.Sample(0.95f, drawn);
        // A joint's skinning matrix takes a point at its rest place to where it is now (the rest places are hero_models.py's).
        var nock = Vector3.Transform(new Vector3(-0.29f, 0.82f, -0.11f), drawn[model.JointIndex("nock")]);
        var bowHand = Vector3.Transform(new Vector3(-0.29f, 0.87f, 0f), drawn[model.JointIndex("hand_l")]);
        Assert.True(bowHand.Z - nock.Z > 0.5f, $"string {nock}, bow hand {bowHand}");
        Assert.InRange(nock.Y, 1.35f, 1.65f);
    }

    private static Vector3 Lowest(SkinnedModel model, AnimationClip clip, float t, string joint) =>
        Posed(model, clip, t, joint).MinBy(v => v.Y);

    private static Vector3 Centre(SkinnedModel model, AnimationClip clip, float t, string joint)
    {
        var points = Posed(model, clip, t, joint).ToList();
        return points.Aggregate(Vector3.Zero, (a, b) => a + b) / points.Count;
    }

    private static IEnumerable<Vector3> Posed(SkinnedModel model, AnimationClip clip, float t, string joint)
    {
        var bones = new Matrix4x4[model.JointCount];
        clip.Sample(t, bones);
        int j = model.JointIndex(joint);
        return BoundTo(model, j).Select(v => Vector3.Transform(v, bones[j])).ToList();
    }

    /// <summary>The rest positions of the vertices that move with joint <paramref name="joint"/>.</summary>
    private static IEnumerable<Vector3> BoundTo(SkinnedModel model, int joint)
    {
        const int stride = 19;
        for (int o = 0; o < model.Vertices.Length; o += stride)
        {
            if ((int)model.Vertices[o + 11] == joint && model.Vertices[o + 15] > 0.5f)
            {
                yield return new Vector3(model.Vertices[o], model.Vertices[o + 1], model.Vertices[o + 2]);
            }
        }
    }
}

/// <summary>Mixing the hero clips from where the feet go.</summary>
public class HeroMotionTests
{
    private const float Stride = 2.6f;
    private const float Idle = 4f;
    private const float Frame = 1f / 60f;

    private static List<ClipWeight> Mix(HeroMotion motion)
    {
        var into = new ClipWeight[6];
        return into.AsSpan(0, motion.Pose(into)).ToArray().ToList();
    }

    /// <summary>Runs the feet along <paramref name="velocity"/> (m/s, flat) for <paramref name="seconds"/>, the body facing +Z.</summary>
    private static Vector3D<float> Run(HeroMotion motion, Vector2D<float> velocity, float seconds, Vector3D<float> from = default, bool grounded = true)
    {
        var feet = from;
        for (int frame = 0, frames = (int)MathF.Round(seconds / Frame); frame < frames; frame++)
        {
            feet += new Vector3D<float>(velocity.X, 0f, velocity.Y) * Frame;
            motion.Update(feet, 0f, grounded, Frame, Stride, Idle);
        }

        return feet;
    }

    [Fact]
    public void StandingStill_IsTheIdle()
    {
        var motion = new HeroMotion();
        Run(motion, Vector2D<float>.Zero, 1f);

        var mix = Mix(motion);
        Assert.Equal(HeroMotion.IdleClip, Assert.Single(mix).Clip);
    }

    [Fact]
    public void RunningAhead_IsTheRun_ItsTimeTheGroundCovered()
    {
        var motion = new HeroMotion();
        Run(motion, new Vector2D<float>(0f, 5f), 0.5f);   // up to speed
        float before = Mix(motion).Single(c => c.Clip == "Run").Time;
        Run(motion, new Vector2D<float>(0f, 5f), 0.2f, new Vector3D<float>(0f, 0f, 2.5f));

        var run = Mix(motion).Where(c => c.Clip.StartsWith("Run")).MaxBy(c => c.Weight);
        Assert.Equal("Run", run.Clip);
        Assert.Equal(1f, Mix(motion).Sum(c => c.Weight), 3);
        Assert.Equal((before + 1f) % Stride, run.Time, 1);   // 5 m/s for 0.2 s is a metre on
    }

    [Theory]
    [InlineData(1f, 0f, "RunRight")]
    [InlineData(-1f, 0f, "RunLeft")]
    [InlineData(1f, 1f, "RunRightAhead")]
    [InlineData(-1f, 1f, "RunLeftAhead")]
    public void RunningToTheSide_MixesTheSideRuns(float x, float z, string clip)
    {
        var motion = new HeroMotion();
        Run(motion, Vector2D.Normalize(new Vector2D<float>(x, z)) * 6f, 1f);

        Assert.Equal(clip, Mix(motion).MaxBy(c => c.Weight).Clip);
        Assert.False(motion.Backward);
    }

    [Fact]
    public void RunningBackward_PlaysTheRunBackward()
    {
        var motion = new HeroMotion();
        Run(motion, new Vector2D<float>(0f, -5f), 1f);
        float before = Mix(motion).MaxBy(c => c.Weight).Time;
        Run(motion, new Vector2D<float>(0f, -5f), 0.1f, new Vector3D<float>(0f, 0f, -5f));

        Assert.True(motion.Backward);
        var run = Mix(motion).MaxBy(c => c.Weight);
        Assert.Equal("Run", run.Clip);
        Assert.Equal(((before - 0.5f) % Stride + Stride) % Stride, run.Time, 1);
    }

    [Fact]
    public void Stopping_FadesBackToTheIdle()
    {
        var motion = new HeroMotion();
        var feet = Run(motion, new Vector2D<float>(0f, 6f), 1f);
        Run(motion, Vector2D<float>.Zero, 0.2f, feet);
        Assert.Contains(Mix(motion), c => c.Clip == HeroMotion.IdleClip && c.Weight is > 0f and < 1f);   // on its way

        Run(motion, Vector2D<float>.Zero, 1f, feet);
        Assert.Equal(HeroMotion.IdleClip, Assert.Single(Mix(motion)).Clip);
    }

    [Fact]
    public void OffTheGround_TakesTheAirPose_OnlyAfterAMoment()
    {
        var motion = new HeroMotion();
        Run(motion, Vector2D<float>.Zero, 0.05f, grounded: false);
        Assert.DoesNotContain(Mix(motion), c => c.Clip == HeroMotion.AirClip);   // a step off a ledge isn't a jump

        Run(motion, Vector2D<float>.Zero, 0.5f, grounded: false);
        Assert.Equal(HeroMotion.AirClip, Assert.Single(Mix(motion)).Clip);
    }

    [Fact]
    public void ATeleport_IsNotARun()
    {
        var motion = new HeroMotion();
        Run(motion, Vector2D<float>.Zero, 0.5f);
        motion.Update(new Vector3D<float>(200f, 0f, 200f), 0f, true, Frame, Stride, Idle);
        Run(motion, Vector2D<float>.Zero, 0.1f, new Vector3D<float>(200f, 0f, 200f));

        Assert.Equal(0f, motion.Speed, 3);
        Assert.Equal(HeroMotion.IdleClip, Assert.Single(Mix(motion)).Clip);
    }

    [Fact]
    public void TheBodyFacing_SetsWhichWayIsAhead()
    {
        // Moving along +X with the body turned to face +X is running straight ahead, not to the side.
        var motion = new HeroMotion();
        var feet = Vector3D<float>.Zero;
        for (float t = 0f; t < 1f; t += Frame)
        {
            feet += new Vector3D<float>(6f * Frame, 0f, 0f);
            motion.Update(feet, MathF.PI / 2f, true, Frame, Stride, Idle);
        }

        Assert.Equal("Run", Mix(motion).MaxBy(c => c.Weight).Clip);
    }

    [Theory]
    [InlineData(0f, "Run", 1f)]
    [InlineData(22.5f, "RunRightAhead", 0.5f)]
    [InlineData(-67.5f, "RunLeft", 0.5f)]
    [InlineData(90f, "RunRight", 1f)]
    public void RunPair_WeighsTheTwoNearestRuns(float heading, string clip, float weight)
    {
        var (a, b) = HeroMotion.RunPair(heading, 1f);
        float found = (a.Clip == clip ? a.Weight : 0f) + (b.Clip == clip ? b.Weight : 0f);
        Assert.Equal(weight, found, 3);
        Assert.Equal(1f, a.Weight + b.Weight, 3);
    }
}
