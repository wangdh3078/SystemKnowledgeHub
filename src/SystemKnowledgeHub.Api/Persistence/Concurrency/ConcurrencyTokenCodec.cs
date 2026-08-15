using System.Buffers.Binary;

namespace SystemKnowledgeHub.Api.Persistence.Concurrency;

public sealed class ConcurrencyTokenCodec
{
    public string Encode(long version)
    {
        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version));
        }

        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, version);
        var payload = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return $"v1_{payload}";
    }

    public bool TryDecode(string? token, out long version)
    {
        version = 0;
        if (string.IsNullOrWhiteSpace(token) || !token.StartsWith("v1_", StringComparison.Ordinal))
        {
            return false;
        }

        var payload = token[3..].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + ((4 - payload.Length % 4) % 4), '=');

        try
        {
            var bytes = Convert.FromBase64String(payload);
            if (bytes.Length != sizeof(long))
            {
                return false;
            }

            version = BinaryPrimitives.ReadInt64BigEndian(bytes);
            return version >= 1;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
