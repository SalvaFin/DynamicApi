using Dynamic.Fidelity.Application.Contracts.Repositories;
using Dynamic.Fidelity.Application.Contracts.Services;
using Dynamic.Fidelity.Domain.Entities;
using Dynamic.Fidelity.Domain.Enums;
using Dynamic.Fidelity.Infrastructure.Persistence;

namespace Dynamic.Fidelity.Application.Services;

public class RegistrationRewardService : IRegistrationRewardService
{
    private readonly DynamicFidelityDbContext _dbContext;
    private readonly IQrCampaignRepository _qrCampaignRepository;
    private readonly IPendingTicketAssignmentRepository _pendingTicketAssignmentRepository;
    private readonly ITicketRepository _ticketRepository;

    public RegistrationRewardService(
        DynamicFidelityDbContext dbContext,
        IQrCampaignRepository qrCampaignRepository,
        IPendingTicketAssignmentRepository pendingTicketAssignmentRepository,
        ITicketRepository ticketRepository)
    {
        _dbContext = dbContext;
        _qrCampaignRepository = qrCampaignRepository;
        _pendingTicketAssignmentRepository = pendingTicketAssignmentRepository;
        _ticketRepository = ticketRepository;
    }

    public async Task<bool> ValidateQrTokenAsync(string qrToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(qrToken))
        {
            return false;
        }

