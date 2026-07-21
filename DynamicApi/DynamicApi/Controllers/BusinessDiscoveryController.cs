using Dynamic.Fidelity.Infrastructure.Persistence;
using Dynamic.Negocios.Application.DTOs.Responses;
using Dynamic.Negocios.Application.Mappings;
using Dynamic.Negocios.Domain.Entities;
using Dynamic.Negocios.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DynamicApi.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/negocios")]
public class BusinessDiscoveryController : ControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly DynamicNegociosDbContext _negociosDbContext;
    private readonly DynamicFidelityDbContext _fidelityDbContext;

    public BusinessDiscoveryController(
        DynamicNegociosDbContext negociosDbContext,
        DynamicFidelityDbContext fidelityDbContext)
    {
        _negociosDbContext = negociosDbContext;
        _fidelityDbContext = fidelityDbContext;
    }

    /// <summary>
    /// Devuelve los negocios publicados de una provincia ordenados por actividad Dynamic.
    /// </summary>
    [HttpGet("provincia/{provincia}")]
    public async Task<IActionResult> GetByProvince(
        string provincia,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        string normalizedProvince = provincia.Trim();
        if (string.IsNullOrWhiteSpace(normalizedProvince))
        {
            return BadRequest(new { message = "La provincia es obligatoria." });
        }

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        List<Negocio> businesses = await _negociosDbContext.Negocios
            .AsNoTracking()
            .Where(negocio =>
                !negocio.IsDeleted &&
                negocio.Activo &&
                negocio.PublicadoPortal &&
                negocio.Provincia != null &&
                negocio.Provincia.ToLower() == normalizedProvince.ToLower())
            .ToListAsync(cancellationToken);

        Guid[] businessIds = businesses.Select(negocio => negocio.Id).ToArray();

        Dictionary<Guid, int> givenByBusiness = businessIds.Length == 0
            ? []
            : await _fidelityDbContext.Tickets
                .AsNoTracking()
                .Where(ticket =>
                    businessIds.Contains(ticket.NegocioId) &&
                    !ticket.EsPlantilla &&
                    ticket.UserId.HasValue)
                .GroupBy(ticket => ticket.NegocioId)
                .Select(group => new { NegocioId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.NegocioId, item => item.Count, cancellationToken);

        Dictionary<Guid, int> usedByBusiness = businessIds.Length == 0
            ? []
            : await _fidelityDbContext.TicketRedemptions
                .AsNoTracking()
                .Where(redemption => businessIds.Contains(redemption.NegocioId))
                .GroupBy(redemption => redemption.NegocioId)
                .Select(group => new { NegocioId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.NegocioId, item => item.Count, cancellationToken);

        List<ExplorarNegocioResponse> ordered = businesses
            .Select(negocio =>
            {
                ExplorarNegocioResponse item = negocio.ToExploreResponse(null);
                item.TicketsDados = givenByBusiness.GetValueOrDefault(negocio.Id);
                item.TicketsUsados = usedByBusiness.GetValueOrDefault(negocio.Id);
                item.ActividadDynamic = item.TicketsDados + item.TicketsUsados;
                return item;
            })
            .OrderByDescending(negocio => negocio.ActividadDynamic)
            .ThenByDescending(negocio => negocio.TicketsUsados)
            .ThenBy(negocio => negocio.NombreComercial)
            .ToList();

        int totalItems = ordered.Count;
        ExplorarNegociosResponse response = new()
        {
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize),
            OrdenadoPorProximidad = false,
            OrdenadoPorDynamic = true,
            Items = ordered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(item => item.WithResolvedMediaUrls(Request))
                .ToArray()
        };

        return Ok(response);
    }
}
