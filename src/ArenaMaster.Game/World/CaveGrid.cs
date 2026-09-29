using Silk.NET.Maths;

namespace ArenaMaster.Game.World;

/// <summary>
/// Which spots of the map can be walked on, a square cell each over the whole 512 m map: the cave's floor, not its rock (see <see cref="CaveLayout.Grid"/>). Asked
/// all the time (every enemy's step, every spawn), so it is worked out once. Pure.
/// </summary>
internal sealed class CaveGrid
{
    /// <summary>From the map's middle to its edge.</summary>
    public const float Half = 256f;

    private readonly bool[] _walkable;

    private CaveGrid(float cell, int size, bool[] walkable)
    {
        Cell = cell;
        Size = size;
        _walkable = walkable;
    }

    public float Cell { get; }

    /// <summary>How many cells across (and down) it is.</summary>
    public int Size { get; }

    /// <summary>A grid of <paramref name="cell"/>-metre cells, each walkable unless <paramref name="blocked"/> says its middle is.</summary>
    public static CaveGrid Build(float cell, Func<float, float, bool> blocked)
    {
        int size = (int)MathF.Ceiling(2f * Half / cell);
        var walkable = new bool[size * size];
        Parallel.For(0, size, j =>
        {
            for (int i = 0; i < size; i++)
            {
                walkable[j * size + i] = !blocked(-Half + (i + 0.5f) * cell, -Half + (j + 0.5f) * cell);
            }
        });

        return new CaveGrid(cell, size, walkable);
    }

    public (int I, int J) CellOf(float x, float z) => ((int)MathF.Floor((x + Half) / Cell), (int)MathF.Floor((z + Half) / Cell));

    public Vector2D<float> Middle(int i, int j) => new(-Half + (i + 0.5f) * Cell, -Half + (j + 0.5f) * Cell);

    public bool Inside(int i, int j) => i >= 0 && j >= 0 && i < Size && j < Size;

    public int Index(int i, int j) => j * Size + i;

    public bool Walkable(int i, int j) => Inside(i, j) && _walkable[Index(i, j)];

    public bool Walkable(float x, float z)
    {
        var (i, j) = CellOf(x, z);
        return Walkable(i, j);
    }

