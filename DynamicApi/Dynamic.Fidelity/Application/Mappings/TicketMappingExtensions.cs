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
            Valor = ticket.Valor,
            Activo = ticket.Activo,
            Publicado = ticket.Publicado,
            EsDeUnSoloUso = ticket.EsDeUnSoloUso,
            RequiereValidacionManual = ticket.RequiereValidacionManual,
            EsPlantilla = ticket.EsPlantilla,
            AvailableFromUtc = ticket.AvailableFromUtc,
            ExpiresAtUtc = ticket.ExpiresAtUtc,
            CreatedAtUtc = ticket.CreatedAtUtc,
            UpdatedAtUtc = ticket.UpdatedAtUtc
        };
}
