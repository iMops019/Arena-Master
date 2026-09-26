using System.Numerics;
using ArenaMaster.Game.Combat;
using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Tests;

/// <summary>The enemy models (made by tools/enemy_models.py) have the clips the game plays, and their walks keep the planted feet on the ground.</summary>
public class EnemyModelTests
{
    public static readonly TheoryData<string> Kinds = new()
    {
        EnemyKind.Ghoul.Name, EnemyKind.CrossbowGhoul.Name, EnemyKind.GhoulMage.Name, EnemyKind.BeastRider.Name, EnemyKind.GhoulTactician.Name,
        EnemyKind.Brute.Name, EnemyKind.HollowKing.Name, DelveBosses.HollowKingUnbound.Name,
    };

    private static readonly EnemyKind[] All =
    {
        EnemyKind.Ghoul, EnemyKind.CrossbowGhoul, EnemyKind.GhoulMage, EnemyKind.BeastRider, EnemyKind.GhoulTactician, EnemyKind.Brute, EnemyKind.HollowKing,
        DelveBosses.HollowKingUnbound,
    };

    private static EnemyKind Kind(string name) => All.Single(k => k.Name == name);

    private static SkinnedModel Load(string file) => SkinnedModel.Load(Path.Combine(EngineAssets.RepoRoot, "assets", "models", file));

    [Theory]
    [MemberData(nameof(Kinds))]
    public void EveryEnemy_HasItsMovingClips_AndOneForEachOfItsAttacks(string name)
    {
        var kind = Kind(name);
        var model = Load(kind.Model);

        foreach (var clip in new[] { EnemyMotion.IdleClip, EnemyMotion.WalkClip, EnemyMotion.DieClip })
        {
            Assert.Contains(clip, model.Clips.Keys);
        }

        var attacks = kind.Attacks.Concat(kind.Phases.SelectMany(p => p.Attacks)).Select(a => a.Type.ToString()).Distinct();
        foreach (var attack in attacks)
        {
            Assert.Contains(attack, model.Clips.Keys);
            Assert.Equal(1f, model.Clips[attack].Duration, 3);
        }

        if (kind.HeldModel is { } held)
        {
            var weapon = Load(held);
            Assert.Equal(model.JointCount, weapon.JointCount);                   // the same skeleton,
            Assert.Equal(model.Clips.Keys.Order(), weapon.Clips.Keys.Order());   // and the same clips, so it is drawn in the same pose
        }
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void TheWalk_NeverSinksAFoot_AndAlwaysHasOneOnTheGround(string name)
    {
        var model = Load(Kind(name).Model);
        var walk = model.Clips[EnemyMotion.WalkClip];
        int left = model.JointIndex("foot_l"), right = model.JointIndex("foot_r");
        for (float t = 0f; t < walk.Duration; t += 0.02f)
        {
            var bones = new Matrix4x4[model.JointCount];
            walk.Sample(t, bones);
            float lowest = MathF.Min(Lowest(model, bones, left), Lowest(model, bones, right));
            Assert.True(lowest > -0.02f, $"{name} at {t:0.00}: a foot is {-lowest:0.000} m under the ground");
            Assert.True(lowest < 0.02f, $"{name} at {t:0.00}: both feet are {lowest:0.000} m up (a walk always has one down)");
        }
    }

    [Fact]
    public void TheGhoulMagesFlame_GrowsThroughTheWindUp_AndIsGoneAsItIsThrown()
    {
        var model = Load(EnemyKind.GhoulMage.HeldModel!);
        var shoot = model.Clips[AttackType.Shoot.ToString()];
        int flame = model.JointIndex("flame");

        float Size(float t)
        {
            var bones = new Matrix4x4[model.JointCount];
            shoot.Sample(t, bones);
            return new Vector3(bones[flame].M11, bones[flame].M12, bones[flame].M13).Length();
        }

        Assert.True(Size(0.38f) > 1.5f * Size(0f));
        Assert.True(Size(0.6f) < 0.3f * Size(0f));
    }

    [Fact]
    public void TheTacticiansBomb_IsGoneOnceThrown_AndANewOneIsInHandByTheEnd()
    {
        var model = Load(EnemyKind.GhoulTactician.HeldModel!);
        var lob = model.Clips[AttackType.Lob.ToString()];
        int bomb = model.JointIndex("bomb");

        float Size(float t)
        {
            var bones = new Matrix4x4[model.JointCount];
            lob.Sample(t, bones);
            return new Vector3(bones[bomb].M11, bones[bomb].M12, bones[bomb].M13).Length();
        }

        Assert.True(Size(0.38f) >= Size(0f));
        Assert.True(Size(0.6f) < 0.1f * Size(0f));
        Assert.Equal(Size(0f), Size(0.99f), 1);
    }

    [Fact]
    public void TheBeast_AlwaysHasAPawOnTheGround_FrontAndBack()
    {
        var model = Load(EnemyKind.BeastRider.Model);
        var walk = model.Clips[EnemyMotion.WalkClip];
        int foreLeft = model.JointIndex("fore_foot_l"), foreRight = model.JointIndex("fore_foot_r");
        for (float t = 0f; t < walk.Duration; t += 0.02f)
        {
            var bones = new Matrix4x4[model.JointCount];
            walk.Sample(t, bones);
            float lowest = MathF.Min(Lowest(model, bones, foreLeft), Lowest(model, bones, foreRight));
            Assert.InRange(lowest, -0.02f, 0.02f);
        }
    }

    private static float Lowest(SkinnedModel model, Matrix4x4[] bones, int joint)
    {
        const int stride = 19;
        float lowest = float.MaxValue;
        for (int o = 0; o < model.Vertices.Length; o += stride)
        {
            if ((int)model.Vertices[o + 11] == joint)
            {
                lowest = MathF.Min(lowest, Vector3.Transform(new Vector3(model.Vertices[o], model.Vertices[o + 1], model.Vertices[o + 2]), bones[joint]).Y);
            }
        }

        return lowest;
    }
}

/// <summary>Choosing each enemy's clip from what it is doing.</summary>
public class EnemyMotionTests
{
    private const float Frame = 1f / 60f;

