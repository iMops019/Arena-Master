using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Combat;

/// <summary>Numbers that pop up where a hit landed, drift up and fade - drawn on the HUD at the hit's place on screen.</summary>
internal sealed class DamageNumbers
{
    private const float Lifetime = 0.8f;
    private const float RiseSpeed = 1.6f;   // metres per second

    /// <summary>At most this many numbers are up at once; past it the oldest goes first, so a swarm's hits can't flood the screen.</summary>
    public const int MaxShown = 60;

    private readonly List<(Vector3D<float> Position, int Amount, bool Kill, bool Crit, float Age)> _numbers = new();

    public int Count => _numbers.Count;

    public void Add(Vector3D<float> position, float amount, bool kill, bool crit = false)
    {
        if (_numbers.Count >= MaxShown)
        {
            _numbers.RemoveAt(0);
        }

        _numbers.Add((position, (int)MathF.Round(amount), kill, crit, 0f));
    }

    public void Update(float deltaSeconds)
    {
        for (int i = _numbers.Count - 1; i >= 0; i--)
        {
            var n = _numbers[i];
            n.Age += deltaSeconds;
            n.Position.Y += RiseSpeed * deltaSeconds;
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

    public void Clear() => _numbers.Clear();

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
            var color = n.Crit ? new Vector4D<float>(1f, 0.42f, 0.2f, fade)
                : n.Kill ? new Vector4D<float>(1f, 0.78f, 0.3f, fade)
                : new Vector4D<float>(1f, 1f, 1f, fade);
            string text = n.Crit ? n.Amount + "!" : n.Amount.ToString();
            float scale = n.Crit ? 1.3f : n.Kill ? 1.1f : 0.9f;
            hud.Text(HudAnchor.TopLeft, new Vector2D<float>(x - text.Length * 5f * scale, y), text, color, scale);
        }
    }
}
