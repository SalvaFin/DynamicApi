using System.Security.Claims;
using Dynamic.Fidelity.Application.Services;
using Dynamic.Fidelity.Domain.Entities;
using Dynamic.Fidelity.Domain.Enums;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Dynamic.Negocios.Domain.Enums;
using Dynamic.Negocios.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dynamic.Fidelity.Controllers;

public sealed record RecurrencePreviewRequest(Guid UserId, decimal AmountEuros, RecurrenceRule? DraftRule);

[ApiController]
[Authorize]
[Route("api/fidelity/negocios/{negocioId:guid}/recurrence-rules")]
public sealed class RecurrenceRulesController(
    DynamicFidelityDbContext db, DynamicNegociosDbContext negocios,
    RecurrenceEvaluationService evaluation) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid negocioId, CancellationToken cancellationToken)
    {
        if (!await CanEditAsync(negocioId, cancellationToken)) return Forbid();
        return Ok(await db.RecurrenceRules.AsNoTracking().Where(x => x.NegocioId == negocioId)
            .OrderByDescending(x => x.Priority).ThenBy(x => x.Id).ToListAsync(cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid negocioId, [FromBody] RecurrenceRule input,
        CancellationToken cancellationToken)
    {
        if (!await CanEditAsync(negocioId, cancellationToken)) return Forbid();
        NormalizeDates(input);
        string? error = Validate(input);
        if (error is not null) return BadRequest(new { message = error });
        DateTime now = DateTime.UtcNow;
        if (input.Id == Guid.Empty) input.Id = Guid.NewGuid();
        if (await db.RecurrenceRules.AnyAsync(x => x.Id == input.Id, cancellationToken))
            return Conflict(new { message = "El identificador de regla ya existe." });
        input.NegocioId = negocioId;
        input.CreatedAtUtc = input.UpdatedAtUtc = now;
        db.RecurrenceRules.Add(input);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(List), new { negocioId }, input);
    }

    [HttpPut("{ruleId:guid}")]
    public async Task<IActionResult> Update(Guid negocioId, Guid ruleId, [FromBody] RecurrenceRule input,
        CancellationToken cancellationToken)
    {
        if (!await CanEditAsync(negocioId, cancellationToken)) return Forbid();
        NormalizeDates(input);
        string? error = Validate(input);
        if (error is not null) return BadRequest(new { message = error });
        RecurrenceRule? rule = await db.RecurrenceRules.SingleOrDefaultAsync(x => x.Id == ruleId &&
            x.NegocioId == negocioId, cancellationToken);
        if (rule is null) return NotFound();
        rule.Name = input.Name.Trim(); rule.Active = input.Active;
        rule.Family = input.Family; rule.BenefitMode = input.BenefitMode;
        rule.Compatibility = input.Compatibility; rule.Threshold = input.Threshold;
        rule.WindowDays = input.WindowDays; rule.BenefitVisits = input.BenefitVisits;
        rule.BenefitDays = input.BenefitDays; rule.Multiplier = input.Multiplier;
        rule.MaxActivationsPerCustomer = input.MaxActivationsPerCustomer;
        rule.Priority = input.Priority; rule.StartsAtUtc = input.StartsAtUtc;
        rule.EndsAtUtc = input.EndsAtUtc; rule.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(rule);
    }

    [HttpPost("preview")]
    public async Task<IActionResult> Preview(Guid negocioId, [FromBody] RecurrencePreviewRequest request,
        CancellationToken cancellationToken)
    {
        if (!await CanEditAsync(negocioId, cancellationToken)) return Forbid();
        if (request.UserId == Guid.Empty || request.AmountEuros <= 0)
            return BadRequest(new { message = "Cliente e importe válidos son obligatorios." });
        if (request.DraftRule is not null)
        {
            NormalizeDates(request.DraftRule);
            string? draftError = Validate(request.DraftRule);
            if (draftError is not null) return BadRequest(new { message = draftError });
        }
        decimal? baseRatio = await negocios.Negocios.AsNoTracking().Where(x => x.Id == negocioId)
            .Select(x => x.RatioConversionEurosAPuntos).SingleOrDefaultAsync(cancellationToken);
        if (baseRatio is not > 0) return BadRequest(new { message = "Factor base no configurado." });
        var result = await evaluation.ResolveAsync(negocioId, request.UserId, DateTime.UtcNow,
            Guid.NewGuid(), cancellationToken, request.DraftRule);
        return Ok(new { result.VisitNumber, result.Multiplier,
            SimulatedDraft = request.DraftRule is not null,
            BasePoints = (int)decimal.Ceiling(request.AmountEuros * baseRatio.Value),
            Points = RecurrenceEvaluationService.CalculatePoints(request.AmountEuros, baseRatio.Value, result),
            Applied = result.Applied.Select(x => new { x.Rule.Id, x.Rule.Name, x.Rule.Multiplier,
                x.Rule.Priority, x.ActivatedAtUtc }),
            Eligible = result.Eligible.Select(x => x.Rule.Id) });
    }

    [HttpGet("me")]
    public async Task<IActionResult> MyProgress(Guid negocioId, CancellationToken cancellationToken)
    {
        Guid? userId = CurrentUserId();
        if (!userId.HasValue) return Unauthorized();
        var result = await evaluation.ResolveAsync(negocioId, userId.Value, DateTime.UtcNow,
            Guid.NewGuid(), cancellationToken);
        DateTime now = DateTime.UtcNow;
        List<DateTime> visitTimes = await db.PointsTransactions.AsNoTracking()
            .Where(x => x.NegocioId == negocioId && x.UserId == userId.Value &&
                x.CreatedAtUtc <= now && x.PointsAmount > 0 &&
                (x.TransactionType == PointsTransactionType.Earn ||
                 x.TransactionType == PointsTransactionType.BackofficeEarn))
            .Select(x => x.CreatedAtUtc).ToListAsync(cancellationToken);
        var rules = await db.RecurrenceRules.AsNoTracking().Where(x => x.NegocioId == negocioId && x.Active &&
            x.StartsAtUtc <= now && (!x.EndsAtUtc.HasValue || now < x.EndsAtUtc.Value))
            .OrderByDescending(x => x.Priority).ToListAsync(cancellationToken);
        return Ok(new { CompletedVisits = result.VisitNumber - 1, NextVisit = result.VisitNumber,
            ActiveMultiplier = result.Multiplier,
            ActiveBenefits = result.Applied.Select(x => x.Rule.Name),
            Rules = rules.Select(x => new { x.Name, x.Family, x.BenefitMode, x.Threshold,
                x.WindowDays, x.Multiplier, x.BenefitVisits, x.BenefitDays,
                x.MaxActivationsPerCustomer,
                Completed = x.Family == RecurrenceRuleFamily.Milestone ?
                    Math.Min(visitTimes.Count, x.Threshold) :
                    visitTimes.Count(t => t >= now.AddDays(-(x.WindowDays ?? 0))) }) });
    }

    private async Task<bool> CanEditAsync(Guid negocioId, CancellationToken cancellationToken)
    {
        Guid? userId = CurrentUserId();
        if (!userId.HasValue) return false;
        if (!await negocios.Negocios.AsNoTracking().AnyAsync(x => x.Id == negocioId &&
            !x.IsDeleted, cancellationToken)) return false;
        if (User.IsInRole("Admin")) return true;
        DateTime now = DateTime.UtcNow;
        return await negocios.NegociosUsuariosVinculaciones.AsNoTracking().AnyAsync(x =>
            x.NegocioId == negocioId && x.UserId == userId.Value && x.Activa &&
            !x.RevokedAtUtc.HasValue && (!x.FechaInicioUtc.HasValue || x.FechaInicioUtc <= now) &&
            (!x.FechaFinUtc.HasValue || x.FechaFinUtc >= now) &&
            x.TipoVinculacion == TipoVinculacionNegocioUsuario.Propietario &&
            x.PuedeGestionarNegocio, cancellationToken);
    }

    private Guid? CurrentUserId()
    {
        string? claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(claim, out Guid value) ? value : null;
    }

    private static string? Validate(RecurrenceRule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.Name) || rule.Name.Length > 120) return "Nombre inválido.";
        if (!Enum.IsDefined(rule.Family) || !Enum.IsDefined(rule.BenefitMode) ||
            !Enum.IsDefined(rule.Compatibility)) return "Tipo o compatibilidad inválidos.";
        if (rule.Multiplier < 1m || rule.Multiplier > 5m || rule.Threshold < 1 || rule.Threshold > 1000)
            return "Factor o umbral fuera de rango.";
        if (rule.StartsAtUtc == default || rule.EndsAtUtc.HasValue && rule.EndsAtUtc <= rule.StartsAtUtc)
            return "La vigencia debe tener un inicio y un fin posterior.";
        if (rule.Family == RecurrenceRuleFamily.Milestone && rule.BenefitMode is not
            (RecurrenceBenefitMode.CurrentVisit or RecurrenceBenefitMode.NextVisits or
             RecurrenceBenefitMode.Duration or RecurrenceBenefitMode.Permanent))
            return "Modalidad no válida para hito.";
        if (rule.Family == RecurrenceRuleFamily.Frequency && (rule.Threshold < 2 ||
            rule.WindowDays is not > 0 or > 365 || rule.BenefitMode is not
            (RecurrenceBenefitMode.OncePerWindow or RecurrenceBenefitMode.RenewOnThreshold or
             RecurrenceBenefitMode.Duration))) return "Frecuencia o ventana inválidas.";
        if (rule.BenefitMode == RecurrenceBenefitMode.NextVisits && rule.BenefitVisits is not > 0 or > 100)
            return "Número de visitas posteriores inválido.";
        if (rule.BenefitMode == RecurrenceBenefitMode.Duration && rule.BenefitDays is not > 0 or > 365)
            return "Duración inválida.";
        if (rule.MaxActivationsPerCustomer is <= 0 or > 1000)
            return "Límite de activaciones inválido.";
        return null;
    }

    private static void NormalizeDates(RecurrenceRule rule)
    {
        rule.StartsAtUtc = AsUtc(rule.StartsAtUtc);
        if (rule.EndsAtUtc.HasValue) rule.EndsAtUtc = AsUtc(rule.EndsAtUtc.Value);
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
