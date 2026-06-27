using Dynamic.Users.Application.Models;
using Dynamic.Users.Domain.Enums;

namespace Dynamic.Users.Application.Contracts.Services;

public interface IExternalAuthTokenValidator
{
    Task<ExternalAuthTokenPayload?> ValidateAsync(
        ExternalAuthProvider provider,
        string idToken,
        string? expectedNonce = null,
        CancellationToken cancellationToken = default);
}
