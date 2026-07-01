using Dynamic.Fidelity.Application.Contracts.Repositories;
using Dynamic.Fidelity.Application.Contracts.Services;
using Dynamic.Fidelity.Domain.Entities;
using Dynamic.Fidelity.Domain.Enums;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Dynamic.Negocios.Application.Contracts.Repositories;
using Dynamic.Negocios.Domain.Entities;

namespace Dynamic.Fidelity.Application.Services;

public class RegistrationRewardService : IRegistrationRewardService
{
    private readonly DynamicFidelityDbContext _dbContext;
    private readonly IQrCampaignRepository _qrCampaignRepository;
    private readonly IPendingTicketAssignmentRepository _pendingTicketAssignmentRepository;
    private readonly ITicketRepository _ticketRepository;
    private readonly INegocioRepository _negocioRepository;

    public RegistrationRewardService(
        DynamicFidelityDbContext dbContext,
        IQrCampaignRepository qrCampaignRepository,
        IPendingTicketAssignmentRepository pendingTicketAssignmentRepository,
        ITicketRepository ticketRepository,
        INegocioRepository negocioRepository)
    {
        _dbContext = dbContext;
        _qrCampaignRepository = qrCampaignRepository;
        _pendingTicketAssignmentRepository = pendingTicketAssignmentRepository;
        _ticketRepository = ticketRepository;
        _negocioRepository = negocioRepository;
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

            Ticket assignedTicket = BuildAssignedTicket(template, userId, assignment.QrCampaignId, "WELCOME", now);

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
        Ticket assignedTicket = BuildAssignedTicket(template, userId, campaign.Id, "TICKET", now);

        await _ticketRepository.AddAsync(assignedTicket, cancellationToken);

        existingAssignment.AssignedTicketId = assignedTicket.Id;
        existingAssignment.Activated = true;
        existingAssignment.ActivatedAtUtc = now;
        _pendingTicketAssignmentRepository.Update(existingAssignment);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return assignedTicket;
    }

    public async Task<bool> AssignBusinessWelcomeTicketAsync(Guid negocioId, Guid userId, CancellationToken cancellationToken = default)
        => await AssignBusinessConfiguredTicketAsync(
            negocioId,
            userId,
            negocio => negocio.BonoBienvenidaTicketId,
            CategoriaEnvioTicket.PrimerRegistro,
            "WELCOME",
            preventDuplicateByTemplate: true,
            cancellationToken);

    public async Task<bool> AssignBusinessReferralTicketAsync(Guid negocioId, Guid userId, CancellationToken cancellationToken = default)
        => await AssignBusinessConfiguredTicketAsync(
            negocioId,
            userId,
            negocio => negocio.BonoInvitacionNuevoClienteTicketId,
            CategoriaEnvioTicket.InvitacionClienteNuevo,
            "REFERRAL",
            preventDuplicateByTemplate: false,
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
        bool preventDuplicateByTemplate,
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

        if (preventDuplicateByTemplate)
        {
            int currentAssignments = await _ticketRepository.CountAssignedToUserByTemplateAsync(userId, template.Id, cancellationToken);
            if (currentAssignments > 0)
            {
                return false;
            }
        }

        Ticket assignedTicket = BuildAssignedTicket(template, userId, null, visibleCodePrefix, now);
        await _ticketRepository.AddAsync(assignedTicket, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
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
