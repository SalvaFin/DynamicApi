using System.Security.Claims;
using System.Security.Cryptography;
using Dynamic.Users.Application.DTOs.Requests;
using Dynamic.Users.Application.DTOs.Responses;
using Dynamic.Users.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using QRCoder;

namespace Dynamic.Users.Controllers;

[ApiController]
[Authorize(Roles = "User")]
[Route("api/users/me/points-splits")]
public class PointsSplitController : ControllerBase
{
    private const string ShareQrPrefix = "dynamic-points-share:";
    private readonly PointsSplitService _service;
    private readonly ITimeLimitedDataProtector _protector;

    public PointsSplitController(PointsSplitService service, IDataProtectionProvider protectionProvider)
    {
        _service = service;
        _protector = protectionProvider.CreateProtector("Dynamic.PointsSplit.RecipientQr.v1")
            .ToTimeLimitedDataProtector();
    }

    [HttpPost("share-qr")]
    [EnableRateLimiting("points-split-identity")]
    public async Task<IActionResult> CreateShareQr(CancellationToken cancellationToken)
    {
        Guid? userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        if (await _service.ResolveByUserIdAsync(userId.Value, cancellationToken) is null)
            return StatusCode(403, new { message = "Necesitas una cuenta de cliente con correo verificado para compartir puntos." });

        DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(5);
        string token = _protector.Protect(userId.Value.ToString("D"), TimeSpan.FromMinutes(5));
        string payload = ShareQrPrefix + token;
        using QRCodeGenerator generator = new();
        using QRCodeData qrData = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        SvgQRCode svg = new(qrData);
        return Ok(new { qrSvg = svg.GetGraphic(20), payload, expiresAtUtc });
    }

    [HttpPost("resolve-recipient")]
    [EnableRateLimiting("points-split-identity")]
    public async Task<IActionResult> ResolveRecipient(
        [FromBody] ResolveSplitRecipientRequest request, CancellationToken cancellationToken)
    {
        Guid? requesterId = CurrentUserId();
        if (requesterId is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Email) == string.IsNullOrWhiteSpace(request.ShareQrToken))
            return BadRequest(new { message = "Introduce un correo o escanea un QR de compartir puntos." });

        SplitRecipientResponse? recipient;
        if (!string.IsNullOrWhiteSpace(request.ShareQrToken))
        {
            string scanned = request.ShareQrToken.Trim();
            if (!scanned.StartsWith(ShareQrPrefix, StringComparison.Ordinal))
                return BadRequest(new { message = "Este QR no sirve para compartir puntos." });
            try
            {
                string value = _protector.Unprotect(scanned[ShareQrPrefix.Length..]);
                recipient = Guid.TryParse(value, out Guid userId)
                    ? await _service.ResolveByUserIdAsync(userId, cancellationToken)
                    : null;
            }
            catch (CryptographicException)
            {
                return BadRequest(new { message = "El QR ha caducado. Pide a tu amigo que genere uno nuevo." });
            }
        }
        else
        {
            recipient = await _service.ResolveByEmailAsync(request.Email!, cancellationToken);
        }

        if (recipient is null || recipient.UserId == requesterId)
            return NotFound(new { message = "No se ha encontrado un cliente activo y verificado con esos datos." });
        return Ok(recipient);
    }

    [HttpGet("transactions/{sourceTransactionId:guid}")]
    public async Task<IActionResult> GetSplit(Guid sourceTransactionId, CancellationToken cancellationToken)
    {
        Guid? userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        PointsSplitResponse? split = await _service.GetAsync(userId.Value, sourceTransactionId, cancellationToken);
        return split is null ? NotFound() : Ok(split);
    }

    [HttpGet("transactions/{sourceTransactionId:guid}/status")]
    public async Task<IActionResult> GetStatus(Guid sourceTransactionId, CancellationToken cancellationToken)
    {
        Guid? userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        PointsSplitStatusResponse? status = await _service.GetStatusAsync(userId.Value, sourceTransactionId, cancellationToken);
        return status is null ? NotFound() : Ok(status);
    }

    [HttpPost("transactions/{sourceTransactionId:guid}")]
    [EnableRateLimiting("points-split-create")]
    public async Task<IActionResult> CreateSplit(
        Guid sourceTransactionId, [FromBody] CreatePointsSplitRequest request,
        CancellationToken cancellationToken)
    {
        Guid? userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        (PointsSplitResponse? split, string? error) = await _service.CreateAsync(
            userId.Value, sourceTransactionId, request.RecipientEmails, cancellationToken);
        if (split is null)
            return error?.Contains("ya se ha repartido") == true
                ? Conflict(new { message = error })
                : BadRequest(new { message = error });
        return CreatedAtAction(nameof(GetSplit), new { sourceTransactionId }, split);
    }

    private Guid? CurrentUserId()
    {
        string? value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out Guid id) ? id : null;
    }
}
