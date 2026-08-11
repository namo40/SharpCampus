namespace SharpCampus.GameCore.Internal;

// splitmix64, from Sebastiano Vigna's public-domain reference implementation:
// https://prng.di.unimi.it/splitmix64.c
// The BCL random generator gives no guarantee that a seeded sequence stays stable
// across runtime versions, and every replay, test and server/client agreement here rests on it.
internal struct SplitMix64
{
    private const ulong Increment = 0x9E3779B97F4A7C15UL;

    private ulong _state;

    public SplitMix64(ulong seed) => _state = seed;

    public ulong NextUInt64()
    {
        unchecked
        {
            _state += Increment;
            var z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    // Modulo bias is irrelevant at the ranges used here (7 piece kinds, 10 board columns).
    public int NextInt(int exclusiveMax) => (int)(NextUInt64() % (ulong)exclusiveMax);

    public static ulong Mix(ulong value) => new SplitMix64(value).NextUInt64();
}
