using System.Text.Json;
using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// Golden data generated from the original Phaser game's JavaScript modules
/// (TestData/distancerpg-golden.json). The C# port must reproduce these
/// outputs exactly â€” same RNG stream, same map for the game's real seed,
/// same fog boxes, same A* paths.
/// </summary>
public sealed class GoldenData
{
    public int MapRows { get; set; }
    public int MapCols { get; set; }
    public Dictionary<string, double[]> RngSamples { get; set; } = new();
    public long MapSeed { get; set; }
    public GoldenRoom[] Rooms { get; set; } = [];
    public GoldenCorridor[] Corridors { get; set; } = [];
    public GoldenCorridor[] ExpandedCorridors { get; set; } = [];
    public int[] PlayerStart { get; set; } = [];
    public int[] EnemyStart { get; set; } = [];
    public string[] GridRows { get; set; } = [];
    public GoldenRect[] UnionFogBoxes { get; set; } = [];
    public GoldenRect[] FogBoxes { get; set; } = [];
    public GoldenPaths Paths { get; set; } = new();

    private static readonly Lazy<GoldenData> _instance = new(() =>
    {
        string path = Path.Combine(AppContext.BaseDirectory, "TestData", "distancerpg-golden.json");
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<GoldenData>(File.ReadAllText(path), options)
               ?? throw new InvalidOperationException($"Failed to parse {path}");
    });

    public static GoldenData Instance => _instance.Value;

    /// <summary>The map the C# generator produces for the golden seed.</summary>
    private static readonly Lazy<MapData> _map = new(()
        => MapGenerator.Generate(new Mulberry32(Instance.MapSeed)));

    public static MapData GeneratedMap => _map.Value;
}

public class GoldenRect
{
    public int X { get; set; }
    public int Y { get; set; }
    public int W { get; set; }
    public int H { get; set; }

    public void AssertMatches(MapRect actual)
    {
        Assert.Equal((X, Y, W, H), (actual.X, actual.Y, actual.W, actual.H));
    }
}

public sealed class GoldenRoom : GoldenRect
{
    public int Cx { get; set; }
    public int Cy { get; set; }
}

public sealed class GoldenCorridor : GoldenRect
{
    public string Dir { get; set; } = "";
}

public sealed class GoldenPaths
{
    public GoldenTile[]? FullPath { get; set; }
    public GoldenTile[]? Range2 { get; set; }
    public GoldenTile[]? Range4 { get; set; }
}

public sealed class GoldenTile
{
    public int R { get; set; }
    public int C { get; set; }
}
