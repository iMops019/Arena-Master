using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Combat;

/// <summary>An experience gem lying where an enemy died, or flying to the player once they come close enough.</summary>
internal sealed class XpGem
{
    public XpGem(int id, Vector3D<float> position, int value)
    {
        Id = id;
        Position = position;
        Value = value;
    }

    public int Id { get; }

    public Vector3D<float> Position { get; set; }

    public int Value { get; set; }

    /// <summary>Once the player has come within pickup range the gem is theirs: it flies to them however far they go.</summary>
    public bool Attracted { get; set; }

    /// <summary>Seconds since it started flying to the player - it speeds up the longer it chases.</summary>
    public float FlyingFor { get; set; }
}

/// <summary>
/// Experience gems on the ground: dropped where enemies die, pulled in when the player comes within their pickup radius, and collected on touch. Pure simulation;
/// <see cref="XpGemView"/> draws them.
/// </summary>
internal sealed class XpGemField
{
    /// <summary>How high above the ground a resting gem floats, metres.</summary>
    public const float FloatHeight = 0.45f;

    /// <summary>How close to the player's middle a gem has to come to be collected.</summary>
    public const float CollectDistance = 0.8f;

    /// <summary>Where on the player gems fly to: this high above the feet.</summary>
    public const float ChestHeight = 1.0f;

    /// <summary>
    /// Past this many gems on the ground, a new drop is added to the oldest resting gem instead of making another (it grows to match): a swarm's drops can't pile
    /// up without limit, and the experience isn't lost.
    /// </summary>
    public const int MaxGems = 400;

    private const float StartSpeed = 6f;
    private const float Acceleration = 30f;

    private readonly List<XpGem> _gems = new();
    private int _nextId = 1;

    public IReadOnlyList<XpGem> Gems => _gems;

    public XpGem Drop(Vector3D<float> groundPosition, int value)
    {
        if (_gems.Count >= MaxGems && _gems.FirstOrDefault(g => !g.Attracted) is { } oldest)
        {
            oldest.Value += value;
            return oldest;
        }

        var gem = new XpGem(_nextId++, groundPosition + new Vector3D<float>(0f, FloatHeight, 0f), value);
        _gems.Add(gem);
        return gem;
    }

    /// <summary>Moves the gems one frame. Returns the experience collected this frame; the collected gems are added to <paramref name="collected"/>.</summary>
    public int Update(float deltaSeconds, Vector3D<float> playerFeet, float pickupRadius, List<XpGem> collected)
    {
        var chest = playerFeet + new Vector3D<float>(0f, ChestHeight, 0f);
        int total = 0;

        foreach (var gem in _gems)
        {
            var toPlayer = chest - gem.Position;
            float distance = toPlayer.Length;

            if (!gem.Attracted && distance <= pickupRadius)
            {
                gem.Attracted = true;
            }

            if (gem.Attracted)
            {
                gem.FlyingFor += deltaSeconds;
                float step = (StartSpeed + Acceleration * gem.FlyingFor) * deltaSeconds;
                gem.Position = step >= distance ? chest : gem.Position + toPlayer / distance * step;
                distance = MathF.Max(0f, distance - step);
            }

            if (distance <= CollectDistance)
            {
                total += gem.Value;
                collected.Add(gem);
            }
        }

        _gems.RemoveAll(collected.Contains);
        return total;
    }

    /// <summary>Every gem on the ground starts flying to the player, wherever it is (a magnet).</summary>
    public void AttractAll()
    {
        foreach (var gem in _gems)
        {
            gem.Attracted = true;
        }
    }

    public List<XpGem> Clear()
    {
        var all = _gems.ToList();
        _gems.Clear();
        return all;
    }
}

/// <summary>Draws the <see cref="XpGemField"/> as spinning, bobbing crystals - one engine crowd, however many are on the ground.</summary>
internal sealed class XpGemView
{
    public const string Model = "xp_gem_placeholder.glb";

    private readonly List<CrowdInstance> _copies = new();
    private float _time;

    public void Sync(EngineWindow window, XpGemField field, float deltaSeconds)
    {
        _time += deltaSeconds;
        _copies.Clear();

        foreach (var gem in field.Gems)
        {
            var position = gem.Position;
            if (!gem.Attracted)
            {
                position.Y += 0.08f * MathF.Sin(_time * 3f + gem.Id);
            }

            float size = MathF.Min(3f, 1f + 0.35f * MathF.Log2(MathF.Max(1, gem.Value)));   // bigger gems for bigger enemies (and merged drops)
            _copies.Add(new CrowdInstance(position, _time * 2.5f + gem.Id, size));
        }

        window.SetCrowd(Model, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_copies));
    }
}
