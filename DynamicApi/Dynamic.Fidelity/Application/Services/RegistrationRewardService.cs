using Dynamic.Fidelity.Application.Contracts.Repositories;
using Dynamic.Fidelity.Application.Contracts.Services;
using Dynamic.Fidelity.Application.Models;
using Dynamic.Fidelity.Domain.Entities;
using Dynamic.Fidelity.Domain.Enums;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Dynamic.Negocios.Application.Contracts.Repositories;
using Dynamic.Negocios.Domain.Entities;
using Dynamic.Negocios.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dynamic.Fidelity.Application.Services;

public class RegistrationRewardService : IRegistrationRewardService
{
    private readonly DynamicFidelityDbContext _dbContext;
    private readonly IQrCampaignRepository _qrCampaignRepository;
    private readonly IPendingTicketAssignmentRepository _pendingTicketAssignmentRepository;
    private readonly ITicketRepository _ticketRepository;
    private readonly INegocioRepository _negocioRepository;
    private readonly DynamicNegociosDbContext _negociosDbContext;
    private readonly ITicketEventPublisher _ticketEventPublisher;

    public RegistrationRewardService(
        DynamicFidelityDbContext dbContext,
        IQrCampaignRepository qrCampaignRepository,
        IPendingTicketAssignmentRepository pendingTicketAssignmentRepository,
        ITicketRepository ticketRepository,
        INegocioRepository negocioRepository,
        DynamicNegociosDbContext negociosDbContext,
        ITicketEventPublisher ticketEventPublisher)
    {
        _dbContext = dbContext;
        _qrCampaignRepository = qrCampaignRepository;
        _pendingTicketAssignmentRepository = pendingTicketAssignmentRepository;
        _ticketRepository = ticketRepository;
        _negocioRepository = negocioRepository;
        _negociosDbContext = negociosDbContext;
        _ticketEventPublisher = ticketEventPublisher;
    }

    public async Task<bool> ValidateQrTokenAsync(string qrToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(qrToken))
        {
            return false;
        }

        QrCampaign? campaign = await _qrCampaignRepository.GetByTokenAsync(qrToken.Trim(), cancellationToken);
        if (!IsCampaignValid(campaign) || !campaign!.WelcomeTicketTemplateId.HasValue)
        {
            return false;
        }
        Ticket? template = await _ticketRepository.GetByIdAsync(campaign.WelcomeTicketTemplateId.Value, cancellationToken);
        return IsWelcomeTemplateAvailable(template, campaign.NegocioId);
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

        Ticket? template = await _ticketRepository.GetByIdAsync(campaign.WelcomeTicketTemplateId.Value, cancellationToken);
        if (!IsWelcomeTemplateAvailable(template, campaign.NegocioId))
        {
            return;
        }

