using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

public class ReachFieldsTests
{
    private const float Tile = GameConstants.Tile;      // 32
    private const float R = GameConstants.PlayerHalf;   // 14
    private const float Step = ReachArea.Step;          // 4

    private static int Node(float logic) => (int)MathF.Round(logic / Step);

    /// <summary>A room of open floor ringed by one tile of wall.</summary>
    private static int[,] Room(int rows, int cols)
    {
        var grid = new int[rows, cols];
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
            if (r == 0 || c == 0 || r == rows - 1 || c == cols - 1) grid[r, c] = 1;
        return grid;
    }

    [Fact]
    public void OpenFloor_CostIsTheEightWayDistance()
    {
        var grid = Room(20, 20);
        float x = 10 * Tile, y = 10 * Tile;
        var area = ReachFields.Walkable(grid, x, y, R, budget: 160f);

        foreach (var (dc, dr) in new[] { (10, 0), (0, -25), (10, 10), (20, 7), (-13, 30) })
        {
            int row = Node(y) + dr, col = Node(x) + dc;
            float expected = ReachFields.Octile(dc * Step, dr * Step);
            Assert.Equal(expected, area.CostAt(row, col), 2);
        }
    }

    [Fact]
    public void OpenFloor_TheEdgeIsTheBudget()
    {
        var grid = Room(20, 20);
        float x = 10 * Tile, y = 10 * Tile;
        var area = ReachFields.Walkable(grid, x, y, R, budget: 80f);

        Assert.True(area.Contains(Node(y), Node(x) + 20));          // 80 straight east: exactly the budget
        Assert.False(area.Contains(Node(y), Node(x) + 21));         // one step past it
        Assert.True(area.Contains(Node(y) + 14, Node(x) + 14));     // 14 diagonal steps ≈ 79.2
        Assert.False(area.Contains(Node(y) + 15, Node(x) + 15));    // 15 ≈ 84.9: a circle would claim it, eight-way steering can't
    }

    [Fact]
    public void WalkingAPathInPlay_SpendsWhatTheFieldSays()
    {
        // Steer like DungeonScene does — 160 units/s, diagonals at 0.707 a
        // side, each frame through GridCollision.Move — east then south-east
        // to a node, and count what the budget pays.
        var grid = Room(20, 20);
        float x = 6 * Tile, y = 6 * Tile;
        var area = ReachFields.Walkable(grid, x, y, R, budget: 400f);
        int goalRow = Node(y) + 12, goalCol = Node(x) + 30;     // 18 east, 12 south-east

        float spent = 0f, px = x, py = y, dt = 1f / 240f;
        void Steer(float vx, float vy, float targetX, float targetY)
        {
            while (MathF.Abs(px - targetX) > 0.01f || MathF.Abs(py - targetY) > 0.01f)
            {
                float dx = vx * dt, dy = vy * dt;
                float len = MathF.Sqrt(dx * dx + dy * dy);
                float left = MathF.Max(MathF.Abs(targetX - px), MathF.Abs(targetY - py));
                float frame = MathF.Max(MathF.Abs(dx), MathF.Abs(dy));
                if (frame > left) { dx *= left / frame; dy *= left / frame; len *= left / frame; }
                var (nx, ny) = GridCollision.Move(grid, px, py, R, dx, dy);
                spent += MathF.Sqrt((nx - px) * (nx - px) + (ny - py) * (ny - py));
                px = nx; py = ny;
            }
        }
        Steer(GameConstants.Speed, 0f, x + 18 * Step, y);
        Steer(GameConstants.Speed * 0.707f, GameConstants.Speed * 0.707f, x + 30 * Step, y + 12 * Step);

        Assert.Equal(area.CostAt(goalRow, goalCol), spent, 0);  // within a unit over a 140-unit walk
    }

    [Fact]
    public void AWall_IsWalkedAround()
    {
        // A wall across the room with a gap at its bottom end: the far side
        // costs the detour, never the straight line through the wall.
        var grid = Room(14, 14);
        for (int r = 1; r <= 10; r++) grid[r, 7] = 1;
        float x = 5.5f * Tile, y = 3.5f * Tile;
        var area = ReachFields.Walkable(grid, x, y, R, budget: 1000f);

        float straight = ReachFields.Octile(4 * Tile, 0f);
        float across = area.CostAt(Node(y), Node(9.5f * Tile));
        Assert.True(across > straight + 7 * Tile, $"cost {across} should be a detour round the wall");
        Assert.False(area.Contains(Node(y), Node(7.5f * Tile)));  // never inside the wall
    }

