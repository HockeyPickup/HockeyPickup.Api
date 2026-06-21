using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace HockeyPickup.Api.Helpers;

public interface IRandomShuffler
{
    // In-place Fisher-Yates shuffle.
    void Shuffle<T>(IList<T> list);
}

// Cryptographically-seeded Fisher-Yates. Excluded from coverage because RandomNumberGenerator is nondeterministic;
// draw ordering is asserted in tests via an injected deterministic IRandomShuffler.
[ExcludeFromCodeCoverage]
public sealed class CryptoRandomShuffler : IRandomShuffler
{
    public void Shuffle<T>(IList<T> list)
    {
        for (var n = list.Count - 1; n > 0; n--)
        {
            var k = RandomNumberGenerator.GetInt32(n + 1);
            (list[n], list[k]) = (list[k], list[n]);
        }
    }
}
