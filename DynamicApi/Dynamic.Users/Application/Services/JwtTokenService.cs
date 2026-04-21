using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Dynamic.Users.Application.Contracts.Services;
using Dynamic.Users.Application.Models;
using Dynamic.Users.Application.Options;
using Dynamic.Users.Domain.Entities;
using Dynamic.Users.Domain.Enums;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Dynamic.Users.Application.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _jwtOptions;

    public JwtTokenService(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions.Value;
    }

    public GeneratedTokenEnvelope GenerateTokens(UserAccount user, UserSession session)
    {
        DateTime now = DateTime.UtcNow;
        (DateTime accessTokenExpiresAt, DateTime refreshTokenExpiresAt) = ResolveExpirationWindow(user.Role, now);
        string jwtId = Guid.NewGuid().ToString("N");

        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName),
            new(JwtRegisteredClaimNames.Jti, jwtId),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("session_id", session.Id.ToString())
        ];

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));
        }

        if (!string.IsNullOrWhiteSpace(user.PhoneNumber))
        {
            claims.Add(new Claim(ClaimTypes.MobilePhone, user.PhoneNumber));
        }

        if (session.UserDeviceId.HasValue)
        {
            claims.Add(new Claim("device_id", session.UserDeviceId.Value.ToString()));
        }

        SymmetricSecurityKey signingKey = new(Encoding.UTF8.GetBytes(_jwtOptions.Secret));
        SigningCredentials credentials = new(signingKey, SecurityAlgorithms.HmacSha256);

        JwtSecurityToken token = new(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: now,
            expires: accessTokenExpiresAt,
            signingCredentials: credentials);

        string accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        string refreshToken = GenerateSecureToken();

        return new GeneratedTokenEnvelope
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            RefreshTokenHash = HashRefreshToken(refreshToken),
            JwtId = jwtId,
            AccessTokenExpiresAtUtc = accessTokenExpiresAt,
            RefreshTokenExpiresAtUtc = refreshTokenExpiresAt
        };
    }

    private (DateTime AccessTokenExpiresAtUtc, DateTime RefreshTokenExpiresAtUtc) ResolveExpirationWindow(UserRole role, DateTime now)
    {
        if (role == UserRole.Admin)
        {
            DateTime expiresAt = now.AddHours(_jwtOptions.AdminSessionExpirationHours);
            return (expiresAt, expiresAt);
        }

        DateTime persistentExpiry = now.AddDays(_jwtOptions.NonAdminPersistentSessionDays);
        return (persistentExpiry, persistentExpiry);
    }

    public string HashRefreshToken(string refreshToken)
    {
        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken.Trim()));
        return Convert.ToHexString(hashBytes);
    }

    private static string GenerateSecureToken()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
