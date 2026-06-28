using Dynamic.Fidelity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dynamic.Fidelity.Infrastructure.Persistence.Configurations;

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("fidelity_tickets");
        builder.HasKey(ticket => ticket.Id);

        builder.Property(ticket => ticket.ParentTicketId);
        builder.Property(ticket => ticket.SourceQrCampaignId);
        builder.Property(ticket => ticket.SourcePromotionCampaignId);
        builder.Property(ticket => ticket.SourcePromotionRecipientId);
        builder.Property(ticket => ticket.Nombre).HasMaxLength(180).IsRequired();
        builder.Property(ticket => ticket.Descripcion).HasMaxLength(2000);
        builder.Property(ticket => ticket.Tipo).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(ticket => ticket.CategoriaEnvioEspecial).HasConversion<string>().HasMaxLength(48).IsRequired();
        builder.Property(ticket => ticket.Valor).HasPrecision(10, 2).IsRequired();
        builder.Property(ticket => ticket.CodigoInterno).HasMaxLength(64);
        builder.Property(ticket => ticket.CodigoVisible).HasMaxLength(64);
        builder.Property(ticket => ticket.TituloCanje).HasMaxLength(255);
        builder.Property(ticket => ticket.InstruccionesCanje).HasMaxLength(2000);
        builder.Property(ticket => ticket.CondicionesUso).HasMaxLength(4000);
        builder.Property(ticket => ticket.MensajeMarketing).HasMaxLength(1000);
        builder.Property(ticket => ticket.DescuentoPorcentaje).HasPrecision(7, 2);
        builder.Property(ticket => ticket.DescuentoImporteFijo).HasPrecision(10, 2);
        builder.Property(ticket => ticket.BeneficioEspecialResumen).HasMaxLength(255);
        builder.Property(ticket => ticket.BeneficioEspecialDetalle).HasMaxLength(2000);
        builder.Property(ticket => ticket.GastoMinimoRequerido).HasPrecision(10, 2);
        builder.Property(ticket => ticket.MaxUsosPorCliente);
        builder.Property(ticket => ticket.UsosConsumidos).IsRequired();
        builder.Property(ticket => ticket.ValidezDiasDesdeAsignacion);
        builder.Property(ticket => ticket.UsedInStoreReference).HasMaxLength(128);
        builder.Property(ticket => ticket.UsedByEmployeeReference).HasMaxLength(128);
        builder.Property(ticket => ticket.NotasInternas).HasMaxLength(2000);

        builder.HasIndex(ticket => ticket.NegocioId);
        builder.HasIndex(ticket => ticket.UserId);
        builder.HasIndex(ticket => ticket.Tipo);
        builder.HasIndex(ticket => ticket.CategoriaEnvioEspecial);
        builder.HasIndex(ticket => ticket.ExpiresAtUtc);
        builder.HasIndex(ticket => ticket.Usado);
        builder.HasIndex(ticket => ticket.EsPlantilla);
        builder.HasIndex(ticket => ticket.ParentTicketId);
        builder.HasIndex(ticket => ticket.SourcePromotionCampaignId);
        builder.HasIndex(ticket => ticket.SourcePromotionRecipientId).IsUnique();
        builder.HasIndex(ticket => new { ticket.NegocioId, ticket.CodigoVisible });
        builder.HasIndex(ticket => new { ticket.NegocioId, ticket.UserId });
    }
}
