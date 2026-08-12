using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SharpCampus.Server.Common.Configuration;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Values;

namespace SharpCampus.Server.Common.Security;

// The ApiServer issues these when it pairs two players; the RoomServer verifies them at the door. A
// shared secret is enough because both ends are ours: nothing here has to survive a public audience.
public sealed class EntryTokenService(IOptions<EntryTokenOptions> options, TimeProvider time)
{
    private static readonly TimeSpan _lifetime = TimeSpan.FromSeconds(60);

    private readonly byte[] _secret = Encoding.UTF8.GetBytes(options.Value.Secret);

    public string Issue(UserId userId, RoomId roomId)
    {
        var payload = Encoding.UTF8.GetBytes(
            $"{userId}:{roomId}:{time.GetUtcNow().Add(_lifetime).ToUnixTimeSeconds()}");

        return $"{Base64Url.EncodeToString(payload)}.{Base64Url.EncodeToString(Sign(payload))}";
    }

    public bool TryValidate(string? token, out UserId userId, out RoomId roomId)
    {
        userId = default;
        roomId = default;

        if (token is null)
        {
            return false;
        }

        var separator = token.IndexOf('.');
        if (separator < 0
            || !TryDecode(token.AsSpan(0, separator), out var payload)
            || !TryDecode(token.AsSpan(separator + 1), out var signature)
            || !CryptographicOperations.FixedTimeEquals(Sign(payload), signature))
        {
            return false;
        }

        var parts = Encoding.UTF8.GetString(payload).Split(':');
        if (parts.Length != 3
            || !UserId.TryParse(parts[0], out var signedUserId)
            || !RoomId.TryParse(parts[1], out var signedRoomId)
            || !long.TryParse(parts[2], out var expiresAt)
            || expiresAt <= time.GetUtcNow().ToUnixTimeSeconds())
        {
            return false;
        }

        userId = signedUserId;
        roomId = signedRoomId;
        return true;
    }

    private byte[] Sign(byte[] payload) => HMACSHA256.HashData(_secret, payload);

    // A hand-typed or truncated token reaches this, so the shape is checked before it is decoded.
    private static bool TryDecode(ReadOnlySpan<char> text, out byte[] bytes)
    {
        if (!Base64Url.IsValid(text))
        {
            bytes = [];
            return false;
        }

        bytes = Base64Url.DecodeFromChars(text);
        return true;
    }
}
