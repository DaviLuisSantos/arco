using System.Buffers.Binary;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Arco.Api.Infrastructure.Push;

/// <summary>
/// Web Push message encryption (RFC 8291, "aes128gcm" content coding from RFC 8188)
/// and VAPID authorization (RFC 8292), using only the .NET crypto APIs.
/// </summary>
public static class WebPushCrypto
{
    public const int RecordSize = 4096;
    private const int UncompressedPointLength = 65;

    /// <summary>Encrypts <paramref name="payload"/> for a subscription as a single aes128gcm record.</summary>
    /// <param name="uaPublicKey">Subscription "p256dh" key (uncompressed P-256 point).</param>
    /// <param name="authSecret">Subscription "auth" secret (16 bytes).</param>
    /// <param name="serverKey">Ephemeral key; a new one is generated when null (tests pass a fixed one).</param>
    /// <param name="salt">16 random bytes; generated when null.</param>
    public static byte[] Encrypt(byte[] payload, byte[] uaPublicKey, byte[] authSecret,
        ECDiffieHellman? serverKey = null, byte[]? salt = null)
    {
        if (authSecret.Length != 16) throw new ArgumentException("auth secret must be 16 bytes", nameof(authSecret));
        salt ??= RandomNumberGenerator.GetBytes(16);
        if (salt.Length != 16) throw new ArgumentException("salt must be 16 bytes", nameof(salt));
        // payload + 0x02 delimiter + 16-byte tag must fit in one record.
        if (payload.Length > RecordSize - 16 - 1 - 86) throw new ArgumentException("payload too large", nameof(payload));

        var ownsKey = serverKey is null;
        var server = serverKey ?? ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            var asPublicKey = ExportUncompressed(server.ExportParameters(false));
            using var ua = ImportPublicKey(uaPublicKey);
            var (cek, nonce) = DeriveKeys(server.DeriveRawSecretAgreement(ua.PublicKey), authSecret, uaPublicKey,
                asPublicKey, salt);

            var plaintext = new byte[payload.Length + 1];
            payload.CopyTo(plaintext, 0);
            plaintext[^1] = 0x02; // last (and only) record

            var body = new byte[16 + 4 + 1 + UncompressedPointLength + plaintext.Length + 16];
            salt.CopyTo(body, 0);
            BinaryPrimitives.WriteUInt32BigEndian(body.AsSpan(16, 4), RecordSize);
            body[20] = UncompressedPointLength;
            asPublicKey.CopyTo(body, 21);
            var headerLength = 21 + UncompressedPointLength;
            using var aes = new AesGcm(cek, 16);
            aes.Encrypt(nonce, plaintext, body.AsSpan(headerLength, plaintext.Length),
                body.AsSpan(headerLength + plaintext.Length, 16));
            return body;
        }
        finally
        {
            if (ownsKey) server.Dispose();
        }
    }

    /// <summary>RFC 8291 §3.3–3.4: content encryption key and nonce.</summary>
    internal static (byte[] Cek, byte[] Nonce) DeriveKeys(byte[] ecdhSecret, byte[] authSecret, byte[] uaPublicKey,
        byte[] asPublicKey, byte[] salt)
    {
        var keyInfo = Concat("WebPush: info\0"u8.ToArray(), uaPublicKey, asPublicKey);
        var ikm = HKDF.DeriveKey(HashAlgorithmName.SHA256, ecdhSecret, 32, authSecret, keyInfo);
        var prk = HKDF.Extract(HashAlgorithmName.SHA256, ikm, salt);
        var cek = HKDF.Expand(HashAlgorithmName.SHA256, prk, 16, "Content-Encoding: aes128gcm\0"u8.ToArray());
        var nonce = HKDF.Expand(HashAlgorithmName.SHA256, prk, 12, "Content-Encoding: nonce\0"u8.ToArray());
        return (cek, nonce);
    }

    /// <summary>Value of the Authorization header: <c>vapid t=&lt;JWT&gt;, k=&lt;public key&gt;</c>.</summary>
    public static string VapidAuthorization(string endpoint, string subject, ECDsa signingKey, byte[] publicKey,
        DateTimeOffset now)
    {
        var uri = new Uri(endpoint);
        var header = Base64Url.EncodeToString("""{"typ":"JWT","alg":"ES256"}"""u8);
        var claims = Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["aud"] = uri.GetLeftPart(UriPartial.Authority),
            ["exp"] = now.AddHours(12).ToUnixTimeSeconds(),
            ["sub"] = subject,
        }));
        var signingInput = $"{header}.{claims}";
        // ES256 = ECDSA P-256 + SHA-256, signature as r || s (IEEE P1363).
        var signature = signingKey.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"vapid t={signingInput}.{Base64Url.EncodeToString(signature)}, k={Base64Url.EncodeToString(publicKey)}";
    }

    public static bool IsValidPublicKey(byte[]? key) =>
        key is { Length: UncompressedPointLength } && key[0] == 0x04;

    public static ECDiffieHellman ImportPublicKey(byte[] uncompressed)
    {
        if (!IsValidPublicKey(uncompressed)) throw new ArgumentException("expected an uncompressed P-256 point");
        return ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = uncompressed[1..33], Y = uncompressed[33..65] },
        });
    }

    public static byte[] ExportUncompressed(ECParameters p) => Concat([0x04], p.Q.X!, p.Q.Y!);

    private static byte[] Concat(params byte[][] parts)
    {
        var result = new byte[parts.Sum(p => p.Length)];
        var offset = 0;
        foreach (var part in parts)
        {
            part.CopyTo(result, offset);
            offset += part.Length;
        }
        return result;
    }
}
