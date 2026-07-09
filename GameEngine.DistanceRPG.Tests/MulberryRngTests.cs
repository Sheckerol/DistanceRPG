using GameEngine.DistanceRPG.Logic;

namespace GameEngine.DistanceRPG.Tests;

/// <summary>
/// The C# mulberry32 must be bit-identical to the JS original â€” map seeds
/// shared between the prototype and the port must generate the same dungeon.
/// </summary>
public class MulberryRngTests
{
    [Theory]
    [InlineData("1")]
    [InlineData("-1")]
    [InlineData("556432165")]
    [InlineData("2762136374")] // the game's map seed; overflows int32
    public void MatchesJsSampleStream(string seedKey)
    {
        double[] expected = GoldenData.Instance.RngSamples[seedKey];
        var rng = new Mulberry32(long.Parse(seedKey));

        for (int i = 0; i < expected.Length; i++)
            Assert.Equal(expected[i], rng.NextDouble()); // exact â€” both are IEEE 754 ops

    }

    [Fact]
    public void NextIntCoversInclusiveRange()
    {
        var rng = new Mulberry32(12345);
        var seen = new HashSet<int>();
        for (int i = 0; i < 1000; i++)
        {
            int v = rng.NextInt(1, 3);
            Assert.InRange(v, 1, 3);
            seen.Add(v);
        }
        Assert.Equal(3, seen.Count);
    }
}
