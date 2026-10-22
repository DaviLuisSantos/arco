using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;

namespace Arco.Api.Infrastructure.Push;

/// <summary>
/// Application server (VAPID) key pair. Read from Push:VapidPublicKey / Push:VapidPrivateKey (base64url);
/// when they are not configured, a pair is generated once and kept in vapid.json next to the database,
/// so existing push subscriptions keep working across restarts.
/// </summary>
public sealed class VapidKeys
{
    public const string FileName = "vapid.json";
    public const string DefaultSubject = "mailto:arco@example.com";

    private readonly byte[] _privateKey;

    private VapidKeys(byte[] publicKey, byte[] privateKey, string subject)
    {
        PublicKey = publicKey;
        _privateKey = privateKey;
        Subject = subject;
    }

    /// <summary>Uncompressed P-256 point (65 bytes).</summary>
    public byte[] PublicKey { get; }
    public string PublicKeyBase64Url => Base64Url.EncodeToString(PublicKey);
    /// <summary>"mailto:" or "https:" contact sent in the VAPID JWT (Push:Subject).</summary>
    public string Subject { get; }

    public ECDsa CreateSigner() => ECDsa.Create(new ECParameters
    {
        Curve = ECCurve.NamedCurves.nistP256,
        D = _privateKey,
        Q = new ECPoint { X = PublicKey[1..33], Y = PublicKey[33..65] },
    });

    private sealed record StoredKeys(string PublicKey, string PrivateKey);

    public static VapidKeys Load(IConfiguration config, string dataDir)
    {
        var subject = config["Push:Subject"] is { Length: > 0 } s ? s : DefaultSubject;
        var configuredPublic = config["Push:VapidPublicKey"];
        var configuredPrivate = config["Push:VapidPrivateKey"];
        if (!string.IsNullOrWhiteSpace(configuredPublic) && !string.IsNullOrWhiteSpace(configuredPrivate))
            return FromBase64Url(configuredPublic, configuredPrivate, subject);

        var path = Path.Combine(dataDir, FileName);
        if (File.Exists(path))
        {
            var stored = JsonSerializer.Deserialize<StoredKeys>(File.ReadAllText(path), JsonSerializerOptions.Web)
                         ?? throw new InvalidOperationException($"{path} is invalid.");
            return FromBase64Url(stored.PublicKey, stored.PrivateKey, subject);
        }

        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = key.ExportParameters(includePrivateParameters: true);
        var keys = new VapidKeys(WebPushCrypto.ExportUncompressed(p), p.D!, subject);
        Directory.CreateDirectory(dataDir);
        File.WriteAllText(path, JsonSerializer.Serialize(
            new StoredKeys(keys.PublicKeyBase64Url, Base64Url.EncodeToString(keys._privateKey)), JsonSerializerOptions.Web));
        return keys;
    }

    private static VapidKeys FromBase64Url(string publicKey, string privateKey, string subject)
    {
        var pub = Base64Url.DecodeFromChars(publicKey.Trim());
        var priv = Base64Url.DecodeFromChars(privateKey.Trim());
        if (!WebPushCrypto.IsValidPublicKey(pub) || priv.Length != 32)
            throw new InvalidOperationException("Invalid VAPID keys: expected a 65-byte public key and a 32-byte private key (base64url).");
        return new VapidKeys(pub, priv, subject);
    }
}
