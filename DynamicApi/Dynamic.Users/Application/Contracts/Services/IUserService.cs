using Dynamic.Users.Application.Common;
using Dynamic.Users.Application.DTOs.Requests;
using Dynamic.Users.Application.DTOs.Responses;

namespace Dynamic.Users.Application.Contracts.Services;

public interface IUserService
{
    Task<ServiceResult<UserSummaryResponse>> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ServiceResult<BusinessCustomerLookupResponse>> SearchBusinessCustomerByContactAsync(
        Guid requesterUserId,
        bool isAdmin,
        BusinessCustomerSearchRequest request,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<BusinessCustomerLookupResponse>> GetBusinessCustomerByIdAsync(
        Guid requesterUserId,
        bool isAdmin,
        Guid customerUserId,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<UserSummaryResponse>> UpdateProfileAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<IReadOnlyCollection<UserSessionResponse>>> GetActiveSessionsAsync(
        Guid userId,
        Guid currentSessionId,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<UserSessionResponse>> UpdatePushTokenAsync(
        Guid userId,
        Guid sessionId,
        UpdatePushTokenRequest request,
        CancellationToken cancellationToken = default);
}
