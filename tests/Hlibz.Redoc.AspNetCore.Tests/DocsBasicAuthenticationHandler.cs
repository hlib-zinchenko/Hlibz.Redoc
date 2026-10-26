// The README's Basic authentication handler, verbatim apart from the usings a test project needs
// spelled out: shape 2a is asserted with the code a reader copies, not a cleaner stand-in.

using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hlibz.Redoc.Extensions.Tests;

// One username and password from configuration (Docs:User, Docs:Password), for the docs only.
// Serve it over HTTPS: Basic sends both, merely base64-encoded, with every request.
public sealed class DocsBasicAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DocsBasic";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        string? user = configuration["Docs:User"];
        string? password = configuration["Docs:Password"];
        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(password))
        {
            // Unconfigured means nobody gets in, never that anybody does.
            return Task.FromResult(AuthenticateResult.Fail("Docs:User/Password not set."));
        }

        byte[] given;
        try
        {
            given = Convert.FromBase64String(header["Basic ".Length..].Trim());
        }
        catch (FormatException)
        {
            return Task.FromResult(AuthenticateResult.Fail("Malformed Basic credentials."));
        }

        byte[] expected = Encoding.UTF8.GetBytes($"{user}:{password}");
        if (!CryptographicOperations.FixedTimeEquals(given, expected))
        {
            return Task.FromResult(AuthenticateResult.Fail("Wrong username or password."));
        }

        ClaimsIdentity identity = new([new Claim(ClaimTypes.Name, user)], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }

    // The WWW-Authenticate header is what makes the browser show its password prompt.
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = "Basic realm=\"API docs\", charset=\"UTF-8\"";
        return Task.CompletedTask;
    }
}
