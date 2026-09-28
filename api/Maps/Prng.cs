namespace battle_map_generator_api.Maps;

// SplitMix64: the project's own small seeded generator. System.Random is not used because its
// algorithm is not guaranteed across runtime versions, and fixture grids must stay identical.
// An instance is created per generation and passed through the algorithm; there is no shared state.
public sealed class Prng(uint seed)
{
    private ulong _state = seed;

    public ulong NextUInt64()
    {
        var z = _state += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    // Multiply-high maps 64 random bits onto the range; the bias is below 2^-32 for map-sized ranges.
    public int NextInt(int minInclusive, int maxExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxExclusive, minInclusive);
        var range = (ulong)((long)maxExclusive - minInclusive);
        return (int)(minInclusive + (long)Math.BigMul(NextUInt64(), range, out _));
    }
}
