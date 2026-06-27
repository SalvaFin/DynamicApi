using System.Security.Claims;
using Dynamic.Users.Application.Common;
using Dynamic.Users.Application.Contracts.Services;
using Dynamic.Users.Application.DTOs.Requests;
using Dynamic.Users.Application.DTOs.Responses;
using Dynamic.Users.Application.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Dynamic.Users.Controllers;

[ApiController]
[Route("api/users/auth")]
public class UsersAuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserService _userService;

    public UsersAuthController(IAuthService authService, IUserService userService)
    {
        _authService = authService;
        _userService = userService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterStartRequest request, CancellationToken cancellationToken)
    {
        ServiceResult<RegisterStartResponse> result = await _authService.StartRegistrationAsync(
            request,
            GetIpAddress(),
            GetUserAgent(),
            cancellationToken);

        return ToActionResult(result, Ok);
    }

    [HttpPost("register/validate")]
    public async Task<IActionResult> CompleteRegister([FromBody] CompleteRegistrationRequest request, CancellationToken cancellationToken)
    {
        ServiceResult<CompleteRegistrationResponse> result = await _authService.CompleteRegistrationAsync(
            request,
            GetIpAddress(),
            GetUserAgent(),
            cancellationToken);

        return ToActionResult(result, Ok);
    }

    [AllowAnonymous]
    [HttpPost("register/classic")]
    public async Task<IActionResult> ClassicRegister([FromBody] ClassicRegisterRequest request, CancellationToken cancellationToken)
    {
        ServiceResult<UserSummaryResponse> result =
            await _authService.ClassicRegisterAsync(request, cancellationToken);

        return ToActionResult(result, Created);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        ServiceResult<AuthResponse> result = await _authService.LoginAsync(
            request,
            GetIpAddress(),
            GetUserAgent(),
            cancellationToken);

        return ToActionResult(result, Ok);
    }

    [HttpPost("external-login")]
    public async Task<IActionResult> ExternalLogin([FromBody] ExternalLoginRequest request, CancellationToken cancellationToken)
    {
        ServiceResult<AuthResponse> result = await _authService.ExternalLoginAsync(
            request,
            GetIpAddress(),
            GetUserAgent(),
            cancellationToken);

        return ToActionResult(result, Ok);
    }

    [HttpPost("external-register/complete")]
    public async Task<IActionResult> CompleteExternalRegister(
        [FromBody] CompleteExternalRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        ServiceResult<AuthResponse> result = await _authService.CompleteExternalRegistrationAsync(
            request,
            GetIpAddress(),
            GetUserAgent(),
            cancellationToken);

        return ToActionResult(result, Ok);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        ServiceResult<AuthResponse> result = await _authService.RefreshAsync(
            request,
            GetIpAddress(),
            GetUserAgent(),
            cancellationToken);

        return ToActionResult(result, Ok);
    }

    [HttpPost("password/forgot")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        ServiceResult<PasswordResetStartResponse> result = await _authService.RequestPasswordResetAsync(
            request,
            GetIpAddress(),
            GetUserAgent(),
            cancellationToken);

        return ToActionResult(result, Ok);
    }

    [HttpPost("password/reset")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        ServiceResult<PasswordResetResponse> result = await _authService.ResetPasswordAsync(
            request,
            GetIpAddress(),
            GetUserAgent(),
            cancellationToken);

        return ToActionResult(result, Ok);
    }

    [Authorize]
    [HttpPost("password/initial")]
    public async Task<IActionResult> SetInitialPassword([FromBody] SetInitialPasswordRequest request, CancellationToken cancellationToken)
    {
        Guid? userId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult<SetInitialPasswordResponse> result =
            await _authService.SetInitialPasswordAsync(userId.Value, request, cancellationToken);

        return ToActionResult(result, Ok);
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        Guid? userId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult result = await _authService.ChangePasswordAsync(userId.Value, request, cancellationToken);
        return ToActionResult(result, () => Ok(new { changed = true, requiresLogin = true }));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        Guid? userId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        Guid? sessionId = GetClaimGuid("session_id");
        if (!userId.HasValue || !sessionId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult result = await _authService.LogoutAsync(userId.Value, sessionId.Value, cancellationToken);
        return ToActionResult(result, () => Ok(new { logout = true }));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        Guid? userId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult<UserSummaryResponse> result = await _userService.GetCurrentUserAsync(userId.Value, cancellationToken);
        return ToActionResult(result, Ok);
    }

    [Authorize]
    [HttpPut("me")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        Guid? userId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult<UserSummaryResponse> result = await _userService.UpdateProfileAsync(userId.Value, request, cancellationToken);
        return ToActionResult(result, Ok);
    }

    [Authorize]
    [HttpGet("sessions")]
    public async Task<IActionResult> Sessions(CancellationToken cancellationToken)
    {
        Guid? userId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        Guid? sessionId = GetClaimGuid("session_id");
        if (!userId.HasValue || !sessionId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult<IReadOnlyCollection<UserSessionResponse>> result =
            await _userService.GetActiveSessionsAsync(userId.Value, sessionId.Value, cancellationToken);

        return ToActionResult(result, Ok);
    }

    [Authorize]
    [HttpPost("push-token")]
    public async Task<IActionResult> UpdatePushToken([FromBody] UpdatePushTokenRequest request, CancellationToken cancellationToken)
    {
        Guid? userId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        Guid? sessionId = GetClaimGuid("session_id");
        if (!userId.HasValue || !sessionId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult<UserSessionResponse> result =
            await _userService.UpdatePushTokenAsync(userId.Value, sessionId.Value, request, cancellationToken);

        return ToActionResult(result, Ok);
    }

    private IActionResult ToActionResult(ServiceResult result, Func<IActionResult> onSuccess)
    {
        if (result.Succeeded)
        {
            return onSuccess();
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    private IActionResult ToActionResult<T>(ServiceResult<T> result, Func<T, IActionResult> onSuccess)
    {
        if (result.Succeeded && result.Data is not null)
        {
            return onSuccess(result.Data);
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    private IActionResult MapFailure(string? errorCode, string? errorMessage)
        => errorCode switch
        {
            "validation_error" => BadRequest(new { message = errorMessage }),
            "conflict" => Conflict(new { message = errorMessage }),
            "external_registration_required" => Conflict(new
            {
                code = "external_registration_required",
                message = errorMessage,
                nextAction = "complete_external_registration"
            }),
            "not_found" => NotFound(new { message = errorMessage }),
            "locked" => StatusCode(StatusCodes.Status423Locked, new { message = errorMessage }),
            "unauthorized" => Unauthorized(new { message = errorMessage }),
            _ => StatusCode(StatusCodes.Status500InternalServerError, new { message = errorMessage ?? "Error interno del servidor." })
        };

    private string? GetIpAddress()
        => HttpContext.Connection.RemoteIpAddress?.ToString();

    private string? GetUserAgent()
        => Request.Headers.UserAgent.FirstOrDefault();

    private Guid? GetClaimGuid(params string[] claimTypes)
    {
        foreach (string claimType in claimTypes)
        {
            string? value = User.FindFirstValue(claimType);
            if (Guid.TryParse(value, out Guid parsedValue))
            {
                return parsedValue;
            }
        }

        return null;
    }

    private CreatedResult Created<T>(T payload)
        => Created(string.Empty, payload);
}
