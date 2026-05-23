using System.Security.Claims;
using Dynamic.Fidelity.Application.Common;
using Dynamic.Fidelity.Application.Contracts.Services;
using Dynamic.Fidelity.Application.DTOs.Requests;
using Dynamic.Fidelity.Application.DTOs.Responses;
using Dynamic.Fidelity.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Dynamic.Fidelity.Controllers;

[ApiController]
[Authorize(Policy = "BusinessStaffAuth")]
[Route("api/backoffice/negocios/{negocioId:guid}/tickets")]
public class BusinessBackofficeTicketsController : ControllerBase
{
    private readonly ITicketService _ticketService;

    public BusinessBackofficeTicketsController(ITicketService ticketService)
    {
        _ticketService = ticketService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        Guid negocioId,
        [FromBody] CreateTicketRequest request,
        CancellationToken cancellationToken)
    {
        Guid? requesterUserId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        if (!requesterUserId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult<TicketResponse> result = await CreateTicketFromRequestAsync(
            negocioId,
            requesterUserId.Value,
            User.IsInRole("Admin"),
            request,
            cancellationToken);

        return ToActionResult(result, Created);
    }

    [HttpPost("general")]
    public async Task<IActionResult> CreateGeneral(
        Guid negocioId,
        [FromBody] CreateBackofficeTicketByCategoryRequest request,
        CancellationToken cancellationToken)
        => await CreateByCategory(
            negocioId,
            request,
            CategoriaEnvioTicket.General,
            cancellationToken);

    [HttpPost("welcome")]
    public async Task<IActionResult> CreateWelcome(
        Guid negocioId,
        [FromBody] CreateBackofficeTicketByCategoryRequest request,
        CancellationToken cancellationToken)
    {
        Guid? requesterUserId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        if (!requesterUserId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult<TicketResponse> result = await _ticketService.CreateWelcomeTicketAsync(
            negocioId,
            requesterUserId.Value,
            User.IsInRole("Admin"),
            ToCreateTicketRequest(request, CategoriaEnvioTicket.PrimerRegistro),
            cancellationToken);

        return ToActionResult(result, Created);
    }

    [HttpPost("referral")]
    public async Task<IActionResult> CreateReferral(
        Guid negocioId,
        [FromBody] CreateBackofficeTicketByCategoryRequest request,
        CancellationToken cancellationToken)
    {
        Guid? requesterUserId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        if (!requesterUserId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult<TicketResponse> result = await _ticketService.CreateReferralTicketAsync(
            negocioId,
            requesterUserId.Value,
            User.IsInRole("Admin"),
            ToCreateTicketRequest(request, CategoriaEnvioTicket.InvitacionClienteNuevo),
            cancellationToken);

        return ToActionResult(result, Created);
    }

    [HttpPost("percentage-discounts")]
    public async Task<IActionResult> CreatePercentageDiscount(
        Guid negocioId,
        [FromBody] CreateBackofficeTicketByTypeRequest request,
        CancellationToken cancellationToken)
        => await CreateByType(
            negocioId,
            request,
            TipoTicket.DescuentoPorcentual,
            cancellationToken);

    [HttpPost("fixed-discounts")]
    public async Task<IActionResult> CreateFixedDiscount(
        Guid negocioId,
        [FromBody] CreateBackofficeTicketByTypeRequest request,
        CancellationToken cancellationToken)
        => await CreateByType(
            negocioId,
            request,
            TipoTicket.DescuentoImporteFijo,
            cancellationToken);

    [HttpPost("gifts")]
    public async Task<IActionResult> CreateGift(
        Guid negocioId,
        [FromBody] CreateBackofficeTicketByTypeRequest request,
        CancellationToken cancellationToken)
        => await CreateByType(
            negocioId,
            request,
            TipoTicket.Regalo,
            cancellationToken);

    [HttpPost("two-for-one")]
    public async Task<IActionResult> CreateTwoForOne(
        Guid negocioId,
        [FromBody] CreateBackofficeTicketByTypeRequest request,
        CancellationToken cancellationToken)
        => await CreateByType(
            negocioId,
            request,
            TipoTicket.DosPorUno,
            cancellationToken);

    [HttpPost("specials")]
    public async Task<IActionResult> CreateSpecial(
        Guid negocioId,
        [FromBody] CreateBackofficeTicketByTypeRequest request,
        CancellationToken cancellationToken)
        => await CreateByType(
            negocioId,
            request,
            TipoTicket.Especial,
            cancellationToken);

    private async Task<IActionResult> CreateByCategory(
        Guid negocioId,
        CreateBackofficeTicketByCategoryRequest request,
        CategoriaEnvioTicket category,
        CancellationToken cancellationToken)
    {
        Guid? requesterUserId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        if (!requesterUserId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult<TicketResponse> result = await CreateTicketFromRequestAsync(
            negocioId,
            requesterUserId.Value,
            User.IsInRole("Admin"),
            ToCreateTicketRequest(request, category),
            cancellationToken);

        return ToActionResult(result, Created);
    }

    private async Task<IActionResult> CreateByType(
        Guid negocioId,
        CreateBackofficeTicketByTypeRequest request,
        TipoTicket type,
        CancellationToken cancellationToken)
    {
        Guid? requesterUserId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        if (!requesterUserId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult<TicketResponse> result = await CreateTicketFromRequestAsync(
            negocioId,
            requesterUserId.Value,
            User.IsInRole("Admin"),
            ToCreateTicketRequest(request, type),
            cancellationToken);

        return ToActionResult(result, Created);
    }

    private async Task<ServiceResult<TicketResponse>> CreateTicketFromRequestAsync(
        Guid negocioId,
        Guid requesterUserId,
        bool isAdmin,
        CreateTicketRequest request,
        CancellationToken cancellationToken)
        => request.CategoriaEnvioEspecial switch
        {
            CategoriaEnvioTicket.PrimerRegistro => await _ticketService.CreateWelcomeTicketAsync(
                negocioId,
                requesterUserId,
                isAdmin,
                request,
                cancellationToken),
            CategoriaEnvioTicket.InvitacionClienteNuevo => await _ticketService.CreateReferralTicketAsync(
                negocioId,
                requesterUserId,
                isAdmin,
                request,
                cancellationToken),
            _ => await _ticketService.CreateAsync(
                negocioId,
                requesterUserId,
                isAdmin,
                request,
                cancellationToken)
        };

    private IActionResult ToActionResult<T>(ServiceResult<T> result, Func<T, IActionResult> onSuccess)
    {
        if (result.Succeeded && result.Data is not null)
        {
            return onSuccess(result.Data);
        }

        return result.ErrorCode switch
        {
            "validation_error" => BadRequest(new { message = result.ErrorMessage }),
            "not_found" => NotFound(new { message = result.ErrorMessage }),
            "conflict" => Conflict(new { message = result.ErrorMessage }),
            "forbidden" => StatusCode(StatusCodes.Status403Forbidden, new { message = result.ErrorMessage }),
            _ => StatusCode(StatusCodes.Status500InternalServerError, new { message = result.ErrorMessage ?? "Error interno del servidor." })
        };
    }

    private IActionResult Created(TicketResponse response)
        => StatusCode(StatusCodes.Status201Created, response);

    private static CreateTicketRequest ToCreateTicketRequest(
        CreateBackofficeTicketByCategoryRequest request,
        CategoriaEnvioTicket category)
        => new()
        {
            Nombre = request.Nombre,
            Descripcion = request.Descripcion,
            Tipo = request.Tipo,
            CategoriaEnvioEspecial = category,
            Valor = request.Valor,
            PuntosCoste = request.PuntosCoste,
            MaxUsosPorCliente = request.MaxUsosPorCliente,
            ValidezDiasDesdeAsignacion = request.ValidezDiasDesdeAsignacion,
            Activo = request.Activo,
            Publicado = request.Publicado,
            EsDeUnSoloUso = request.EsDeUnSoloUso,
            RequiereValidacionManual = request.RequiereValidacionManual,
            AvailableFromUtc = request.AvailableFromUtc,
            ExpiresAtUtc = request.ExpiresAtUtc
        };

    private static CreateTicketRequest ToCreateTicketRequest(
        CreateBackofficeTicketByTypeRequest request,
        TipoTicket type)
        => new()
        {
            Nombre = request.Nombre,
            Descripcion = request.Descripcion,
            Tipo = type,
            CategoriaEnvioEspecial = request.CategoriaEnvioEspecial,
            Valor = request.Valor,
            PuntosCoste = request.PuntosCoste,
            MaxUsosPorCliente = request.MaxUsosPorCliente,
            ValidezDiasDesdeAsignacion = request.ValidezDiasDesdeAsignacion,
            Activo = request.Activo,
            Publicado = request.Publicado,
            EsDeUnSoloUso = request.EsDeUnSoloUso,
            RequiereValidacionManual = request.RequiereValidacionManual,
            AvailableFromUtc = request.AvailableFromUtc,
            ExpiresAtUtc = request.ExpiresAtUtc
        };

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
}
