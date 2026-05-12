using System.Security.Cryptography;
using System.Text;
using Guardiao.Application.Ports.Outbound;
using Guardiao.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Guardiao.Infrastructure.Security;

public sealed class HmacSha256WebhookSignatureVerifier : IWebhookSignatureVerifier
{
    private readonly byte[] _secret;

    public HmacSha256WebhookSignatureVerifier(IOptions<VictimRegistryOptions> options)
    {
        _secret = Encoding.UTF8.GetBytes(options.Value.WebhookSecret);
    }

    public bool IsValid(string payload, string signature, DateTimeOffset timestamp)
    {
        using var hmac = new HMACSHA256(_secret);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var expectedHex = Convert.ToHexString(hash).ToLowerInvariant();
        var actualHex = signature.Trim().ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedHex),
            Encoding.UTF8.GetBytes(actualHex));
    }
}
