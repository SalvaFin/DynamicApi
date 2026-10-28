using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Dynamic.Fidelity.Application.Services;
using Dynamic.Fidelity.Domain.Entities;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Dynamic.Negocios.Domain.Enums;
using Dynamic.Negocios.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dynamic.Fidelity.Controllers;

public sealed class SavePointsAccrualReasonRequest
{
    [Required, MaxLength(120)] public string Name { get; set; } = string.Empty;
    public bool UniquePerCustomer { get; set; }
    public bool Active { get; set; } = true;
}

[ApiController]
[Authorize(Policy = "BusinessStaffAuth")]
[Route("api/fidelity/negocios/{negocioId:guid}/points-accrual-reasons")]
public sealed class PointsAccrualReasonsController(DynamicFidelityDbContext db,
    DynamicNegociosDbContext negocios, PointsAccrualReasonService reasons) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid negocioId, bool includeInactive, CancellationToken cancellationToken)
    {
        if (!await CanAccessAsync(negocioId, includeInactive, cancellationToken)) return Forbid();
        return Ok(await reasons.ListAsync(negocioId, includeInactive, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid negocioId, SavePointsAccrualReasonRequest input,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessAsync(negocioId, true, cancellationToken)) return Forbid();
        string? error = await ValidateAsync(negocioId, null, input, cancellationToken);
        if (error is not null) return BadRequest(new { message = error });
        PointsAccrualReason reason = new() { Id = Guid.NewGuid(), NegocioId = negocioId,
            Name = input.Name.Trim(), UniquePerCustomer = input.UniquePerCustomer, Active = input.Active };
        db.PointsAccrualReasons.Add(reason);
        return await SaveAsync(reason, true, cancellationToken);
    }

    [HttpPut("{reasonId:guid}")]
    public async Task<IActionResult> Update(Guid negocioId, Guid reasonId, SavePointsAccrualReasonRequest input,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessAsync(negocioId, true, cancellationToken)) return Forbid();
        if (reasonId == negocioId && (!input.Active || input.Name.Trim() != PointsAccrualReason.DefaultName))
            return BadRequest(new { message = "Paso por caja es el motivo por defecto y debe permanecer activo con ese nombre." });
        string? error = await ValidateAsync(negocioId, reasonId, input, cancellationToken);
        if (error is not null) return BadRequest(new { message = error });
        PointsAccrualReason? reason = await db.PointsAccrualReasons.SingleOrDefaultAsync(x =>
            x.Id == reasonId && x.NegocioId == negocioId, cancellationToken);
        if (reason is null)
        {
            if (reasonId != negocioId) return NotFound();
            reason = PointsAccrualReason.Default(negocioId);
            db.PointsAccrualReasons.Add(reason);
        }
        reason.Name = input.Name.Trim(); reason.UniquePerCustomer = input.UniquePerCustomer;
        reason.Active = input.Active;
        return await SaveAsync(reason, false, cancellationToken);
    }

    private async Task<IActionResult> SaveAsync(PointsAccrualReason reason, bool created, CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException exception) when (exception.InnerException is MySqlConnector.MySqlException { Number: 1062 })
        {
            return Conflict(new { message = "El motivo ya existe o ha cambiado. Recarga la configuración y vuelve a intentarlo." });
        }
        return created ? CreatedAtAction(nameof(List), new { negocioId = reason.NegocioId }, reason) : Ok(reason);
    }

    private async Task<string?> ValidateAsync(Guid negocioId, Guid? reasonId,
        SavePointsAccrualReasonRequest input, CancellationToken ct)
    {
        string name = input.Name.Trim();
        if (name.Length is < 1 or > 120) return "Introduce un nombre de motivo de entre 1 y 120 caracteres.";
        if (reasonId != negocioId && name.Equals(PointsAccrualReason.DefaultName, StringComparison.OrdinalIgnoreCase) ||
            await db.PointsAccrualReasons.AnyAsync(x => x.NegocioId == negocioId && x.Id != reasonId &&
                x.Name.ToUpper() == name.ToUpper(), ct)) return "Ya existe un motivo con ese nombre en este negocio.";
        return null;
    }

    private async Task<bool> CanAccessAsync(Guid negocioId, bool edit, CancellationToken ct)
    {
        string? claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(claim, out Guid userId)) return false;
        if (!await negocios.Negocios.AnyAsync(x => x.Id == negocioId && !x.IsDeleted, ct)) return false;
        if (User.IsInRole("Admin")) return true;
        DateTime now = DateTime.UtcNow;
        return await negocios.NegociosUsuariosVinculaciones.AsNoTracking().AnyAsync(x =>
            x.NegocioId == negocioId && x.UserId == userId && x.Activa && !x.RevokedAtUtc.HasValue &&
            (!x.FechaInicioUtc.HasValue || x.FechaInicioUtc <= now) &&
            (!x.FechaFinUtc.HasValue || x.FechaFinUtc >= now) &&
            (edit ? x.TipoVinculacion == TipoVinculacionNegocioUsuario.Propietario && x.PuedeGestionarNegocio :
                x.PuedeGestionarNegocio || x.PuedeGestionarPuntos || x.PuedeValidarTickets ||
                x.TipoVinculacion == TipoVinculacionNegocioUsuario.Propietario ||
                x.TipoVinculacion == TipoVinculacionNegocioUsuario.Gerente), ct);
    }
}
