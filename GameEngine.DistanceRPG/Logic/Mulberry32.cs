namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// Deterministic PRNG matching the JavaScript mulberry32 used by the original
/// Phaser prototype, so a given map seed generates the identical dungeon in
/// both implementations. All arithmetic is 32-bit with wraparound — uint ops
/// here are bit-identical to JS <c>| 0</c> / <c>Math.imul</c> / <c>&gt;&gt;&gt;</c> semantics.
/// </summary>
public sealed class Mulberry32
{
    private uint _state;

    /// <param name="seed">
    /// Seed, taken modulo 2^32 like the JS <c>seed |= 0</c> coercion — the
    /// prototype's map seed 2762136374 doesn't fit in an int32.
    /// </param>
    public Mulberry32(long seed)
    {
        _state = unchecked((uint)seed);
    }

    /// <summary>Next sample in [0, 1).</summary>
    public double NextDouble()
    {
        unchecked
        {
            _state += 0x6d2b79f5u;
            uint t = (_state ^ (_state >> 15)) * (_state | 1u);
            t = (t + ((t ^ (t >> 7)) * (t | 61u))) ^ t;
            return (t ^ (t >> 14)) / 4294967296.0;
        }
    }

    /// <summary>Inclusive integer range, matching the JS <c>randInt</c> helper.</summary>
    public int NextInt(int min, int max)
    {
        return (int)Math.Floor(NextDouble() * (max - min + 1)) + min;
    }
}
