using Dynamic.Users.Application.Common;
using Dynamic.Users.Application.DTOs.Requests;
using Dynamic.Users.Application.DTOs.Responses;

namespace Dynamic.Users.Application.Contracts.Services;

public interface IAuthService
{
    Task<ServiceResult<RegisterStartResponse>> StartRegistrationAsync(
        RegisterStartRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CompleteRegistrationResponse>> CompleteRegistrationAsync(
        CompleteRegistrationRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<UserSummaryResponse>> ClassicRegisterAsync(
        ClassicRegisterRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AuthResponse>> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AuthResponse>> ExternalLoginAsync(
        ExternalLoginRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AuthResponse>> CompleteExternalRegistrationAsync(
        CompleteExternalRegistrationRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AuthResponse>> RefreshAsync(
        RefreshTokenRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PasswordResetStartResponse>> RequestPasswordResetAsync(
        ForgotPasswordRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PasswordResetResponse>> ResetPasswordAsync(
        ResetPasswordRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<SetInitialPasswordResponse>> SetInitialPasswordAsync(
        Guid userId,
        SetInitialPasswordRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> LogoutAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default);
}
