using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// Fog union-box math must match the JS original for the real map (golden),
/// plus targeted unit tests for the primitive rect operations.
/// </summary>
public class FogGeometryTests
{
    [Fact]
    public void GoldenSeed_UnionBoxesMatchJs()
    {
        var golden = GoldenData.Instance;
        var map = MapGenerator.Generate(new Mulberry32(golden.MapSeed));
        var fog = FogBoxBuilder.Build(map);

        Assert.Equal(golden.UnionFogBoxes.Length, fog.UnionBoxes.Count);
        for (int i = 0; i < golden.UnionFogBoxes.Length; i++)
            golden.UnionFogBoxes[i].AssertMatches(fog.UnionBoxes[i]);
    }

    [Fact]
    public void GoldenSeed_PrunedFogBoxesMatchJs()
    {
        var golden = GoldenData.Instance;
        var map = MapGenerator.Generate(new Mulberry32(golden.MapSeed));
        var fog = FogBoxBuilder.Build(map);

        Assert.Equal(golden.FogBoxes.Length, fog.FogBoxes.Count);
        for (int i = 0; i < golden.FogBoxes.Length; i++)
            golden.FogBoxes[i].AssertMatches(fog.FogBoxes[i]);
    }

    [Fact]
    public void RectIntersection_OverlapAndDisjoint()
    {
        var overlap = FogGeometry.RectIntersection(new MapRect(0, 0, 4, 4), new MapRect(2, 2, 4, 4));
        Assert.NotNull(overlap);
        Assert.Equal((2, 2, 2, 2), (overlap.X, overlap.Y, overlap.W, overlap.H));

        // Touching edges do not intersect
        Assert.Null(FogGeometry.RectIntersection(new MapRect(0, 0, 2, 2), new MapRect(2, 0, 2, 2)));
        Assert.Null(FogGeometry.RectIntersection(new MapRect(0, 0, 2, 2), new MapRect(5, 5, 2, 2)));
    }

    [Fact]
    public void RectContainsRect_InnerInsideOuter()
    {
        Assert.True(FogGeometry.RectContainsRect(new MapRect(1, 1, 2, 2), new MapRect(0, 0, 4, 4)));
        Assert.True(FogGeometry.RectContainsRect(new MapRect(0, 0, 4, 4), new MapRect(0, 0, 4, 4)));
        Assert.False(FogGeometry.RectContainsRect(new MapRect(0, 0, 4, 4), new MapRect(1, 1, 2, 2)));
    }

    [Fact]
    public void Prune_DropsContainedAndDuplicateBoxes()
    {
        var boxes = new List<MapRect>
        {
            new(0, 0, 10, 10),
            new(2, 2, 3, 3),   // contained â†’ dropped
            new(0, 0, 10, 10), // duplicate â†’ dropped
            new(9, 9, 5, 5),   // partial overlap â†’ kept
            new(0, 0, 0, 5),   // degenerate â†’ dropped
        };

        var kept = FogGeometry.PruneFullyOverlappedBoxes(boxes);

        Assert.Equal(2, kept.Count);
        Assert.Equal((0, 0, 10, 10), (kept[0].X, kept[0].Y, kept[0].W, kept[0].H));
        Assert.Equal((9, 9, 5, 5), (kept[1].X, kept[1].Y, kept[1].W, kept[1].H));
    }

    [Fact]
    public void FogState_RevealsBoxPlusBorder_AndTracksSeen()
    {
        var fogState = new FogState(10, 10);
        var boxes = new List<MapRect> { new(3, 3, 2, 2) };

        var newly = fogState.RevealAt(3, 3, boxes);

        // Box (3,3,2,2) plus one-tile border â†’ rows/cols 2..5 = 16 tiles
        Assert.Equal(16, newly.Count);
        Assert.True(fogState.Visible[2, 2]);
        Assert.True(fogState.Visible[5, 5]);
        Assert.False(fogState.Visible[6, 6]);

        fogState.ResetVisibility();
        Assert.False(fogState.Visible[3, 3]);
        Assert.True(fogState.Seen[3, 3]); // explored memory persists

        // Standing outside every box reveals nothing
        Assert.Empty(fogState.RevealAt(0, 0, boxes));
    }

    [Fact]
    public void FogState_RevealClampsAtMapEdge()
    {
        var fogState = new FogState(5, 5);
        var boxes = new List<MapRect> { new(0, 0, 2, 2) };

        var newly = fogState.RevealAt(0, 0, boxes);

        // Border would extend to -1; clamped to rows/cols 0..2 = 9 tiles
        Assert.Equal(9, newly.Count);
    }
}
