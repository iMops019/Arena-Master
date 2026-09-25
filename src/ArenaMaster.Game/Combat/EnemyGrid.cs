using Silk.NET.Maths;

namespace ArenaMaster.Game.Combat;

/// <summary>
/// A flat grid over the map that files every enemy under the cell its feet are in, rebuilt each frame, so "who is near here" looks at a few cells instead of the
/// whole horde: enemies keeping apart from their neighbours, and arrows finding what their short stretch of flight passes through. With hundreds of enemies that
/// is the difference between checking every pair and checking a handful.
/// </summary>
internal sealed class EnemyGrid
{
    /// <summary>Cell size, metres: bigger than any enemy's spacing radius, so a neighbour is always in the same or an adjacent cell.</summary>
    public const float CellSize = 4f;

    private readonly Dictionary<(int X, int Z), List<Enemy>> _cells = new();
    private readonly Stack<List<Enemy>> _spare = new();

    /// <summary>Files every enemy in <paramref name="enemies"/> afresh.</summary>
    public void Rebuild(IReadOnlyList<Enemy> enemies)
    {
        foreach (var list in _cells.Values)
        {
            list.Clear();
            _spare.Push(list);
        }

        _cells.Clear();
        foreach (var enemy in enemies)
        {
            var key = Cell(enemy.Position.X, enemy.Position.Z);
            if (!_cells.TryGetValue(key, out var list))
            {
                list = _spare.Count > 0 ? _spare.Pop() : new List<Enemy>();
                _cells[key] = list;
            }

            list.Add(enemy);
        }
    }

    /// <summary>Every enemy filed in a cell that overlaps the square of half-size <paramref name="radius"/> around (<paramref name="x"/>, <paramref name="z"/>). Some may be further than the radius; callers check the exact distance.</summary>
    public IEnumerable<Enemy> Near(float x, float z, float radius) => InBox(x - radius, z - radius, x + radius, z + radius);

    /// <summary>Every enemy filed in a cell that overlaps the flat box around the segment <paramref name="from"/>-<paramref name="to"/>, grown by <paramref name="pad"/>.</summary>
    public IEnumerable<Enemy> AlongSegment(Vector3D<float> from, Vector3D<float> to, float pad) =>
        InBox(MathF.Min(from.X, to.X) - pad, MathF.Min(from.Z, to.Z) - pad, MathF.Max(from.X, to.X) + pad, MathF.Max(from.Z, to.Z) + pad);

    private IEnumerable<Enemy> InBox(float minX, float minZ, float maxX, float maxZ)
    {
        var (x0, z0) = Cell(minX, minZ);
        var (x1, z1) = Cell(maxX, maxZ);
        for (int cx = x0; cx <= x1; cx++)
        {
            for (int cz = z0; cz <= z1; cz++)
            {
                if (_cells.TryGetValue((cx, cz), out var list))
                {
                    foreach (var enemy in list)
                    {
                        yield return enemy;
                    }
                }
            }
        }
    }

    private static (int X, int Z) Cell(float x, float z) => ((int)MathF.Floor(x / CellSize), (int)MathF.Floor(z / CellSize));
}
