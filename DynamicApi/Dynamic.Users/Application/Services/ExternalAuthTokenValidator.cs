using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Dynamic.Users.Application.Contracts.Services;
using Dynamic.Users.Application.Models;
using Dynamic.Users.Application.Options;
using Dynamic.Users.Domain.Enums;
using Google.Apis.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Dynamic.Users.Application.Services;

public class ExternalAuthTokenValidator : IExternalAuthTokenValidator
{
    private const string AppleIssuer = "https://appleid.apple.com";
    private const string AppleMetadataAddress = "https://appleid.apple.com/.well-known/openid-configuration";
    private readonly ExternalAuthOptions _options;
    private readonly IConfigurationManager<OpenIdConnectConfiguration> _appleConfigurationManager;
    private readonly ILogger<ExternalAuthTokenValidator> _logger;

    public ExternalAuthTokenValidator(
        IOptions<ExternalAuthOptions> options,
        ILogger<ExternalAuthTokenValidator> logger)
    {
        _options = options.Value;
        _logger = logger;
        _appleConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            AppleMetadataAddress,
            new OpenIdConnectConfigurationRetriever());
    }

    public Task<ExternalAuthTokenPayload?> ValidateAsync(
        ExternalAuthProvider provider,
        string idToken,
        string? expectedNonce = null,
        CancellationToken cancellationToken = default)
        => provider switch
        {
            ExternalAuthProvider.Google => ValidateGoogleAsync(idToken, expectedNonce, cancellationToken),
            ExternalAuthProvider.Apple => ValidateAppleAsync(idToken, expectedNonce, cancellationToken),
            _ => Task.FromResult<ExternalAuthTokenPayload?>(null)
        };

    private async Task<ExternalAuthTokenPayload?> ValidateGoogleAsync(
        string idToken,
        string? expectedNonce,
        CancellationToken cancellationToken)
    {
        if (_options.GoogleClientIds.Count == 0)
        {
            _logger.LogWarning("ExternalAuth:GoogleClientIds no esta configurado.");
            return null;
        }

        try
        {
            GoogleJsonWebSignature.Payload payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = _options.GoogleClientIds
                });

            if (!MatchesNonce(payload.Nonce, expectedNonce))
            {
                return null;
            }

            return new ExternalAuthTokenPayload
            {
                Provider = ExternalAuthProvider.Google,
                Subject = payload.Subject,
                Email = NormalizeNullable(payload.Email),
                EmailVerified = payload.EmailVerified,
                FirstName = NormalizeNullable(payload.GivenName),
                LastName = NormalizeNullable(payload.FamilyName),
                DisplayName = NormalizeNullable(payload.Name),
                AvatarUrl = NormalizeNullable(payload.Picture),
                HostedDomain = NormalizeNullable(payload.HostedDomain),
                Nonce = NormalizeNullable(payload.Nonce)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo validar el id_token de Google.");
            return null;
        }
    }

    private async Task<ExternalAuthTokenPayload?> ValidateAppleAsync(
        string idToken,
        string? expectedNonce,
        CancellationToken cancellationToken)
    {
        if (_options.AppleClientIds.Count == 0)
        {
            _logger.LogWarning("ExternalAuth:AppleClientIds no esta configurado.");
            return null;
        }

        try
        {
            OpenIdConnectConfiguration configuration = await _appleConfigurationManager.GetConfigurationAsync(cancellationToken);
            TokenValidationParameters validationParameters = new()
            {
                ValidateIssuer = true,
                ValidIssuer = AppleIssuer,
                ValidateAudience = true,
                ValidAudiences = _options.AppleClientIds,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = configuration.SigningKeys,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2)
            };

            JwtSecurityTokenHandler tokenHandler = new();
            ClaimsPrincipal principal = tokenHandler.ValidateToken(idToken, validationParameters, out _);
            string? subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
            string? nonce = principal.FindFirstValue("nonce");

            if (string.IsNullOrWhiteSpace(subject) || !MatchesNonce(nonce, expectedNonce))
            {
                return null;
            }

            string? email = principal.FindFirstValue(JwtRegisteredClaimNames.Email) ?? principal.FindFirstValue(ClaimTypes.Email);
            bool emailVerified = ParseBoolClaim(principal.FindFirstValue("email_verified"));

            return new ExternalAuthTokenPayload
            {
                Provider = ExternalAuthProvider.Apple,
                Subject = subject,
                Email = NormalizeNullable(email),
                EmailVerified = emailVerified,
                Nonce = NormalizeNullable(nonce)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo validar el id_token de Apple.");
            return null;
        }
    }

    private static bool MatchesNonce(string? actualNonce, string? expectedNonce)
        => string.IsNullOrWhiteSpace(expectedNonce) || string.Equals(actualNonce, expectedNonce.Trim(), StringComparison.Ordinal);

    private static bool ParseBoolClaim(string? value)
        => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
