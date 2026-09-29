using Silk.NET.Maths;

namespace ArenaMaster.Game.World;

/// <summary>What the Hungering Maw did this frame, for the run to answer.</summary>
internal enum HungerNews
{
    None,

    /// <summary>It opened: announce it, and send more monsters to feed it.</summary>
    Opened,

    /// <summary>It was fed its fill in time: it spits out its hoard.</summary>
    Sated,

    /// <summary>Its time ran out hungry: it spews up what it has in its belly - a pack of rare ghouls and a brute.</summary>
    Starved,
}

/// <summary>
/// A random event of the cave, the Hungering Maw (the user's idea, 2026-09-29: kill a lot of enemies to satisfy something's hunger, in the way of Path of Exile's
/// league mechanics; the rest is Claude's): every few minutes a mouth opens in the floor near the player, ringed with teeth. Every kill within its
/// <see cref="Reach"/> feeds it (a red mote flies from the kill into it). Fill it (<see cref="Need"/> kills, more the later in the run) within
/// <see cref="OpenSeconds"/> and it is sated and spits out a hoard; let it starve and it spews up a pack of rare ghouls and a brute. While it is open more monsters
/// come, to feed it. Pure: the clocks, the count and the motes; the run spawns, pays and draws.
/// </summary>
internal sealed class Hunger
{
    public const float FirstMin = 300f;
    public const float FirstMax = 420f;

    /// <summary>How long after one closes the next is due (between the two).</summary>
    public const float GapMin = 240f;
    public const float GapMax = 360f;

    /// <summary>How long it stays open, hungry.</summary>
    public const float OpenSeconds = 45f;

    /// <summary>A kill this near it feeds it.</summary>
    public const float Reach = 13f;

    /// <summary>How many more monsters there are while it is open (times the director's number, plus this many).</summary>
    public const float CrowdMultiplier = 1.4f;
    public const int CrowdExtra = 12;

    /// <summary>How long a mote takes to fly from a kill into the maw.</summary>
    public const float MoteSeconds = 0.6f;

    private readonly Random _random;
    private readonly List<(Vector3D<float> From, float Age)> _motes = new();
    private float _next;

    public Hunger(Random random)
    {
        _random = random;
        Reset();
    }

    public bool IsOpen { get; private set; }

    /// <summary>Where its mouth is (its middle, on the ground).</summary>
    public Vector3D<float> Centre { get; private set; }

    /// <summary>How many kills it has been fed, and how many it wants.</summary>
    public int Fed { get; private set; }

    public int Need { get; private set; }

    /// <summary>Seconds it stays open yet.</summary>
    public float Left { get; private set; }

    public float NextIn => _next;

    /// <summary>The motes flying into it now: where each set off, and how far through its flight (0 to 1).</summary>
    public IEnumerable<(Vector3D<float> From, float Through)> Motes => _motes.Select(m => (m.From, m.Age / MoteSeconds));

    /// <summary>How many kills it wants <paramref name="runSeconds"/> into a run: 30 at first, two more a minute, at most 90.</summary>
    public static int NeedAt(float runSeconds) => Math.Min(90, 30 + (int)(2f * runSeconds / 60f));

    public void Reset()
    {
        IsOpen = false;
        _motes.Clear();
        _next = FirstMin + (float)_random.NextDouble() * (FirstMax - FirstMin);
    }

    /// <summary>
    /// Moves the clocks on. Closed and <paramref name="allowed"/>, once due it asks <paramref name="pickSpot"/> where to open (null: nowhere just now). Open, it is
    /// sated the moment it has its fill, and starves when its time runs out.
    /// </summary>
    public HungerNews Update(float deltaSeconds, float runSeconds, bool allowed, Func<Vector3D<float>?> pickSpot)
    {
        for (int i = _motes.Count - 1; i >= 0; i--)
        {
            var (from, age) = _motes[i];
            age += deltaSeconds;
            if (age >= MoteSeconds)
            {
                _motes.RemoveAt(i);
            }
            else
            {
                _motes[i] = (from, age);
            }
        }

        if (!IsOpen)
        {
            _next = MathF.Max(0f, _next - deltaSeconds);
            if (_next > 0f || !allowed || pickSpot() is not { } spot)
            {
                return HungerNews.None;
            }

            IsOpen = true;
            Centre = spot;
            Fed = 0;
            Need = NeedAt(runSeconds);
            Left = OpenSeconds;
            return HungerNews.Opened;
        }

        if (Fed >= Need)
        {
            Close();
            return HungerNews.Sated;
        }

        Left = MathF.Max(0f, Left - deltaSeconds);
        if (Left > 0f)
        {
            return HungerNews.None;
        }

        Close();
        return HungerNews.Starved;
    }

    /// <summary>A kill at <paramref name="at"/>: it feeds the maw if the maw is open and it was within reach (true then), and a mote flies in.</summary>
    public bool Feed(Vector3D<float> at)
    {
        if (!IsOpen || Fed >= Need || Vector2D.Distance(new Vector2D<float>(at.X, at.Z), new Vector2D<float>(Centre.X, Centre.Z)) > Reach)
        {
            return false;
        }

        Fed++;
        _motes.Add((at + new Vector3D<float>(0f, 1f, 0f), 0f));
        return true;
    }

    /// <summary>The fodder target while it is open: more monsters, to feed it.</summary>
    public int Crowd(int target) => IsOpen ? (int)(target * CrowdMultiplier) + CrowdExtra : target;

    private void Close()
    {
        IsOpen = false;
        _next = GapMin + (float)_random.NextDouble() * (GapMax - GapMin);
    }
}
