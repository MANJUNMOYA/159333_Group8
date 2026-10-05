using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace CampusCoffeeSystem.Services;

public sealed class CoffeeModelKeyHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, IOptions<CoffeeModelOptions> modelOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "CoffeeModelKey";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var expected = modelOptions.Value.PublicApiKey;
        var authorization = Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(expected) || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.NoResult());
        var supplied = authorization[7..].Trim();
        if (supplied.Length > 256 || !CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(supplied)), SHA256.HashData(Encoding.UTF8.GetBytes(expected))))
            return Task.FromResult(AuthenticateResult.Fail("Invalid API credentials."));
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "coffee-model-api")], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