    private static readonly IReadOnlyDictionary<string, float> Clips = new Dictionary<string, float>
    {
        [EnemyMotion.IdleClip] = 3f, [EnemyMotion.WalkClip] = 1.6f, [EnemyMotion.DieClip] = 1f, ["Lunge"] = 1f,
    };

    private static Enemy Ghoul() => new(7, EnemyKind.Ghoul, Vector3D<float>.Zero, EnemyScaling.None);

    private static EnemyMotion.Pose Walk(EnemyMotion motion, Enemy enemy, float speed, float seconds)
    {
        EnemyMotion.Pose pose = default;
        for (int i = 0, n = (int)MathF.Round(seconds / Frame); i < n; i++)
        {
            enemy.Position += new Vector3D<float>(0f, 0f, speed * Frame);
            pose = motion.Update(enemy, Frame, Clips);
        }

        return pose;
    }

    [Fact]
    public void StandingStill_IsTheIdle()
    {
        var motion = new EnemyMotion();
        var pose = Walk(motion, Ghoul(), 0f, 0.5f);
        Assert.Equal(EnemyMotion.IdleClip, pose.Clip);
        Assert.Null(pose.From);
    }

    [Fact]
    public void Walking_MovesTheStrideOnByTheGroundCovered()
    {
        var motion = new EnemyMotion();
        var enemy = Ghoul();
        float before = Walk(motion, enemy, 3f, 1f).Time;
        var pose = Walk(motion, enemy, 3f, 0.2f);

        Assert.Equal(EnemyMotion.WalkClip, pose.Clip);
        Assert.Equal((before + 0.6f) % 1.6f, pose.Time, 2);   // 3 m/s for 0.2 s is 0.6 m on
    }

    [Fact]
    public void StartingToWalk_FadesOutOfTheIdle()
    {
        var motion = new EnemyMotion();
        var enemy = Ghoul();
        Walk(motion, enemy, 0f, 0.5f);
        var pose = Walk(motion, enemy, 3f, 0.05f);

        Assert.Equal(EnemyMotion.WalkClip, pose.Clip);
        Assert.Equal(EnemyMotion.IdleClip, pose.From);
        Assert.InRange(pose.Fade, 0.01f, 0.99f);
    }

    [Fact]
    public void AnAttack_LaysItsPhasesOverItsClip()
    {
        var lunge = EnemyKind.Brute.Attacks.First(a => a.Type == AttackType.Lunge);
        var enemy = new Enemy(1, EnemyKind.Brute, Vector3D<float>.Zero, EnemyScaling.None) { Attack = lunge, AttackPhase = AttackPhase.WindUp, PhaseTime = lunge.WindUp / 2f };
        Assert.Equal(EnemyMotion.WindUpEnd / 2f, EnemyMotion.AttackTime(enemy, lunge), 3);

        enemy.AttackPhase = AttackPhase.Active;
        enemy.PhaseTime = lunge.Active;
        Assert.Equal(EnemyMotion.ActiveEnd, EnemyMotion.AttackTime(enemy, lunge), 3);

        enemy.AttackPhase = AttackPhase.Recover;
        enemy.PhaseTime = lunge.Recover / 2f;
        Assert.Equal((EnemyMotion.ActiveEnd + 1f) / 2f, EnemyMotion.AttackTime(enemy, lunge), 3);

        var pose = new EnemyMotion().Update(enemy, Frame, Clips);
        Assert.Equal("Lunge", pose.Clip);
    }

    [Fact]
    public void Dying_PlaysTheDieClipOverTheDeath()
    {
        var motion = new EnemyMotion();
        var enemy = Ghoul();
        Walk(motion, enemy, 3f, 0.5f);
        enemy.Health = 0f;
        enemy.DeadFor = EnemyField.DeathDuration / 2f;

        var pose = motion.Update(enemy, Frame, Clips);

        Assert.Equal(EnemyMotion.DieClip, pose.Clip);
        Assert.Equal(0.5f, pose.Time, 3);
    }

    [Fact]
    public void AShoveFromTheCrowd_IsNotAWalk()
    {
        var motion = new EnemyMotion();
        var pose = Walk(motion, Ghoul(), 0.2f, 1f);
        Assert.Equal(EnemyMotion.IdleClip, pose.Clip);
    }
}
