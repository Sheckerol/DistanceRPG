using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

public class GridCollisionTests
{
    private const float Tile = GameConstants.Tile;   // 32
    private const float R = GameConstants.PlayerHalf; // 14

    /// <summary>3Ã—5 grid, fully open except a wall tile at row 1, col 2.</summary>
    private static int[,] GridWithCenterWall()
    {
        var grid = new int[3, 5];
        grid[1, 2] = 1;
        return grid;
    }

    [Fact]
    public void HeadOnPush_StopsAtWallFace()
    {
        var grid = GridWithCenterWall();
        float y = 1.5f * Tile; // vertically centred on the wall tile's row

        // Approach the wall tile (x range 64..96) from the left
        var (x, ny) = GridCollision.Move(grid, 1.0f * Tile, y, R, dx: 40f, dy: 0f);

        Assert.Equal(2f * Tile - R, x, 3); // flush against the wall's left face
        Assert.Equal(y, ny, 3);
    }

    [Fact]
    public void DiagonalPush_SlidesAlongWall()
    {
        var grid = GridWithCenterWall();
        float y = 1.5f * Tile;

        // Push into the wall and downward at once: X blocked, Y slides.
        var (x, ny) = GridCollision.Move(grid, 2f * Tile - R, y, R, dx: 10f, dy: 8f);

        Assert.Equal(2f * Tile - R, x, 3);
        Assert.Equal(y + 8f, ny, 3);
    }

    [Fact]
    public void NoObstacle_MovesFreely()
    {
        var grid = new int[4, 4];
        var (x, y) = GridCollision.Move(grid, 50f, 50f, R, dx: 12f, dy: -9f);
        Assert.Equal(62f, x, 3);
        Assert.Equal(41f, y, 3);
    }

    [Fact]
    public void WorldBounds_ClampToRadius()
    {
        var grid = new int[3, 3];
        var (x, y) = GridCollision.Move(grid, 20f, 20f, R, dx: -100f, dy: -100f);
        Assert.Equal(R, x, 3);
        Assert.Equal(R, y, 3);

        (x, y) = GridCollision.Move(grid, 70f, 70f, R, dx: 100f, dy: 100f);
        Assert.Equal(3f * Tile - R, x, 3);
        Assert.Equal(3f * Tile - R, y, 3);
    }

    [Fact]
    public void CornerContact_PushesOutToRadius()
    {
        var grid = GridWithCenterWall();
        // Slide right along the wall's top: circle centre above the tile's
        // top edge (y0 = 32) by less than R after moving down.
        float startY = 1f * Tile - R + 4f; // 4 units of overlap attempt
        var (x, y) = GridCollision.Move(grid, 2.5f * Tile, 1f * Tile - R, R, dx: 0f, dy: 4f);

        // Y pass must hold the circle at the wall's top face.
        Assert.Equal(1f * Tile - R, y, 3);
        Assert.Equal(2.5f * Tile, x, 3);
        _ = startY;
    }

    [Fact]
    public void BlockerCircle_StopsMovement()
    {
        var grid = new int[4, 4];
        var blocker = new GridCollision.Circle(80f, 50f, 14f);

        // Approach the blocker from the left along its centre line
        var (x, y) = GridCollision.Move(grid, 40f, 50f, R, dx: 30f, dy: 0f, new[] { blocker });

        Assert.Equal(80f - (R + 14f), x, 3); // surface to surface
        Assert.Equal(50f, y, 3);
    }

    [Fact]
    public void BlockerCircle_OffCenterApproach_SlidesOut()
    {
        var grid = new int[4, 4];
        var blocker = new GridCollision.Circle(80f, 50f, 14f);

        // Approach offset vertically by 10: X resolves to touch distance
        var (x, y) = GridCollision.Move(grid, 40f, 60f, R, dx: 30f, dy: 0f, new[] { blocker });

        float dy = 60f - 50f;
        float sum = R + 14f;
        float expectedX = 80f - MathF.Sqrt(sum * sum - dy * dy);
        Assert.Equal(expectedX, x, 3);
        Assert.Equal(60f, y, 3);
    }

    [Fact]
    public void DeadStop_ZeroDelta_StaysPut()
    {
        var grid = GridWithCenterWall();
        var (x, y) = GridCollision.Move(grid, 40f, 40f, R, 0f, 0f);
        Assert.Equal(40f, x, 3);
        Assert.Equal(40f, y, 3);
    }
}
