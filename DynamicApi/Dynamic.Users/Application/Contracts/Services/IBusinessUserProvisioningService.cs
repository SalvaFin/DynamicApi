using Dynamic.Users.Application.Common;
using Dynamic.Users.Application.DTOs.Requests;
using Dynamic.Users.Application.DTOs.Responses;

namespace Dynamic.Users.Application.Contracts.Services;

public interface IBusinessUserProvisioningService
{
    Task<ServiceResult<IReadOnlyCollection<BusinessUserAccountResponse>>> GetBusinessAccountsByAdminAsync(
        Guid negocioId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProvisionedBusinessUserResponse>> CreateOwnerAccountByAdminAsync(
        Guid negocioId,
        CreateBusinessManagedUserRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProvisionedBusinessUserResponse>> CreateWorkerAccountByAdminAsync(
        Guid negocioId,
        CreateBusinessManagedUserRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProvisionedBusinessUserResponse>> CreateWorkerAccountByOwnerAsync(
        Guid negocioId,
        Guid requesterUserId,
        bool isAdmin,
        CreateBusinessManagedUserRequest request,
        CancellationToken cancellationToken = default);
}