        QrCampaign? campaign = await _qrCampaignRepository.GetByTokenAsync(qrToken.Trim(), cancellationToken);
        return IsCampaignValid(campaign);
    }

    public async Task PreparePendingAssignmentAsync(Guid userId, string qrToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(qrToken))
        {
            return;
        }

        QrCampaign? campaign = await _qrCampaignRepository.GetByTokenAsync(qrToken.Trim(), cancellationToken);
        if (!IsCampaignValid(campaign) || !campaign!.WelcomeTicketTemplateId.HasValue)
        {
            return;
        }

        PendingTicketAssignment? existingAssignment = await _pendingTicketAssignmentRepository.GetByUserAndCampaignAsync(userId, campaign.Id, cancellationToken);
        if (existingAssignment is not null)
        {
            return;
        }

        PendingTicketAssignment assignment = new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            NegocioId = campaign.NegocioId,
            QrCampaignId = campaign.Id,
            TicketTemplateId = campaign.WelcomeTicketTemplateId.Value,
            QrToken = campaign.Token,
            Activated = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _pendingTicketAssignmentRepository.AddAsync(assignment, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task FinalizePendingAssignmentsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<PendingTicketAssignment> pendingAssignments =
            await _pendingTicketAssignmentRepository.GetPendingByUserIdAsync(userId, cancellationToken);

        if (pendingAssignments.Count == 0)
        {
            return;
        }

        DateTime now = DateTime.UtcNow;

        foreach (PendingTicketAssignment assignment in pendingAssignments)
        {
            Ticket? template = await _ticketRepository.GetByIdAsync(assignment.TicketTemplateId, cancellationToken);
            if (template is null)
            {
                continue;
            }

            Ticket assignedTicket = new()
            {
                Id = Guid.NewGuid(),
                NegocioId = template.NegocioId,
                UserId = userId,
                ParentTicketId = template.Id,
                SourceQrCampaignId = assignment.QrCampaignId,
                Nombre = template.Nombre,
                Descripcion = template.Descripcion,
                Tipo = template.Tipo,
                Valor = template.Valor,
                CodigoInterno = template.CodigoInterno,
                CodigoVisible = $"{template.CodigoVisible ?? "WELCOME"}-{Guid.NewGuid():N}"[..20],
                TituloCanje = template.TituloCanje,
                InstruccionesCanje = template.InstruccionesCanje,
                CondicionesUso = template.CondicionesUso,
                MensajeMarketing = template.MensajeMarketing,
                DescuentoPorcentaje = template.DescuentoPorcentaje,
                DescuentoImporteFijo = template.DescuentoImporteFijo,
                BeneficioEspecialResumen = template.BeneficioEspecialResumen,
                BeneficioEspecialDetalle = template.BeneficioEspecialDetalle,
                GastoMinimoRequerido = template.GastoMinimoRequerido,
                PuntosCoste = template.PuntosCoste,
                MaxUsosPorCliente = template.MaxUsosPorCliente,
                UsosConsumidos = 0,
                ValidezDiasDesdeAsignacion = template.ValidezDiasDesdeAsignacion,
                RequiereValidacionManual = template.RequiereValidacionManual,
                EsDeUnSoloUso = template.EsDeUnSoloUso,
                EsPlantilla = false,
                Activo = template.Activo,
                Publicado = template.Publicado,
                Usado = false,
                CreatedAtUtc = now,
                AvailableFromUtc = template.AvailableFromUtc ?? now,
                ExpiresAtUtc = ResolveAssignedExpiration(template, now),
                UpdatedAtUtc = now
            };

            await _ticketRepository.AddAsync(assignedTicket, cancellationToken);

            assignment.AssignedTicketId = assignedTicket.Id;
            assignment.Activated = true;
            assignment.ActivatedAtUtc = now;
            _pendingTicketAssignmentRepository.Update(assignment);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Ticket?> ClaimTicketFromQrAsync(Guid userId, string qrToken, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(qrToken))
        {
            return null;
        }

        QrCampaign? campaign = await _qrCampaignRepository.GetByTokenAsync(qrToken.Trim(), cancellationToken);
        if (!IsCampaignValid(campaign) || !campaign!.WelcomeTicketTemplateId.HasValue)
        {
            return null;
        }

        PendingTicketAssignment? existingAssignment =
            await _pendingTicketAssignmentRepository.GetByUserAndCampaignAsync(userId, campaign.Id, cancellationToken);

        if (existingAssignment?.Activated == true && existingAssignment.AssignedTicketId.HasValue)
        {
            return await _ticketRepository.GetByIdAsync(existingAssignment.AssignedTicketId.Value, cancellationToken);
        }

        if (existingAssignment is null)
        {
            existingAssignment = new PendingTicketAssignment
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                NegocioId = campaign.NegocioId,
                QrCampaignId = campaign.Id,
                TicketTemplateId = campaign.WelcomeTicketTemplateId.Value,
                QrToken = campaign.Token,
                Activated = false,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _pendingTicketAssignmentRepository.AddAsync(existingAssignment, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        Ticket? template = await _ticketRepository.GetByIdAsync(existingAssignment.TicketTemplateId, cancellationToken);
        if (template is null)
        {
            return null;
        }

        DateTime now = DateTime.UtcNow;
        Ticket assignedTicket = new()
        {
            Id = Guid.NewGuid(),
            NegocioId = template.NegocioId,
            UserId = userId,
            ParentTicketId = template.Id,
            SourceQrCampaignId = campaign.Id,
            Nombre = template.Nombre,
            Descripcion = template.Descripcion,
            Tipo = template.Tipo,
            Valor = template.Valor,
            CodigoInterno = template.CodigoInterno,
            CodigoVisible = $"{template.CodigoVisible ?? "TICKET"}-{Guid.NewGuid():N}"[..20],
            TituloCanje = template.TituloCanje,
            InstruccionesCanje = template.InstruccionesCanje,
            CondicionesUso = template.CondicionesUso,
            MensajeMarketing = template.MensajeMarketing,
            DescuentoPorcentaje = template.DescuentoPorcentaje,
            DescuentoImporteFijo = template.DescuentoImporteFijo,
            BeneficioEspecialResumen = template.BeneficioEspecialResumen,
            BeneficioEspecialDetalle = template.BeneficioEspecialDetalle,
            GastoMinimoRequerido = template.GastoMinimoRequerido,
            PuntosCoste = template.PuntosCoste,
            MaxUsosPorCliente = template.MaxUsosPorCliente,
            UsosConsumidos = 0,
            ValidezDiasDesdeAsignacion = template.ValidezDiasDesdeAsignacion,
            RequiereValidacionManual = template.RequiereValidacionManual,
            EsDeUnSoloUso = template.EsDeUnSoloUso,
            EsPlantilla = false,
            Activo = template.Activo,
            Publicado = template.Publicado,
            Usado = false,
            CreatedAtUtc = now,
            AvailableFromUtc = template.AvailableFromUtc ?? now,
            ExpiresAtUtc = ResolveAssignedExpiration(template, now),
            UpdatedAtUtc = now
        };

        await _ticketRepository.AddAsync(assignedTicket, cancellationToken);

        existingAssignment.AssignedTicketId = assignedTicket.Id;
        existingAssignment.Activated = true;
        existingAssignment.ActivatedAtUtc = now;
        _pendingTicketAssignmentRepository.Update(existingAssignment);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return assignedTicket;
    }

    private static bool IsCampaignValid(QrCampaign? campaign)
    {
        if (campaign is null || !campaign.Activa)
        {
            return false;
        }

        DateTime now = DateTime.UtcNow;

        if (campaign.AvailableFromUtc.HasValue && campaign.AvailableFromUtc.Value > now)
        {
            return false;
        }

        if (campaign.Expira && campaign.ExpiresAtUtc.HasValue && campaign.ExpiresAtUtc.Value < now)
        {
            return false;
        }

        return true;
    }

    private static DateTime ResolveAssignedExpiration(Ticket template, DateTime assignedAtUtc)
        => template.ValidezDiasDesdeAsignacion.HasValue
            ? assignedAtUtc.AddDays(template.ValidezDiasDesdeAsignacion.Value)
            : template.ExpiresAtUtc;
}
