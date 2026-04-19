using Dynamic.Negocios.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dynamic.Negocios.Infrastructure.Persistence.Configurations;

public class NegocioUsuarioVinculacionConfiguration : IEntityTypeConfiguration<NegocioUsuarioVinculacion>
{
    public void Configure(EntityTypeBuilder<NegocioUsuarioVinculacion> builder)
    {
        builder.ToTable("negocio_user_links");
        builder.HasKey(vinculacion => vinculacion.Id);

        builder.Property(vinculacion => vinculacion.TipoVinculacion)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(vinculacion => vinculacion.TituloRelacion).HasMaxLength(128);
        builder.Property(vinculacion => vinculacion.NotasInternas).HasMaxLength(2000);
        builder.Property(vinculacion => vinculacion.OrigenVinculacion).HasMaxLength(128);

        builder.HasIndex(vinculacion => new { vinculacion.NegocioId, vinculacion.UserId }).IsUnique();
        builder.HasIndex(vinculacion => vinculacion.UserId);
        builder.HasIndex(vinculacion => vinculacion.Activa);
        builder.HasIndex(vinculacion => vinculacion.TipoVinculacion);

        builder.HasOne(vinculacion => vinculacion.Negocio)
            .WithMany(negocio => negocio.VinculacionesUsuarios)
            .HasForeignKey(vinculacion => vinculacion.NegocioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
