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
            Activo = ticket.Activo,
            Publicado = ticket.Publicado,
            EsDeUnSoloUso = ticket.EsDeUnSoloUso,
            RequiereValidacionManual = ticket.RequiereValidacionManual,
            EsPlantilla = ticket.EsPlantilla,
            MaxUsosPorCliente = ticket.MaxUsosPorCliente,
            UsosConsumidos = ticket.UsosConsumidos,
            ValidezDiasDesdeAsignacion = ticket.ValidezDiasDesdeAsignacion,
            AvailableFromUtc = ticket.AvailableFromUtc,
            ExpiresAtUtc = ticket.ExpiresAtUtc,
            CreatedAtUtc = ticket.CreatedAtUtc,
            UpdatedAtUtc = ticket.UpdatedAtUtc
        };
}
