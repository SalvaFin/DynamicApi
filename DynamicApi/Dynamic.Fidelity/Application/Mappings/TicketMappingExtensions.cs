using Dynamic.Fidelity.Application.DTOs.Responses;
using Dynamic.Fidelity.Domain.Entities;

namespace Dynamic.Fidelity.Application.Mappings;

public static class TicketMappingExtensions
{
    public static TicketResponse ToResponse(this Ticket ticket)
        => new()
        {
            Id = ticket.Id,
            NegocioId = ticket.NegocioId,
            Nombre = ticket.Nombre,
            Descripcion = ticket.Descripcion,
            Tipo = ticket.Tipo,
            CategoriaEnvioEspecial = ticket.CategoriaEnvioEspecial,
            Valor = ticket.Valor,
            PuntosCoste = ticket.PuntosCoste,
            UserId = ticket.UserId,
            ParentTicketId = ticket.ParentTicketId,
            SourcePromotionCampaignId = ticket.SourcePromotionCampaignId,
            SourcePromotionRecipientId = ticket.SourcePromotionRecipientId,
            Activo = ticket.Activo,
            Publicado = ticket.Publicado,
            EsDeUnSoloUso = ticket.EsDeUnSoloUso,
            RequiereValidacionManual = ticket.RequiereValidacionManual,
            EsPlantilla = ticket.EsPlantilla,
            Usado = ticket.Usado,
            MaxUsosPorCliente = ticket.MaxUsosPorCliente,
            UsosConsumidos = ticket.UsosConsumidos,
            ValidezDiasDesdeAsignacion = ticket.ValidezDiasDesdeAsignacion,
            AvailableFromUtc = ticket.AvailableFromUtc,
            ExpiresAtUtc = ticket.ExpiresAtUtc,
            UsedAtUtc = ticket.UsedAtUtc,
            CreatedAtUtc = ticket.CreatedAtUtc,
            UpdatedAtUtc = ticket.UpdatedAtUtc
        };

    public static ValidatedTicketResponse ToValidatedResponse(this Ticket ticket)
        => new()
        {
            Id = ticket.Id,
            NegocioId = ticket.NegocioId,
            Nombre = ticket.Nombre,
            Descripcion = ticket.Descripcion,
            Tipo = ticket.Tipo,
            CategoriaEnvioEspecial = ticket.CategoriaEnvioEspecial,
            Valor = ticket.Valor,
            PuntosCoste = ticket.PuntosCoste,
            UserId = ticket.UserId,
            ParentTicketId = ticket.ParentTicketId,
            SourceQrCampaignId = ticket.SourceQrCampaignId,
            SourcePromotionCampaignId = ticket.SourcePromotionCampaignId,
            SourcePromotionRecipientId = ticket.SourcePromotionRecipientId,
            CodigoInterno = ticket.CodigoInterno,
            CodigoVisible = ticket.CodigoVisible,
            TituloCanje = ticket.TituloCanje,
            InstruccionesCanje = ticket.InstruccionesCanje,
            CondicionesUso = ticket.CondicionesUso,
            MensajeMarketing = ticket.MensajeMarketing,
            DescuentoPorcentaje = ticket.DescuentoPorcentaje,
            DescuentoImporteFijo = ticket.DescuentoImporteFijo,
            BeneficioEspecialResumen = ticket.BeneficioEspecialResumen,
            BeneficioEspecialDetalle = ticket.BeneficioEspecialDetalle,
            GastoMinimoRequerido = ticket.GastoMinimoRequerido,
            Activo = ticket.Activo,
            Publicado = ticket.Publicado,
            EsDeUnSoloUso = ticket.EsDeUnSoloUso,
            RequiereValidacionManual = ticket.RequiereValidacionManual,
            EsPlantilla = ticket.EsPlantilla,
            Usado = ticket.Usado,
            MaxUsosPorCliente = ticket.MaxUsosPorCliente,
            UsosConsumidos = ticket.UsosConsumidos,
            ValidezDiasDesdeAsignacion = ticket.ValidezDiasDesdeAsignacion,
            AvailableFromUtc = ticket.AvailableFromUtc,
            ExpiresAtUtc = ticket.ExpiresAtUtc,
            UsedAtUtc = ticket.UsedAtUtc,
            UsedInStoreReference = ticket.UsedInStoreReference,
            UsedByEmployeeReference = ticket.UsedByEmployeeReference,
            CreatedAtUtc = ticket.CreatedAtUtc,
            UpdatedAtUtc = ticket.UpdatedAtUtc
        };
}