    [Fact]
    public void AOneTileCorridor_IsPassable()
    {
        var grid = new int[5, 12];
        for (int r = 0; r < 5; r++)
        for (int c = 0; c < 12; c++)
            grid[r, c] = r == 2 ? 0 : 1;                        // one open row
        float y = 2.5f * Tile;
        var area = ReachFields.Walkable(grid, 1.5f * Tile, y, R, budget: 200f);

        Assert.True(area.Contains(Node(y), Node(7.5f * Tile)));   // 6 tiles down the corridor
        Assert.False(area.Contains(Node(y) + 1, Node(7.5f * Tile))); // a step off its centre line touches a wall
    }

    [Fact]
    public void ABody_Blocks()
    {
        var grid = Room(9, 30);
        // A wall of bodies across the room: nothing past it.
        var blockers = new List<GridCollision.Circle>();
        for (int r = 1; r < 8; r++) blockers.Add(new GridCollision.Circle(15.5f * Tile, (r + 0.5f) * Tile, R));
        float y = 4.5f * Tile;
        var area = ReachFields.Walkable(grid, 10.5f * Tile, y, R, budget: 600f, blockers);

        Assert.True(area.Contains(Node(y), Node(13.5f * Tile)));
        Assert.False(area.Contains(Node(y), Node(18.5f * Tile)));
    }

    [Fact]
    public void NoBudget_StaysWhereItStands()
    {
        var grid = Room(10, 10);
        var area = ReachFields.Walkable(grid, 5 * Tile, 5 * Tile, R, budget: 0f);

        Assert.True(area.Contains(Node(5 * Tile), Node(5 * Tile)));   // already on a node: zero distance
        Assert.Equal(1, area.Count);
    }

    [Fact]
    public void InReach_IsExactlyWhereCanHitSaysYes()
    {
        // Everywhere in the window: an enemy standing on a node is in the area
        // exactly when EnemyAi.CanHit (range and sight) lets the member hit it.
        var grid = Room(16, 16);
        grid[6, 9] = 1; grid[7, 9] = 1; grid[8, 9] = 1;           // a pillar to cast a shadow
        var weapon = TestWeapons.Get("weakspot_stiletto");
        var member = TestPools.Holding("A", weapon, x: 7 * Tile + 3, y: 7.5f * Tile);
        var probe = new EnemyState();
        var area = ReachFields.InReach(grid, member.X, member.Y, member.Radius, probe.Radius, weapon.Range);

        int checkedIn = 0;
        for (int row = area.Row0; row < area.Row0 + area.Rows; row++)
        for (int col = area.Col0; col < area.Col0 + area.Cols; col++)
        {
            var (px, py) = ReachArea.NodePoint(row, col);
            if (!ReachFields.BodyFits(grid, px, py, probe.Radius, null)) { Assert.False(area.Contains(row, col)); continue; }
            probe.X = px; probe.Y = py;
            bool hit = EnemyAi.CanHit(member, probe, weapon, grid);
            Assert.Equal(hit, area.Contains(row, col));
            if (hit) checkedIn++;
        }
        Assert.True(checkedIn > 0);
    }

    [Fact]
    public void InReach_AWallCastsAShadow()
    {
        var grid = Room(16, 16);
        grid[7, 9] = 1;
        float x = 7.5f * Tile, y = 7.5f * Tile;
        var area = ReachFields.InReach(grid, x, y, R, R, range: 200f);

        Assert.True(area.Contains(Node(5.5f * Tile), Node(11.5f * Tile)));   // off the wall's line: seen
        Assert.False(area.Contains(Node(y), Node(11.5f * Tile)));            // straight behind it: hidden
    }

    [Fact]
    public void AttackTicks_CountBackFromTheEnd()
    {
        // 100 left at 30 an attack: stopping at 70 leaves one, 40 two, 10 three.
        Assert.Equal([70f, 40f, 10f], ReachFields.AttackTicks(100f, 30));
    }

    [Fact]
    public void AttackTicks_AnExactMultipleTicksTheActorsOwnSpot()
    {
        Assert.Equal([60f, 30f, 0f], ReachFields.AttackTicks(90f, 30));
    }

    [Theory]
    [InlineData(20f, 30)]   // not enough for even one: no tick
    [InlineData(0f, 30)]    // no movement
    [InlineData(100f, 0)]   // a free attack
    public void AttackTicks_NoneWhereNoneFit(float left, int cost)
    {
        Assert.Empty(ReachFields.AttackTicks(left, cost));
    }
}
