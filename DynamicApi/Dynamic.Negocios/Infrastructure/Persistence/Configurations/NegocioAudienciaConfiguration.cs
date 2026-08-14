using Dynamic.Negocios.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dynamic.Negocios.Infrastructure.Persistence.Configurations;

public class NegocioAudienciaConfiguration : IEntityTypeConfiguration<NegocioAudiencia>
{
    public void Configure(EntityTypeBuilder<NegocioAudiencia> builder)
    {
        builder.ToTable("negocio_audience_memberships");
        builder.HasKey(audiencia => audiencia.Id);

        builder.Property(audiencia => audiencia.OrigenAlta).HasMaxLength(128);
        builder.Property(audiencia => audiencia.UltimaActividadOrigen).HasMaxLength(128);
        builder.Property(audiencia => audiencia.PermiteCorreosPromocionales).HasDefaultValue(true);

        builder.HasIndex(audiencia => new { audiencia.NegocioId, audiencia.UserId }).IsUnique();
        builder.HasIndex(audiencia => audiencia.UserId);
        builder.HasIndex(audiencia => audiencia.Activa);
        builder.HasIndex(audiencia => audiencia.EsFavorito);
        builder.HasIndex(audiencia => audiencia.UltimaActividadUtc);
        builder.HasIndex(audiencia => new { audiencia.UserId, audiencia.Activa, audiencia.UltimaActividadUtc });

        builder.HasOne(audiencia => audiencia.Negocio)
            .WithMany()
            .HasForeignKey(audiencia => audiencia.NegocioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
