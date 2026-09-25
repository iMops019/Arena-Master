using CEngine.Core;
using Silk.NET.Maths;

namespace ArenaMaster.Game.Combat;

/// <summary>
/// Puts the <see cref="EnemyField"/> on screen as engine props: one placed prop per enemy, with a shambling bob while it walks, a flinch when it is hit, and a sink into the ground
/// as it dies. There are no animations yet; the stand-in models are rigid.
/// </summary>
internal sealed class EnemyView
{
    private readonly Dictionary<int, int> _props = new();   // enemy id -> placed prop id
    private float _time;

    public void Sync(EngineWindow window, EnemyField field, IEnumerable<Enemy> gone, float deltaSeconds)
    {
        _time += deltaSeconds;

        foreach (var enemy in gone)
        {
            Remove(window, enemy);
        }

        foreach (var enemy in field.Enemies)
        {
            var placement = Pose(enemy);
            if (_props.TryGetValue(enemy.Id, out int propId))
            {
                window.SetPlacedProp(propId, placement);
            }
            else
            {
                _props[enemy.Id] = window.PlaceProp(placement);
            }
        }
    }

    public void Remove(EngineWindow window, Enemy enemy)
    {
        if (_props.Remove(enemy.Id, out int propId))
        {
            window.RemovePlacedProp(propId);
        }
    }

    private PropPlacement Pose(Enemy enemy)
    {
        var position = enemy.Position;
        float scale = 1f + 0.12f * enemy.HitFlash;   // a quick swell on a hit
        float pitch = -0.25f * enemy.HitFlash;       // and a flinch back

        if (enemy.IsAlive)
        {
            position.Y += MathF.Abs(MathF.Sin(_time * 7f + enemy.Phase)) * 0.06f;
            pitch += 0.08f * MathF.Sin(_time * 3.5f + enemy.Phase);
        }
        else
        {
            float t = Math.Clamp(enemy.DeadFor / EnemyField.DeathDuration, 0f, 1f);
            position.Y -= t * enemy.Kind.Height * 0.8f;   // sinks into the ground
            scale *= 1f - 0.4f * t;
            pitch += 0.9f * t;                              // slumping forward
        }

        return new PropPlacement(enemy.Kind.Model, position, enemy.Yaw, scale, pitch);
    }
}
