using System.Security.Cryptography;

namespace FileHub.IntegrationTests;

/// <summary>
/// Computes a TOTP the way an authenticator app would, so tests can complete the real 2FA setup.
/// Matches what Identity's <c>AuthenticatorTokenProvider</c> validates: HMAC-SHA1 over 30-second
/// steps, truncated to six digits, with the shared secret base32-encoded.
/// </summary>
public static class TotpCode
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private const int Digits = 6;
    private const int StepSeconds = 30;

    /// <summary>The code valid right now for <paramref name="base32Key"/> (spaces are ignored).</summary>
    public static string Current(string base32Key)
    {
        var key = FromBase32(base32Key.Replace(" ", string.Empty).Replace("-", string.Empty));
        var step = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / StepSeconds;

        var counter = BitConverter.GetBytes(step);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(counter);
        }

        var hash = HMACSHA1.HashData(key, counter);

        // RFC 4226 dynamic truncation: the low nibble of the last byte picks the 4-byte window.
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
                     | (hash[offset + 1] << 16)
                     | (hash[offset + 2] << 8)
                     | hash[offset + 3];

        return (binary % (int)Math.Pow(10, Digits)).ToString($"D{Digits}");
    }

    private static byte[] FromBase32(string value)
    {
        var bits = 0;
        var accumulator = 0;
        var bytes = new List<byte>(value.Length * 5 / 8);

        foreach (var character in value.TrimEnd('=').ToUpperInvariant())
        {
            var index = Base32Alphabet.IndexOf(character, StringComparison.Ordinal);
            Assert.True(index >= 0, $"'{character}' is not a base32 character");

            accumulator = (accumulator << 5) | index;
            bits += 5;
            if (bits < 8)
            {
                continue;
            }

            bits -= 8;
            bytes.Add((byte)(accumulator >> bits));
        }

        return [.. bytes];
    }
}
