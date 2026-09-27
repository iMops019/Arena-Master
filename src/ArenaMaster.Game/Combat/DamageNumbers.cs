using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Combat;

/// <summary>
/// Numbers that pop up where a hit landed, drift up and fade - drawn on the HUD at the hit's place on screen. Damage over time (Plague, rot, holy circles, the
/// Blizzard, the Shaman's zaps) ticks far too often to number each tick: it is added up for each enemy and shown once every <see cref="OverTimeEvery"/> seconds
/// (or at once, when the enemy dies), smaller and in its own green.
/// </summary>
internal sealed class DamageNumbers
{
    private const float Lifetime = 0.8f;
    private const float RiseSpeed = 1.6f;   // metres per second

    /// <summary>At most this many numbers are up at once; past it the oldest goes first, so a swarm's hits can't flood the screen.</summary>
    public const int MaxShown = 60;

    /// <summary>How long an enemy's damage over time is added up before it shows as one number.</summary>
    public const float OverTimeEvery = 1f;

    private readonly List<Number> _numbers = new();

    /// <summary>Each enemy's damage over time not shown yet: where it last landed, how much, and how long it has been adding up.</summary>
    private readonly Dictionary<Enemy, (Vector3D<float> Position, float Amount, float Age)> _overTime = new();

    private record struct Number(Vector3D<float> Position, int Amount, bool Kill, bool Crit, bool OverTime, float Age);

    public int Count => _numbers.Count;

    /// <summary>The numbers up now, for the tests: each one's amount, and whether it is damage over time.</summary>
    public IEnumerable<(int Amount, bool OverTime)> Shown => _numbers.Select(n => (n.Amount, n.OverTime));

    public void Add(Vector3D<float> position, float amount, bool kill, bool crit = false) => Show(new Number(position, (int)MathF.Round(amount), kill, crit, false, 0f));

    /// <summary>
    /// A tick of damage over time on <paramref name="enemy"/>: added to what it has taken since its last number, which shows once it has added up for
    /// <see cref="OverTimeEvery"/> seconds, or now if <paramref name="killed"/>.
    /// </summary>
    public void AddOverTime(Enemy enemy, Vector3D<float> position, float amount, bool killed)
    {
        var (_, sum, age) = _overTime.GetValueOrDefault(enemy);
        _overTime[enemy] = (position, sum + amount, age);
        if (killed)
        {
            Flush(enemy, kill: true);
        }
    }

    public void Update(float deltaSeconds)
    {
        foreach (var enemy in _overTime.Keys.ToList())
        {
            var (position, amount, age) = _overTime[enemy];
            _overTime[enemy] = (position, amount, age + deltaSeconds);
            if (age + deltaSeconds >= OverTimeEvery)
            {
                Flush(enemy, kill: false);
            }
        }

        for (int i = _numbers.Count - 1; i >= 0; i--)
        {
            var n = _numbers[i];
            n.Age += deltaSeconds;
            n.Position += new Vector3D<float>(0f, RiseSpeed * deltaSeconds, 0f);
            if (n.Age >= Lifetime)
            {
                _numbers.RemoveAt(i);
            }
            else
            {
                _numbers[i] = n;
            }
        }
    }

    public void Clear()
    {
        _numbers.Clear();
        _overTime.Clear();
    }

    public void Draw(IHud hud, Camera camera)
    {
        var screen = hud.ScreenSize;
        if (screen.X <= 0 || screen.Y <= 0)
        {
            return;
        }

        var viewProjection = camera.GetView() * camera.GetProjection((float)screen.X / screen.Y);
        foreach (var n in _numbers)
        {
            var clip = Vector4D.Transform(new Vector4D<float>(n.Position, 1f), viewProjection);
            if (clip.W <= 0.1f)
            {
                continue;   // behind the camera
            }

            float x = (clip.X / clip.W * 0.5f + 0.5f) * screen.X;
            float y = (1f - (clip.Y / clip.W * 0.5f + 0.5f)) * screen.Y;
            float fade = 1f - n.Age / Lifetime;
            var color = n.OverTime ? new Vector4D<float>(0.62f, 0.95f, 0.42f, fade)
                : n.Crit ? new Vector4D<float>(1f, 0.42f, 0.2f, fade)
                : n.Kill ? new Vector4D<float>(1f, 0.78f, 0.3f, fade)
                : new Vector4D<float>(1f, 1f, 1f, fade);
            string text = n.Crit ? n.Amount + "!" : n.Amount.ToString();
            float scale = n.OverTime ? 0.8f : n.Crit ? 1.3f : n.Kill ? 1.1f : 0.9f;
            hud.Text(HudAnchor.TopLeft, new Vector2D<float>(x - text.Length * 5f * scale, y), text, color, scale);
        }
    }

    private void Show(Number number)
    {
        if (_numbers.Count >= MaxShown)
        {
            _numbers.RemoveAt(0);
        }

        _numbers.Add(number);
    }

    /// <summary>Shows what <paramref name="enemy"/> has taken over time as one number, a little above where it landed, and starts adding up afresh.</summary>
    private void Flush(Enemy enemy, bool kill)
    {
        var (position, amount, _) = _overTime[enemy];
        _overTime.Remove(enemy);
        int rounded = (int)MathF.Round(amount);
        if (rounded > 0)
        {
            Show(new Number(position + new Vector3D<float>(0.3f, 0.4f, 0f), rounded, kill, false, true, 0f));
        }
    }
}