        bool alreadyReceived = await _dbContext.WelcomeTicketClaims.AsNoTracking()
            .AnyAsync(claim => claim.UserId == userId && claim.NegocioId == campaign.NegocioId, cancellationToken);
        if (alreadyReceived)
        {
            return;
        }

        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT IGNORE INTO fidelity_pending_ticket_assignments (Id, UserId, NegocioId, QrCampaignId, TicketTemplateId, QrToken, Activated, CreatedAtUtc) VALUES ({Guid.NewGuid()}, {userId}, {campaign.NegocioId}, {campaign.Id}, {campaign.WelcomeTicketTemplateId.Value}, {campaign.Token}, {false}, {DateTime.UtcNow})",
            cancellationToken);
    }

    public async Task FinalizePendingAssignmentsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<PendingTicketAssignment> pendingAssignments =
            await _pendingTicketAssignmentRepository.GetPendingByUserIdAsync(userId, cancellationToken);

        if (pendingAssignments.Count == 0)
        {
            return;
        }

        foreach (PendingTicketAssignment assignment in pendingAssignments)
        {
            await ClaimTicketFromQrAsync(userId, assignment.QrToken, cancellationToken);
        }
    }

    public async Task<WelcomeTicketClaimResult?> ClaimTicketFromQrAsync(Guid userId, string qrToken, CancellationToken cancellationToken = default)
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

        WelcomeTicketClaim? previousClaim = await _dbContext.WelcomeTicketClaims.AsNoTracking()
            .SingleOrDefaultAsync(claim => claim.UserId == userId && claim.NegocioId == campaign.NegocioId, cancellationToken);
        if (previousClaim is not null)
        {
            await MarkPendingAssignmentClaimedAsync(userId, campaign.Id, previousClaim.TicketId, cancellationToken);
            Ticket? previousTicket = await _ticketRepository.GetByIdAsync(previousClaim.TicketId, cancellationToken);
            return new WelcomeTicketClaimResult(previousTicket, true);
        }

        Ticket? template = await _ticketRepository.GetByIdAsync(campaign.WelcomeTicketTemplateId.Value, cancellationToken);
        if (!IsWelcomeTemplateAvailable(template, campaign.NegocioId))
        {
            return null;
        }

        DateTime now = DateTime.UtcNow;
        WelcomeTicketClaimResult claim = await ClaimWelcomeTicketAsync(
            template!, userId, campaign, "TICKET", now, cancellationToken);
        if (claim.AlreadyClaimed || claim.Ticket is null)
        {
            return claim;
        }

        await EnsureAudienceAsync(campaign.NegocioId, userId, "welcome_ticket_qr", now, cancellationToken);
        await _negociosDbContext.SaveChangesAsync(cancellationToken);
        await _ticketEventPublisher.PublishReceivedAsync(claim.Ticket, "qr", cancellationToken);
        return claim;
    }

    public async Task<bool> AssignBusinessWelcomeTicketAsync(Guid negocioId, Guid userId, CancellationToken cancellationToken = default)
        => await AssignBusinessConfiguredTicketAsync(
            negocioId,
            userId,
            negocio => negocio.BonoBienvenidaTicketId,
            CategoriaEnvioTicket.PrimerRegistro,
            "WELCOME",
            cancellationToken);

    public async Task<bool> AssignBusinessReferralTicketAsync(Guid negocioId, Guid userId, CancellationToken cancellationToken = default)
        => await AssignBusinessConfiguredTicketAsync(
            negocioId,
            userId,
            negocio => negocio.BonoInvitacionNuevoClienteTicketId,
            CategoriaEnvioTicket.InvitacionClienteNuevo,
            "REFERRAL",
            cancellationToken);

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

    private async Task<bool> AssignBusinessConfiguredTicketAsync(
        Guid negocioId,
        Guid userId,
        Func<Negocio, Guid?> templateSelector,
        CategoriaEnvioTicket expectedCategory,
        string visibleCodePrefix,
        CancellationToken cancellationToken)
    {
        Negocio? negocio = await _negocioRepository.GetByIdAsync(negocioId, cancellationToken);
        if (negocio is null || negocio.IsDeleted)
        {
            return false;
        }

        Guid? templateId = templateSelector(negocio);
        if (!templateId.HasValue)
        {
            return false;
        }

        Ticket? template = await _ticketRepository.GetByIdAsync(templateId.Value, cancellationToken);
        if (template is null ||
            template.NegocioId != negocioId ||
            !template.EsPlantilla ||
            template.UserId.HasValue ||
            !template.Activo ||
            template.CategoriaEnvioEspecial != expectedCategory)
        {
            return false;
        }

        DateTime now = DateTime.UtcNow;
        if ((template.AvailableFromUtc.HasValue && template.AvailableFromUtc.Value > now) ||
            template.ExpiresAtUtc <= now)
        {
            return false;
        }

        if (expectedCategory == CategoriaEnvioTicket.PrimerRegistro)
        {
            WelcomeTicketClaimResult claim = await ClaimWelcomeTicketAsync(
                template, userId, null, visibleCodePrefix, now, cancellationToken);
            if (claim.AlreadyClaimed || claim.Ticket is null)
            {
                return false;
            }
            await EnsureAudienceAsync(negocioId, userId, "welcome_ticket", now, cancellationToken);
            await _negociosDbContext.SaveChangesAsync(cancellationToken);
            await _ticketEventPublisher.PublishReceivedAsync(claim.Ticket, "welcome", cancellationToken);
            return true;
        }

        Ticket assignedTicket = BuildAssignedTicket(template, userId, null, visibleCodePrefix, now);
        await _ticketRepository.AddAsync(assignedTicket, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _ticketEventPublisher.PublishReceivedAsync(
            assignedTicket,
            "referral",
            cancellationToken);
        return true;
    }

    private static bool IsWelcomeTemplateAvailable(Ticket? template, Guid negocioId)
    {
        DateTime now = DateTime.UtcNow;
        return template is not null && template.NegocioId == negocioId && template.EsPlantilla &&
            !template.UserId.HasValue && template.Activo && template.Publicado &&
            template.CategoriaEnvioEspecial == CategoriaEnvioTicket.PrimerRegistro &&
            template.PuntosCoste.GetValueOrDefault() <= 0 &&
            (!template.AvailableFromUtc.HasValue || template.AvailableFromUtc.Value <= now) &&
            template.ExpiresAtUtc > now;
    }

    private async Task<WelcomeTicketClaimResult> ClaimWelcomeTicketAsync(
        Ticket template, Guid userId, QrCampaign? campaign, string codePrefix, DateTime now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (campaign is not null)
        {
            List<QrCampaign> lockedCampaigns = await _dbContext.QrCampaigns
                .FromSqlInterpolated($"SELECT * FROM fidelity_qr_campaigns WHERE Id = {campaign.Id} FOR UPDATE")
                .AsNoTracking().ToListAsync(cancellationToken);
            if (lockedCampaigns.Count != 1 || !IsCampaignValid(lockedCampaigns[0]) ||
                lockedCampaigns[0].WelcomeTicketTemplateId != template.Id)
            {
                return new WelcomeTicketClaimResult(null, false);
            }
        }
        List<Ticket> lockedTemplates = await _dbContext.Tickets
            .FromSqlInterpolated($"SELECT * FROM fidelity_tickets WHERE Id = {template.Id} FOR UPDATE")
            .AsNoTracking().ToListAsync(cancellationToken);
        if (lockedTemplates.Count != 1 || !IsWelcomeTemplateAvailable(lockedTemplates[0], template.NegocioId))
        {
            return new WelcomeTicketClaimResult(null, false);
        }
        template = lockedTemplates[0];
        Guid assignedTicketId = Guid.NewGuid();
        // The composite primary key is the cross-route concurrency gate. A losing request
        // waits for the winner to commit, then reads its committed ticket.
        int inserted = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT IGNORE INTO fidelity_welcome_ticket_claims (UserId, NegocioId, TicketId, CreatedAtUtc) VALUES ({userId}, {template.NegocioId}, {assignedTicketId}, {now})",
            cancellationToken);
        if (inserted == 0)
        {
            WelcomeTicketClaim existing = await _dbContext.WelcomeTicketClaims.AsNoTracking()
                .SingleAsync(item => item.UserId == userId && item.NegocioId == template.NegocioId, cancellationToken);
            if (campaign is not null)
            {
                await MarkPendingAssignmentClaimedAsync(userId, campaign.Id, existing.TicketId, cancellationToken);
            }
            Ticket? previousTicket = await _ticketRepository.GetByIdAsync(existing.TicketId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new WelcomeTicketClaimResult(previousTicket, true);
        }

        Ticket assignedTicket = BuildAssignedTicket(template, userId, campaign?.Id, codePrefix, now);
        assignedTicket.Id = assignedTicketId;
        await _ticketRepository.AddAsync(assignedTicket, cancellationToken);
        if (campaign is not null)
        {
            PendingTicketAssignment? assignment = await _pendingTicketAssignmentRepository
                .GetByUserAndCampaignAsync(userId, campaign.Id, cancellationToken);
            if (assignment is null)
            {
                assignment = new PendingTicketAssignment
                {
                    Id = Guid.NewGuid(), UserId = userId, NegocioId = campaign.NegocioId,
                    QrCampaignId = campaign.Id, TicketTemplateId = template.Id,
                    QrToken = campaign.Token, CreatedAtUtc = now
                };
                await _pendingTicketAssignmentRepository.AddAsync(assignment, cancellationToken);
            }
            assignment.AssignedTicketId = assignedTicketId;
            assignment.Activated = true;
            assignment.ActivatedAtUtc = now;
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new WelcomeTicketClaimResult(assignedTicket, false);
    }

    private async Task MarkPendingAssignmentClaimedAsync(
        Guid userId, Guid campaignId, Guid ticketId, CancellationToken cancellationToken)
    {
        PendingTicketAssignment? assignment = await _pendingTicketAssignmentRepository
            .GetByUserAndCampaignAsync(userId, campaignId, cancellationToken);
        if (assignment is null || assignment.Activated)
        {
            return;
        }
        assignment.AssignedTicketId = ticketId;
        assignment.Activated = true;
        assignment.ActivatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureAudienceAsync(
        Guid negocioId,
        Guid userId,
        string origin,
        DateTime now,
        CancellationToken cancellationToken)
    {
        NegocioAudiencia? audience = await _negociosDbContext.NegociosAudiencias
            .FirstOrDefaultAsync(item => item.NegocioId == negocioId && item.UserId == userId, cancellationToken);

        if (audience is null)
        {
            await _negociosDbContext.NegociosAudiencias.AddAsync(new NegocioAudiencia
            {
                Id = Guid.NewGuid(),
                NegocioId = negocioId,
                UserId = userId,
                Activa = true,
                PermiteCorreosPromocionales = true,
                CorreosPromocionalesAceptadosAtUtc = now,
                OrigenAlta = origin,
                UltimaActividadOrigen = origin,
                FechaAltaUtc = now,
                UltimaActividadUtc = now,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            }, cancellationToken);
            return;
        }

        audience.Activa = true;
        audience.FechaBajaUtc = null;
        audience.OrigenAlta ??= origin;
        audience.UltimaActividadOrigen = origin;
        audience.UltimaActividadUtc = now;
        audience.UpdatedAtUtc = now;
    }

    private static Ticket BuildAssignedTicket(
        Ticket template,
        Guid userId,
        Guid? sourceQrCampaignId,
        string visibleCodePrefix,
        DateTime now)
        => new()
        {
            Id = Guid.NewGuid(),
            NegocioId = template.NegocioId,
            UserId = userId,
            ParentTicketId = template.Id,
            SourceQrCampaignId = sourceQrCampaignId,
            Nombre = template.Nombre,
            Descripcion = template.Descripcion,
            Tipo = template.Tipo,
            CategoriaEnvioEspecial = template.CategoriaEnvioEspecial,
            Valor = template.Valor,
            CodigoInterno = template.CodigoInterno,
            CodigoVisible = $"{template.CodigoVisible ?? visibleCodePrefix}-{Guid.NewGuid():N}"[..20],
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

    private static DateTime ResolveAssignedExpiration(Ticket template, DateTime assignedAtUtc)
        => template.ValidezDiasDesdeAsignacion.HasValue
            ? assignedAtUtc.AddDays(template.ValidezDiasDesdeAsignacion.Value)
            : template.ExpiresAtUtc;
}
