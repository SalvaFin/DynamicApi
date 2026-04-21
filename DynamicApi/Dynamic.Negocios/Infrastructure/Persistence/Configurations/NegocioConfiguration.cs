using Dynamic.Negocios.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dynamic.Negocios.Infrastructure.Persistence.Configurations;

public class NegocioConfiguration : IEntityTypeConfiguration<Negocio>
{
    public void Configure(EntityTypeBuilder<Negocio> builder)
    {
        builder.ToTable("negocios");
        builder.HasKey(negocio => negocio.Id);

        builder.Property(negocio => negocio.NombreComercial).HasMaxLength(160).IsRequired();
        builder.Property(negocio => negocio.SlugPortal).HasMaxLength(160).IsRequired();
        builder.Property(negocio => negocio.CodigoInterno).HasMaxLength(64);
        builder.Property(negocio => negocio.ReferenciaExterna).HasMaxLength(128);
        builder.Property(negocio => negocio.RazonSocial).HasMaxLength(180);
        builder.Property(negocio => negocio.DocumentoFiscal).HasMaxLength(64);
        builder.Property(negocio => negocio.RegistroMercantil).HasMaxLength(128);
        builder.Property(negocio => negocio.TipoNegocio).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(negocio => negocio.Estado).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(negocio => negocio.PlanSuscripcion).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(negocio => negocio.CategoriaPrincipal).HasMaxLength(120);
        builder.Property(negocio => negocio.Subcategoria).HasMaxLength(120);
        builder.Property(negocio => negocio.Etiquetas).HasColumnType("text").HasMaxLength(512);
        builder.Property(negocio => negocio.DescripcionCorta).HasColumnType("text").HasMaxLength(500);
        builder.Property(negocio => negocio.DescripcionLarga).HasColumnType("text").HasMaxLength(4000);
        builder.Property(negocio => negocio.Eslogan).HasMaxLength(240);
        builder.Property(negocio => negocio.HistoriaMarca).HasColumnType("text").HasMaxLength(4000);
        builder.Property(negocio => negocio.Mision).HasColumnType("text").HasMaxLength(2000);
        builder.Property(negocio => negocio.Vision).HasColumnType("text").HasMaxLength(2000);
        builder.Property(negocio => negocio.Valores).HasColumnType("text").HasMaxLength(2000);
        builder.Property(negocio => negocio.PersonaObjetivo).HasColumnType("text").HasMaxLength(2000);
        builder.Property(negocio => negocio.EmailContacto).HasMaxLength(256);
        builder.Property(negocio => negocio.EmailSoporte).HasMaxLength(256);
        builder.Property(negocio => negocio.TelefonoPrincipal).HasMaxLength(32);
        builder.Property(negocio => negocio.TelefonoSecundario).HasMaxLength(32);
        builder.Property(negocio => negocio.WhatsApp).HasMaxLength(32);
        builder.Property(negocio => negocio.SitioWebUrl).HasColumnType("text").HasMaxLength(512);
        builder.Property(negocio => negocio.DominioPersonalizado).HasMaxLength(255);
        builder.Property(negocio => negocio.RutaPortal).HasMaxLength(255);
        builder.Property(negocio => negocio.DireccionLinea1).HasMaxLength(255);
        builder.Property(negocio => negocio.DireccionLinea2).HasMaxLength(255);
        builder.Property(negocio => negocio.CodigoPostal).HasMaxLength(24);
        builder.Property(negocio => negocio.Ciudad).HasMaxLength(128);
        builder.Property(negocio => negocio.Provincia).HasMaxLength(128);
        builder.Property(negocio => negocio.Region).HasMaxLength(128);
        builder.Property(negocio => negocio.PaisCodigoIso2).HasMaxLength(8);
        builder.Property(negocio => negocio.Latitud).HasPrecision(10, 7);
        builder.Property(negocio => negocio.Longitud).HasPrecision(10, 7);
        builder.Property(negocio => negocio.ZonaHoraria).HasMaxLength(64);
        builder.Property(negocio => negocio.IdiomaPorDefecto).HasMaxLength(16);
        builder.Property(negocio => negocio.IdiomasSoportados).HasColumnType("text").HasMaxLength(256);
        builder.Property(negocio => negocio.MonedaCodigo).HasMaxLength(8);
        builder.Property(negocio => negocio.HorarioAperturaJson).HasColumnType("text").HasMaxLength(4000);
        builder.Property(negocio => negocio.DiasFestivosJson).HasColumnType("text").HasMaxLength(4000);
        builder.Property(negocio => negocio.LogoPrincipalUrl).HasColumnType("text").HasMaxLength(512);
        builder.Property(negocio => negocio.LogoSecundarioUrl).HasColumnType("text").HasMaxLength(512);
        builder.Property(negocio => negocio.IconoUrl).HasColumnType("text").HasMaxLength(512);
        builder.Property(negocio => negocio.ImagenHeroUrl).HasColumnType("text").HasMaxLength(512);
        builder.Property(negocio => negocio.ImagenCoverUrl).HasColumnType("text").HasMaxLength(512);
        builder.Property(negocio => negocio.ImagenMobileUrl).HasColumnType("text").HasMaxLength(512);
        builder.Property(negocio => negocio.GaleriaImagenesJson).HasColumnType("text").HasMaxLength(4000);
        builder.Property(negocio => negocio.VideoPromocionalUrl).HasColumnType("text").HasMaxLength(512);
        builder.Property(negocio => negocio.ColorPrimario).HasMaxLength(32);
        builder.Property(negocio => negocio.ColorSecundario).HasMaxLength(32);
        builder.Property(negocio => negocio.ColorAcento).HasMaxLength(32);
        builder.Property(negocio => negocio.ColorFondo).HasMaxLength(32);
        builder.Property(negocio => negocio.ColorTexto).HasMaxLength(32);
        builder.Property(negocio => negocio.FuenteTitulo).HasMaxLength(128);
        builder.Property(negocio => negocio.FuenteCuerpo).HasMaxLength(128);
        builder.Property(negocio => negocio.HeadlinePortal).HasMaxLength(255);
        builder.Property(negocio => negocio.SubheadlinePortal).HasColumnType("text").HasMaxLength(500);
        builder.Property(negocio => negocio.MensajeBienvenida).HasColumnType("text").HasMaxLength(2000);
        builder.Property(negocio => negocio.MensajeLegal).HasColumnType("text").HasMaxLength(2000);
        builder.Property(negocio => negocio.SeoTitle).HasMaxLength(255);
        builder.Property(negocio => negocio.SeoDescription).HasColumnType("text").HasMaxLength(500);
        builder.Property(negocio => negocio.SeoKeywords).HasColumnType("text").HasMaxLength(1000);
        builder.Property(negocio => negocio.OpenGraphImageUrl).HasColumnType("text").HasMaxLength(512);
        builder.Property(negocio => negocio.FacebookUrl).HasColumnType("text").HasMaxLength(512);
        builder.Property(negocio => negocio.InstagramUrl).HasColumnType("text").HasMaxLength(512);
        builder.Property(negocio => negocio.TikTokUrl).HasColumnType("text").HasMaxLength(512);
        builder.Property(negocio => negocio.XUrl).HasColumnType("text").HasMaxLength(512);
        builder.Property(negocio => negocio.LinkedInUrl).HasColumnType("text").HasMaxLength(512);
        builder.Property(negocio => negocio.YoutubeUrl).HasColumnType("text").HasMaxLength(512);
        builder.Property(negocio => negocio.CondicionesUsoUrl).HasColumnType("text").HasMaxLength(512);
        builder.Property(negocio => negocio.PoliticaPrivacidadUrl).HasColumnType("text").HasMaxLength(512);
        builder.Property(negocio => negocio.PoliticaCookiesUrl).HasColumnType("text").HasMaxLength(512);
        builder.Property(negocio => negocio.TextoCondicionesPrograma).HasColumnType("text").HasMaxLength(4000);
        builder.Property(negocio => negocio.TextoPoliticaPuntos).HasColumnType("text").HasMaxLength(4000);
        builder.Property(negocio => negocio.NombreProgramaFidelizacion).HasMaxLength(180);
        builder.Property(negocio => negocio.DescripcionProgramaFidelizacion).HasColumnType("text").HasMaxLength(2000);
        builder.Property(negocio => negocio.RatioConversionEurosAPuntos).HasPrecision(10, 4);
        builder.Property(negocio => negocio.ClaveMaestraLocalHash).HasMaxLength(128);
        builder.Property(negocio => negocio.ValorMonetarioPunto).HasPrecision(10, 2);

        builder.HasIndex(negocio => negocio.SlugPortal).IsUnique();
        builder.HasIndex(negocio => negocio.OwnerUserId);
        builder.HasIndex(negocio => negocio.BonoBienvenidaTicketId);
        builder.HasIndex(negocio => negocio.Estado);
        builder.HasIndex(negocio => negocio.IsDeleted);

        builder.HasMany(negocio => negocio.VinculacionesUsuarios)
            .WithOne(vinculacion => vinculacion.Negocio)
            .HasForeignKey(vinculacion => vinculacion.NegocioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