    /// <summary>Whether the straight way from <paramref name="from"/> to <paramref name="to"/> (flat) crosses only walkable cells, looked at every half a cell.</summary>
    public bool ClearLine(Vector2D<float> from, Vector2D<float> to)
    {
        float length = Vector2D.Distance(from, to);
        int steps = Math.Max(1, (int)MathF.Ceiling(length / (Cell * 0.5f)));
        for (int k = 1; k <= steps; k++)
        {
            var p = Vector2D.Lerp(from, to, (float)k / steps);
            if (!Walkable(p.X, p.Y))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// The way to the player through the cave: every walkable cell's distance along the floor to the player's (from the <see cref="CaveGrid"/>, worked out again as the
/// player moves from cell to cell), so an enemy with a wall between it and the player goes round by the tunnels, and a spawn can be kept to where there is a way to
/// the player. With a clear line an enemy just comes straight. Pure.
/// </summary>
internal sealed class CaveFlow
{
    /// <summary>At most this often (seconds) is the way worked out again, however the player moves.</summary>
    public const float RefreshSeconds = 0.25f;

    private readonly CaveGrid _grid;
    private readonly float[] _distance;
    private (int I, int J) _target = (-1, -1);
    private float _sinceRefresh = float.MaxValue;

    public CaveFlow(CaveGrid grid)
    {
        _grid = grid;
        _distance = new float[grid.Size * grid.Size];
        Array.Fill(_distance, float.PositiveInfinity);
    }

    /// <summary>How many times the way has been worked out (for the tests).</summary>
    public int Refreshes { get; private set; }

    /// <summary>Keeps the way pointing at <paramref name="feet"/>: worked out again when they are in a new cell (no more often than <see cref="RefreshSeconds"/>).</summary>
    public void Update(Vector3D<float> feet, float deltaSeconds)
    {
        _sinceRefresh += deltaSeconds;
        var cell = _grid.CellOf(feet.X, feet.Z);
        if (cell == _target || _sinceRefresh < RefreshSeconds)
        {
            return;
        }

        _target = cell;
        _sinceRefresh = 0f;
        Refresh();
    }

    /// <summary>How far along the floor a spot is from the player's cell, in metres; infinity with no way there.</summary>
    public float Distance(float x, float z)
    {
        var (i, j) = _grid.CellOf(x, z);
        return _grid.Inside(i, j) ? _distance[_grid.Index(i, j)] : float.PositiveInfinity;
    }

    /// <summary>
    /// The flat way an enemy at <paramref name="from"/> should walk to reach <paramref name="to"/> (the player): null if the line between them is clear (walk straight),
    /// otherwise toward the next cell nearer the player along the floor. Null too with no way at all (it stays put against the rock).
    /// </summary>
    public Vector3D<float>? Steer(Vector3D<float> from, Vector3D<float> to)
    {
        var a = new Vector2D<float>(from.X, from.Z);
        if (_grid.ClearLine(a, new Vector2D<float>(to.X, to.Z)))
        {
            return null;
        }

        var (i, j) = _grid.CellOf(from.X, from.Z);
        if (!_grid.Inside(i, j))
        {
            return null;
        }

        // Toward the nearest-to-the-player of the cells round it (and its own, if it is the best: then toward that cell's middle).
        float best = _distance[_grid.Index(i, j)];
        (int I, int J)? next = null;
        foreach (var (di, dj) in Neighbours)
        {
            int ni = i + di, nj = j + dj;
            if (!Step(i, j, di, dj))
            {
                continue;
            }

            float d = _distance[_grid.Index(ni, nj)];
            if (d < best)
            {
                best = d;
                next = (ni, nj);
            }
        }

        if (next is not { } cell || float.IsPositiveInfinity(best))
        {
            return null;
        }

        var toward = _grid.Middle(cell.I, cell.J) - a;
        return toward.LengthSquared > 1e-6f ? Vector3D.Normalize(new Vector3D<float>(toward.X, 0f, toward.Y)) : null;
    }

    private static readonly (int, int)[] Neighbours = { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1) };

    /// <summary>Whether a step from (i, j) by (di, dj) is open: onto a walkable cell, and a diagonal only with both cells beside it open (no cutting a corner of rock).</summary>
    private bool Step(int i, int j, int di, int dj) =>
        _grid.Walkable(i + di, j + dj) && (di == 0 || dj == 0 || (_grid.Walkable(i + di, j) && _grid.Walkable(i, j + dj)));

    /// <summary>Every cell's distance along the floor to the target cell (Dijkstra over the 8 cells round each).</summary>
    private void Refresh()
    {
        Refreshes++;
        Array.Fill(_distance, float.PositiveInfinity);
        if (!_grid.Walkable(_target.I, _target.J))
        {
            // The player is on something not walkable (a mound's edge): start from the walkable cells round them.
            SeedAround();
        }
        else
        {
            _distance[_grid.Index(_target.I, _target.J)] = 0f;
        }

        var queue = new PriorityQueue<(int I, int J), float>();
        for (int k = 0; k < _distance.Length; k++)
        {
            if (_distance[k] == 0f)
            {
                queue.Enqueue((k % _grid.Size, k / _grid.Size), 0f);
            }
        }

        float diagonal = _grid.Cell * MathF.Sqrt(2f);
        while (queue.TryDequeue(out var cell, out float d))
        {
            if (d > _distance[_grid.Index(cell.I, cell.J)])
            {
                continue;
            }

            foreach (var (di, dj) in Neighbours)
            {
                if (!Step(cell.I, cell.J, di, dj))
                {
                    continue;
                }

                float nd = d + (di != 0 && dj != 0 ? diagonal : _grid.Cell);
                int index = _grid.Index(cell.I + di, cell.J + dj);
                if (nd < _distance[index])
                {
                    _distance[index] = nd;
                    queue.Enqueue((cell.I + di, cell.J + dj), nd);
                }
            }
        }
    }

    private void SeedAround()
    {
        for (int r = 1; r <= 4; r++)
        {
            bool found = false;
            for (int dj = -r; dj <= r; dj++)
            {
                for (int di = -r; di <= r; di++)
                {
                    if (_grid.Walkable(_target.I + di, _target.J + dj))
                    {
                        _distance[_grid.Index(_target.I + di, _target.J + dj)] = 0f;
                        found = true;
                    }
                }
            }

            if (found)
            {
                return;
            }
        }
    }
}
