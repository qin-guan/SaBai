using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace SaBai.API.Sync;

/// <summary>
/// Mints short-lived ES256 JWTs for PowerSync client connections and exposes the matching
/// public key as a JWKS document at <c>/.well-known/jwks.json</c>.
/// </summary>
public sealed class SyncTokenService : IDisposable
{
    private const string KeyId = "sabai-sync-1";
    private const string Issuer = "sabai-api";
    private const string Audience = "powersync";

    private readonly ECDsa _key;
    private readonly string _jwksJson;

    public SyncTokenService(IConfiguration configuration)
    {
        var pem = configuration["Sync:PrivateKeyPem"];
        _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        if (!string.IsNullOrWhiteSpace(pem))
        {
            _key.ImportFromPem(pem);
        }

        var parameters = _key.ExportParameters(includePrivateParameters: false);
        _jwksJson = JsonSerializer.Serialize(
            new
            {
                keys = new[]
                {
                    new
                    {
                        kty = "EC",
                        crv = "P-256",
                        alg = "ES256",
                        use = "sig",
                        kid = KeyId,
                        x = WebEncoders.Base64UrlEncode(parameters.Q.X!),
                        y = WebEncoders.Base64UrlEncode(parameters.Q.Y!),
                    },
                },
            }
        );
    }

    public string GetJwksJson() => _jwksJson;

    public (string Token, DateTimeOffset ExpiresAt) CreateToken(
        Guid userId,
        IReadOnlyList<string> roles
    )
    {
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(15);

        var header = WebEncoders.Base64UrlEncode(
            Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(new { alg = "ES256", typ = "JWT", kid = KeyId })
            )
        );
        var payload = WebEncoders.Base64UrlEncode(
            Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(
                    new
                    {
                        sub = userId.ToString(),
                        iss = Issuer,
                        aud = Audience,
                        roles,
                        iat = now.ToUnixTimeSeconds(),
                        exp = expiresAt.ToUnixTimeSeconds(),
                    }
                )
            )
        );

        var signingInput = $"{header}.{payload}";
        var signature = _key.SignData(
            Encoding.ASCII.GetBytes(signingInput),
            HashAlgorithmName.SHA256
        );
        return ($"{signingInput}.{WebEncoders.Base64UrlEncode(signature)}", expiresAt);
    }

    public void Dispose() => _key.Dispose();
}
