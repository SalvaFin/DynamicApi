using System.Security.Claims;
using Dynamic.Users.Application.Common;
using Dynamic.Users.Application.Contracts.Services;
using Dynamic.Users.Application.DTOs.Requests;
using Dynamic.Users.Application.DTOs.Responses;
using Dynamic.Users.Application.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Dynamic.Users.Controllers;

[ApiController]
[Route("api/users/auth")]
public class UsersAuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserService _userService;
    private readonly UserRegistrationOptions _userRegistrationOptions;

    public UsersAuthController(IAuthService authService, IUserService userService, IOptions<UserRegistrationOptions> userRegistrationOptions)
    {
        _authService = authService;
        _userService = userService;
        _userRegistrationOptions = userRegistrationOptions.Value;
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
        ServiceResult<CompleteRegistrationResponse> result = await _authService.CompleteRegistrationAsync(request, cancellationToken);
        return ToActionResult(result, Ok);
    }

    [AllowAnonymous]
    [HttpPost("register/classic")]
    public async Task<IActionResult> ClassicRegister([FromBody] ClassicRegisterRequest request, CancellationToken cancellationToken)
    {
        bool isAuthenticatedAdmin = User.Identity?.IsAuthenticated == true && User.IsInRole("Admin");
        bool hasValidBootstrapKey =
            !string.IsNullOrWhiteSpace(_userRegistrationOptions.ClassicRegisterBootstrapKey) &&
            string.Equals(
                _userRegistrationOptions.ClassicRegisterBootstrapKey,
                request.BootstrapKey?.Trim(),
                StringComparison.Ordinal);

        if (!isAuthenticatedAdmin && !hasValidBootstrapKey)
        {
            return Unauthorized(new
            {
                message = "El registro cl\u00e1sico requiere un administrador autenticado o una bootstrap key v\u00e1lida."
            });
        }

        ServiceResult<UserSummaryResponse> result =
            await _authService.ClassicRegisterAsync(request, isAuthenticatedAdmin || hasValidBootstrapKey, cancellationToken);

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
