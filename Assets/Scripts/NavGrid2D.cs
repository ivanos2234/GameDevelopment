using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A walkability grid over the level and an A* search on it, so chasers walk around walls and around enemies
/// that stand still (dummies, turrets, Masters) instead of getting stuck on them. Created on first use;
/// cells are blocked by static colliders, inflated by the walker's radius. The grid rebuilds when an enemy dies,
/// because a dead dummy opens up the way.
/// </summary>
public sealed class NavGrid2D : MonoBehaviour
{
    private const float CellSize = 0.5f;
    private const float AgentRadius = 0.5f;
    private const int MaxSearchNodes = 6000;

    private static NavGrid2D instance;

    private LayerMask blockers;
    private Rect area;
    private int width;
    private int height;
    private bool[] blocked;
    private bool dirty = true;

    // Reused A* buffers.
    private float[] cost;
    private int[] cameFrom;
    private readonly List<(float priority, int cell)> open = new();

    public static NavGrid2D Instance
    {
        get
        {
            if (instance == null)
                instance = new GameObject("Nav Grid").AddComponent<NavGrid2D>();
            return instance;
        }
    }

    /// <summary>Call when the static layout changed (an enemy that blocked the way died).</summary>
    public static void MarkDirty()
    {
        if (instance != null)
            instance.dirty = true;
    }

    private void Awake()
    {
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        blockers = 1 | (enemyLayer >= 0 ? 1 << enemyLayer : 0);
    }

    /// <summary>
    /// Fills <paramref name="path"/> with world-space waypoints from <paramref name="from"/> to <paramref name="to"/>,
    /// already shortened where a straight walk is clear. Returns false when no path exists.
    /// </summary>
    public bool FindPath(Vector2 from, Vector2 to, List<Vector2> path)
    {
        path.Clear();
        if (dirty)
            Rebuild();
        if (blocked == null)
            return false;

        int start = Cell(from);
        int goal = NearestOpen(Cell(to));
        if (start < 0 || goal < 0)
            return false;

        if (!Search(start, goal))
            return false;

        for (int cell = goal; cell != start; cell = cameFrom[cell])
            path.Add(Center(cell));
        path.Reverse();
        if (path.Count > 0)
            path[path.Count - 1] = to;
        return true;
    }

    /// <summary>True when a walker of the agent's size can move straight between the points without hitting anything static.</summary>
    public bool IsClear(Vector2 from, Vector2 to)
    {
        Vector2 delta = to - from;
        float distance = delta.magnitude;
        if (distance < 0.01f)
            return true;

        var hits = new RaycastHit2D[8];
        int count = Physics2D.CircleCast(from, AgentRadius * 0.9f, delta / distance, new ContactFilter2D { useLayerMask = true, layerMask = blockers, useTriggers = false }, hits, distance);
        for (int i = 0; i < count; i++)
            if (IsStatic(hits[i].collider) && hits[i].distance > 0f)
                return false;
        return true;
    }

    // Walls and enemies without a moving body count; the player, chasers, saw and Guardians move and do not.
    private static bool IsStatic(Collider2D collider)
    {
        if (collider == null || collider.isTrigger)
            return false;
        Rigidbody2D rb = collider.attachedRigidbody;
        return rb == null || rb.bodyType == RigidbodyType2D.Static;
    }

    private void Rebuild()
    {
        dirty = false;

        // The grid covers the floor; levels size their floor sprite to the arena.
        GameObject floor = GameObject.Find("Repeating Background");
        Bounds bounds = floor != null && floor.TryGetComponent(out SpriteRenderer sprite)
            ? sprite.bounds
            : new Bounds(Vector3.zero, new Vector3(40f, 40f, 0f));
        area = new Rect(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y);
        width = Mathf.Max(1, Mathf.CeilToInt(area.width / CellSize));
        height = Mathf.Max(1, Mathf.CeilToInt(area.height / CellSize));

        int count = width * height;
        if (blocked == null || blocked.Length != count)
        {
            blocked = new bool[count];
            cost = new float[count];
            cameFrom = new int[count];
        }

        var overlaps = new Collider2D[8];
        var filter = new ContactFilter2D { useLayerMask = true, layerMask = blockers, useTriggers = false };
        Vector2 probe = Vector2.one * (CellSize + AgentRadius * 2f) * 0.9f;
        for (int i = 0; i < count; i++)
        {
            blocked[i] = false;
            int hits = Physics2D.OverlapBox(Center(i), probe, 0f, filter, overlaps);
            for (int h = 0; h < hits; h++)
            {
                if (IsStatic(overlaps[h]))
                {
                    blocked[i] = true;
                    break;
                }
            }
        }
    }

