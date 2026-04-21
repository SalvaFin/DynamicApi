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
        builder.Property(negocio => negocio.Etiquetas).HasMaxLength(512);
        builder.Property(negocio => negocio.DescripcionCorta).HasMaxLength(500);
        builder.Property(negocio => negocio.DescripcionLarga).HasMaxLength(4000);
        builder.Property(negocio => negocio.Eslogan).HasMaxLength(240);
        builder.Property(negocio => negocio.HistoriaMarca).HasMaxLength(4000);
        builder.Property(negocio => negocio.Mision).HasMaxLength(2000);
        builder.Property(negocio => negocio.Vision).HasMaxLength(2000);
        builder.Property(negocio => negocio.Valores).HasMaxLength(2000);
        builder.Property(negocio => negocio.PersonaObjetivo).HasMaxLength(2000);
        builder.Property(negocio => negocio.EmailContacto).HasMaxLength(256);
        builder.Property(negocio => negocio.EmailSoporte).HasMaxLength(256);
        builder.Property(negocio => negocio.TelefonoPrincipal).HasMaxLength(32);
        builder.Property(negocio => negocio.TelefonoSecundario).HasMaxLength(32);
        builder.Property(negocio => negocio.WhatsApp).HasMaxLength(32);
        builder.Property(negocio => negocio.SitioWebUrl).HasMaxLength(512);
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
        builder.Property(negocio => negocio.IdiomasSoportados).HasMaxLength(256);
        builder.Property(negocio => negocio.MonedaCodigo).HasMaxLength(8);
        builder.Property(negocio => negocio.HorarioAperturaJson).HasMaxLength(4000);
        builder.Property(negocio => negocio.DiasFestivosJson).HasMaxLength(4000);
        builder.Property(negocio => negocio.LogoPrincipalUrl).HasMaxLength(512);
        builder.Property(negocio => negocio.LogoSecundarioUrl).HasMaxLength(512);
        builder.Property(negocio => negocio.IconoUrl).HasMaxLength(512);
        builder.Property(negocio => negocio.ImagenHeroUrl).HasMaxLength(512);
        builder.Property(negocio => negocio.ImagenCoverUrl).HasMaxLength(512);
        builder.Property(negocio => negocio.ImagenMobileUrl).HasMaxLength(512);
        builder.Property(negocio => negocio.GaleriaImagenesJson).HasMaxLength(4000);
        builder.Property(negocio => negocio.VideoPromocionalUrl).HasMaxLength(512);
        builder.Property(negocio => negocio.ColorPrimario).HasMaxLength(32);
        builder.Property(negocio => negocio.ColorSecundario).HasMaxLength(32);
        builder.Property(negocio => negocio.ColorAcento).HasMaxLength(32);
        builder.Property(negocio => negocio.ColorFondo).HasMaxLength(32);
        builder.Property(negocio => negocio.ColorTexto).HasMaxLength(32);
        builder.Property(negocio => negocio.FuenteTitulo).HasMaxLength(128);
        builder.Property(negocio => negocio.FuenteCuerpo).HasMaxLength(128);
        builder.Property(negocio => negocio.HeadlinePortal).HasMaxLength(255);
        builder.Property(negocio => negocio.SubheadlinePortal).HasMaxLength(500);
        builder.Property(negocio => negocio.MensajeBienvenida).HasMaxLength(2000);
        builder.Property(negocio => negocio.MensajeLegal).HasMaxLength(2000);
        builder.Property(negocio => negocio.SeoTitle).HasMaxLength(255);
        builder.Property(negocio => negocio.SeoDescription).HasMaxLength(500);
        builder.Property(negocio => negocio.SeoKeywords).HasMaxLength(1000);
        builder.Property(negocio => negocio.OpenGraphImageUrl).HasMaxLength(512);
        builder.Property(negocio => negocio.FacebookUrl).HasMaxLength(512);
        builder.Property(negocio => negocio.InstagramUrl).HasMaxLength(512);
        builder.Property(negocio => negocio.TikTokUrl).HasMaxLength(512);
        builder.Property(negocio => negocio.XUrl).HasMaxLength(512);
        builder.Property(negocio => negocio.LinkedInUrl).HasMaxLength(512);
        builder.Property(negocio => negocio.YoutubeUrl).HasMaxLength(512);
        builder.Property(negocio => negocio.CondicionesUsoUrl).HasMaxLength(512);
        builder.Property(negocio => negocio.PoliticaPrivacidadUrl).HasMaxLength(512);
        builder.Property(negocio => negocio.PoliticaCookiesUrl).HasMaxLength(512);
        builder.Property(negocio => negocio.TextoCondicionesPrograma).HasMaxLength(4000);
        builder.Property(negocio => negocio.TextoPoliticaPuntos).HasMaxLength(4000);
        builder.Property(negocio => negocio.NombreProgramaFidelizacion).HasMaxLength(180);
        builder.Property(negocio => negocio.DescripcionProgramaFidelizacion).HasMaxLength(2000);
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
