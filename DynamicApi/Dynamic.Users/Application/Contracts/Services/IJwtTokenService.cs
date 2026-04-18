using Dynamic.Users.Application.Models;
using Dynamic.Users.Domain.Entities;

namespace Dynamic.Users.Application.Contracts.Services;

public interface IJwtTokenService
{
    GeneratedTokenEnvelope GenerateTokens(UserAccount user, UserSession session);
    string HashRefreshToken(string refreshToken);
}
