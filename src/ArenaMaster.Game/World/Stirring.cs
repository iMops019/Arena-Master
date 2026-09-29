using ArenaMaster.Game.Combat;
using Silk.NET.Maths;

namespace ArenaMaster.Game.World;

/// <summary>Where the Stirring is: waiting for the next, a mini boss stirring at a marked spot (get there), or one awake and on the field.</summary>
internal enum StirringPhase
{
    Waiting,
    Stirring,
    Awake,
}

/// <summary>What the Stirring did this frame, for the run to answer.</summary>
internal enum StirringNews
{
    None,

    /// <summary>A mini boss began to stir: announce it and mark the spot.</summary>
    Began,

    /// <summary>The count ran out: spawn the mini boss at the spot now.</summary>
    Woke,
}

/// <summary>
/// A random event of the cave (the user's, 2026-09-29): "something is stirring - get there". Every few minutes one of the <see cref="MiniBosses"/> begins to
/// stir at a spot in another chamber, a way off along the floor: the spot is marked, an arrow points the way, and a count runs down; when it ends the mini boss
/// wakes there, whether the player made it or not (it comes for them either way). One at a time: the next is due a while after the last woke, and waits while a
/// mini boss is still on the field. The first comes <see cref="FirstMin"/> to <see cref="FirstMax"/> seconds into a run. Pure: it keeps the clocks and the choice;
/// the run picks the spot, spawns, draws and pays.
/// </summary>
internal sealed class Stirring
{
    public const float FirstMin = 120f;
    public const float FirstMax = 200f;

    /// <summary>How long after one wakes the next is due (between the two).</summary>
    public const float GapMin = 180f;
    public const float GapMax = 300f;

    /// <summary>How long it stirs before it wakes: the time to get there.</summary>
    public const float StirSeconds = 40f;

    private readonly Random _random;
    private float _next;

    public Stirring(Random random)
    {
        _random = random;
        Reset();
    }

    public StirringPhase Phase { get; private set; }

    /// <summary>The mini boss stirring (or awake), and where.</summary>
    public EnemyKind? Kind { get; private set; }

    public Vector2D<float> Spot { get; private set; }

    /// <summary>The chamber it stirs in, for the announcement ("in the Bone Hall"), or null for a tunnel.</summary>
    public string? Where { get; private set; }

    /// <summary>Seconds until it wakes, while it stirs.</summary>
    public float WakesIn { get; private set; }

    /// <summary>Seconds until the next is due, while waiting.</summary>
    public float NextIn => _next;

    /// <summary>The mini boss once awake, until it dies.</summary>
    public Enemy? Awake { get; private set; }

    /// <summary>A new run: nothing stirring, the first due a while in.</summary>
    public void Reset()
    {
        Phase = StirringPhase.Waiting;
        Kind = null;
        Awake = null;
        Where = null;
        _next = FirstMin + (float)_random.NextDouble() * (FirstMax - FirstMin);
    }

    /// <summary>
    /// Moves the clocks on. While waiting and <paramref name="allowed"/>, once due it asks <paramref name="pickSpot"/> for a spot (null: none just now, try again
    /// next frame) and begins to stir there. While stirring, when the count runs out, it says to wake the mini boss (the run spawns it and hands it back with
    /// <see cref="Woken"/>).
    /// </summary>
    public StirringNews Update(float deltaSeconds, bool allowed, Func<(Vector2D<float> Spot, string? Where)?> pickSpot)
    {
        switch (Phase)
        {
            case StirringPhase.Waiting:
                _next = MathF.Max(0f, _next - deltaSeconds);
                if (_next > 0f || !allowed || pickSpot() is not { } place)
                {
                    return StirringNews.None;
                }

                Kind = MiniBosses.All[_random.Next(MiniBosses.All.Count)];
                Spot = place.Spot;
                Where = place.Where;
                WakesIn = StirSeconds;
                Phase = StirringPhase.Stirring;
                return StirringNews.Began;

            case StirringPhase.Stirring:
                WakesIn = MathF.Max(0f, WakesIn - deltaSeconds);
                return WakesIn > 0f ? StirringNews.None : StirringNews.Woke;

            default:
                if (Awake is { IsAlive: false })
                {
                    Awake = null;   // gone (or never came back): the next may come once due
                }

                _next = MathF.Max(0f, _next - deltaSeconds);
                if (Awake is null && _next <= 0f)
                {
                    _next = 0f;
                    Phase = StirringPhase.Waiting;
                }

                return StirringNews.None;
        }
    }

    /// <summary>The run has spawned the mini boss that woke: the next is due a while after this.</summary>
    public void Woken(Enemy boss)
    {
        Awake = boss;
        Phase = StirringPhase.Awake;
        _next = GapMin + (float)_random.NextDouble() * (GapMax - GapMin);
    }

    /// <summary>
    /// A spot for a mini boss to stir: in a chamber other than the player's (and not the boss arena's), where the way along the floor from the player
    /// (<paramref name="pathDistance"/>) is between <see cref="NearestPath"/> and <see cref="FarthestPath"/>; failing that, anywhere walkable at a fair way. The
    /// chambers' spots are tried in a random order, a dozen each. Null if there is nowhere.
    /// </summary>
    public (Vector2D<float> Spot, string? Where)? PickSpot(Vector2D<float> player, Func<float, float, float> pathDistance, Func<float, float, bool> open)
    {
        var here = CaveLayout.ChamberAt(player.X, player.Y);
        var tried = new List<(Vector2D<float>, string?, float)>();
        foreach (var chamber in CaveLayout.Chambers.OrderBy(_ => _random.Next()))
        {
            if (chamber == here || chamber.Name == ArenaChamber)
            {
                continue;
            }

            for (int k = 0; k < 12; k++)
            {
                float a = (float)_random.NextDouble() * MathF.Tau, r = 0.6f * MathF.Sqrt((float)_random.NextDouble());
                var spot = chamber.Centre + new Vector2D<float>(MathF.Cos(a) * r * chamber.RadiusX, MathF.Sin(a) * r * chamber.RadiusZ);
                if (!open(spot.X, spot.Y))
                {
                    continue;
                }

                float d = pathDistance(spot.X, spot.Y);
                if (d is >= NearestPath and <= FarthestPath)
                {
                    return (spot, chamber.Name);
                }

                if (float.IsFinite(d))
                {
                    tried.Add((spot, chamber.Name, d));
                }
            }
        }

        // Nowhere in the band (a small cave, or the player far out in a corner): the one nearest to it, if any has a way to it at all.
        if (tried.Count == 0)
        {
            return null;
        }

        var (best, where, _) = tried.OrderBy(t => MathF.Abs(t.Item3 - (NearestPath + FarthestPath) / 2f)).First();
        return (best, where);
    }

    /// <summary>A stirring spot is at least this far along the floor from the player, and at most this far.</summary>
    public const float NearestPath = 60f;
    public const float FarthestPath = 220f;

    /// <summary>The boss arena's chamber: no stirring there.</summary>
    public const string ArenaChamber = "The Pit";
}
