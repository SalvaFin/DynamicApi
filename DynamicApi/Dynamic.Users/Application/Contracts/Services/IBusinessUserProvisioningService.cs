using Dynamic.Users.Application.Common;
using Dynamic.Users.Application.DTOs.Requests;
using Dynamic.Users.Application.DTOs.Responses;

namespace Dynamic.Users.Application.Contracts.Services;

public interface IBusinessUserProvisioningService
{
    Task<ServiceResult<IReadOnlyCollection<BusinessUserAccountResponse>>> GetBusinessAccountsByAdminAsync(
        Guid negocioId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyCollection<BusinessUserAccountResponse>>> GetBusinessAccountsByOwnerAsync(
        Guid negocioId, Guid requesterUserId, CancellationToken cancellationToken = default);

    Task<ServiceResult<ProvisionedBusinessUserResponse>> CreateOwnerAccountByOwnerAsync(
        Guid negocioId, Guid requesterUserId, CreateBusinessManagedUserRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProvisionedBusinessUserResponse>> UpdateBusinessAccountByOwnerAsync(
        Guid negocioId, Guid userId, Guid requesterUserId, UpdateBusinessManagedUserRequest request,
        string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);

    Task<ServiceResult> UnlinkBusinessAccountByOwnerAsync(
        Guid negocioId, Guid userId, Guid requesterUserId,
        string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyCollection<BusinessUserAccountAuditResponse>>> GetBusinessAccountAuditByOwnerAsync(
        Guid negocioId, Guid userId, Guid requesterUserId, CancellationToken cancellationToken = default);

    Task<ServiceResult<ProvisionedBusinessUserResponse>> CreateOwnerAccountByAdminAsync(
        Guid negocioId,
        CreateBusinessManagedUserRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProvisionedBusinessUserResponse>> CreateWorkerAccountByAdminAsync(
        Guid negocioId,
        CreateBusinessManagedUserRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProvisionedBusinessUserResponse>> UpdateBusinessAccountByAdminAsync(
        Guid negocioId,
        Guid userId,
        Guid modifiedByUserId,
        UpdateBusinessManagedUserRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> UnlinkBusinessAccountByAdminAsync(
        Guid negocioId, Guid userId, Guid modifiedByUserId,
        string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyCollection<BusinessUserAccountAuditResponse>>> GetBusinessAccountAuditByAdminAsync(
        Guid negocioId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProvisionedBusinessUserResponse>> CreateWorkerAccountByOwnerAsync(
        Guid negocioId,
        Guid requesterUserId,
        bool isAdmin,
        CreateBusinessManagedUserRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<BusinessCustomerRegistrationResponse>> CreateCustomerByBusinessStaffAsync(
        Guid negocioId,
        Guid requesterUserId,
        bool isAdmin,
        CreateBusinessCustomerUserRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);
}