    private bool Search(int start, int goal)
    {
        for (int i = 0; i < cost.Length; i++)
            cost[i] = float.MaxValue;
        open.Clear();
        cost[start] = 0f;
        cameFrom[start] = start;
        Push(Heuristic(start, goal), start);

        int expanded = 0;
        while (open.Count > 0 && expanded++ < MaxSearchNodes)
        {
            int current = Pop();
            if (current == goal)
                return true;

            int cx = current % width;
            int cy = current / width;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0)
                        continue;
                    int nx = cx + dx, ny = cy + dy;
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                        continue;
                    int next = ny * width + nx;
                    if (blocked[next] && next != goal)
                        continue;
                    // No cutting corners past a blocked cell.
                    if (dx != 0 && dy != 0 && (blocked[cy * width + nx] || blocked[ny * width + cx]))
                        continue;

                    float step = dx != 0 && dy != 0 ? 1.4142f : 1f;
                    float newCost = cost[current] + step;
                    if (newCost >= cost[next])
                        continue;
                    cost[next] = newCost;
                    cameFrom[next] = current;
                    Push(newCost + Heuristic(next, goal), next);
                }
            }
        }
        return false;
    }

    private float Heuristic(int a, int b)
    {
        int dx = Mathf.Abs(a % width - b % width);
        int dy = Mathf.Abs(a / width - b / width);
        return Mathf.Max(dx, dy) + 0.4142f * Mathf.Min(dx, dy);
    }

    // A small binary heap ordered by priority.
    private void Push(float priority, int cell)
    {
        open.Add((priority, cell));
        int i = open.Count - 1;
        while (i > 0)
        {
            int parent = (i - 1) / 2;
            if (open[parent].priority <= open[i].priority)
                break;
            (open[parent], open[i]) = (open[i], open[parent]);
            i = parent;
        }
    }

    private int Pop()
    {
        int top = open[0].cell;
        int last = open.Count - 1;
        open[0] = open[last];
        open.RemoveAt(last);
        int i = 0;
        while (true)
        {
            int left = i * 2 + 1, right = left + 1, smallest = i;
            if (left < open.Count && open[left].priority < open[smallest].priority) smallest = left;
            if (right < open.Count && open[right].priority < open[smallest].priority) smallest = right;
            if (smallest == i)
                break;
            (open[smallest], open[i]) = (open[i], open[smallest]);
            i = smallest;
        }
        return top;
    }

    private int Cell(Vector2 point)
    {
        int x = Mathf.FloorToInt((point.x - area.xMin) / CellSize);
        int y = Mathf.FloorToInt((point.y - area.yMin) / CellSize);
        if (x < 0 || y < 0 || x >= width || y >= height)
            return -1;
        return y * width + x;
    }

    private Vector2 Center(int cell)
    {
        return new Vector2(area.xMin + (cell % width + 0.5f) * CellSize, area.yMin + (cell / width + 0.5f) * CellSize);
    }

    // The target may stand right next to a wall; aim for the closest free cell instead.
    private int NearestOpen(int cell)
    {
        if (cell < 0 || !blocked[cell])
            return cell;

        int cx = cell % width, cy = cell / width;
        for (int radius = 1; radius < 6; radius++)
            for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int x = cx + dx, y = cy + dy;
                    if (x >= 0 && y >= 0 && x < width && y < height && !blocked[y * width + x])
                        return y * width + x;
                }
        return cell;
    }
}
