using System.ComponentModel.DataAnnotations;
using Dynamic.Negocios.Domain.Enums;

namespace Dynamic.Negocios.Application.DTOs.Requests;

public class CrearNegocioRequest
{
    public Guid? OwnerUserId { get; set; }

    [Required]
    [MaxLength(160)]
    public string NombreComercial { get; set; } = string.Empty;

    [Required]
    [MaxLength(160)]
    public string SlugPortal { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? CodigoInterno { get; set; }

    [MaxLength(128)]
    public string? ReferenciaExterna { get; set; }

    [MaxLength(180)]
    public string? RazonSocial { get; set; }

    [MaxLength(64)]
    public string? DocumentoFiscal { get; set; }

    [MaxLength(128)]
    public string? RegistroMercantil { get; set; }

    public TipoNegocio TipoNegocio { get; set; } = TipoNegocio.Otro;
    public EstadoNegocio Estado { get; set; } = EstadoNegocio.Borrador;
    public PlanSuscripcionNegocio PlanSuscripcion { get; set; } = PlanSuscripcionNegocio.Starter;

    [MaxLength(120)]
    public string? CategoriaPrincipal { get; set; }

    [MaxLength(120)]
    public string? Subcategoria { get; set; }

    [MaxLength(512)]
    public string? Etiquetas { get; set; }

    [MaxLength(500)]
    public string? DescripcionCorta { get; set; }

    [MaxLength(4000)]
    public string? DescripcionLarga { get; set; }

    [MaxLength(240)]
    public string? Eslogan { get; set; }

    [MaxLength(4000)]
    public string? HistoriaMarca { get; set; }

    [MaxLength(2000)]
    public string? Mision { get; set; }

    [MaxLength(2000)]
    public string? Vision { get; set; }

    [MaxLength(2000)]
    public string? Valores { get; set; }

    [MaxLength(2000)]
    public string? PersonaObjetivo { get; set; }

    [EmailAddress]
    [MaxLength(256)]
    public string? EmailContacto { get; set; }

    [EmailAddress]
    [MaxLength(256)]
    public string? EmailSoporte { get; set; }

    [MaxLength(32)]
    public string? TelefonoPrincipal { get; set; }

    [MaxLength(32)]
    public string? TelefonoSecundario { get; set; }

    [MaxLength(32)]
    public string? WhatsApp { get; set; }

    [MaxLength(512)]
    public string? SitioWebUrl { get; set; }

    [MaxLength(255)]
    public string? DominioPersonalizado { get; set; }

    [MaxLength(255)]
    public string? RutaPortal { get; set; }

    [MaxLength(255)]
    public string? DireccionLinea1 { get; set; }

    [MaxLength(255)]
    public string? DireccionLinea2 { get; set; }

    [MaxLength(24)]
    public string? CodigoPostal { get; set; }

    [MaxLength(128)]
    public string? Ciudad { get; set; }

    [MaxLength(128)]
    public string? Provincia { get; set; }

    [MaxLength(128)]
    public string? Region { get; set; }

    [MaxLength(8)]
    public string? PaisCodigoIso2 { get; set; }

    public decimal? Latitud { get; set; }
    public decimal? Longitud { get; set; }

    [MaxLength(64)]
    public string? ZonaHoraria { get; set; }

    [MaxLength(16)]
    public string? IdiomaPorDefecto { get; set; }

    [MaxLength(256)]
    public string? IdiomasSoportados { get; set; }

    [MaxLength(8)]
    public string? MonedaCodigo { get; set; }

    [MaxLength(4000)]
    public string? HorarioAperturaJson { get; set; }

    [MaxLength(4000)]
    public string? DiasFestivosJson { get; set; }

    [MaxLength(512)]
    public string? LogoPrincipalUrl { get; set; }

    [MaxLength(512)]
    public string? LogoSecundarioUrl { get; set; }

    [MaxLength(512)]
    public string? IconoUrl { get; set; }

    [MaxLength(512)]
    public string? ImagenHeroUrl { get; set; }

    [MaxLength(512)]
    public string? ImagenCoverUrl { get; set; }

    [MaxLength(512)]
    public string? ImagenMobileUrl { get; set; }

    [MaxLength(4000)]
    public string? GaleriaImagenesJson { get; set; }

    [MaxLength(512)]
    public string? VideoPromocionalUrl { get; set; }

    [MaxLength(32)]
    public string? ColorPrimario { get; set; }

    [MaxLength(32)]
    public string? ColorSecundario { get; set; }

    [MaxLength(32)]
    public string? ColorAcento { get; set; }

    [MaxLength(32)]
    public string? ColorFondo { get; set; }

    [MaxLength(32)]
    public string? ColorTexto { get; set; }

    [MaxLength(128)]
    public string? FuenteTitulo { get; set; }

    [MaxLength(128)]
    public string? FuenteCuerpo { get; set; }

    [MaxLength(255)]
    public string? HeadlinePortal { get; set; }

    [MaxLength(500)]
    public string? SubheadlinePortal { get; set; }

    [MaxLength(2000)]
    public string? MensajeBienvenida { get; set; }

    [MaxLength(2000)]
    public string? MensajeLegal { get; set; }

    [MaxLength(255)]
    public string? SeoTitle { get; set; }

    [MaxLength(500)]
    public string? SeoDescription { get; set; }

    [MaxLength(1000)]
    public string? SeoKeywords { get; set; }

    [MaxLength(512)]
    public string? OpenGraphImageUrl { get; set; }

    [MaxLength(512)]
    public string? FacebookUrl { get; set; }

    [MaxLength(512)]
    public string? InstagramUrl { get; set; }

    [MaxLength(512)]
    public string? TikTokUrl { get; set; }

    [MaxLength(512)]
    public string? XUrl { get; set; }

    [MaxLength(512)]
    public string? LinkedInUrl { get; set; }

    [MaxLength(512)]
    public string? YoutubeUrl { get; set; }

    [MaxLength(512)]
    public string? CondicionesUsoUrl { get; set; }

    [MaxLength(512)]
    public string? PoliticaPrivacidadUrl { get; set; }

    [MaxLength(512)]
    public string? PoliticaCookiesUrl { get; set; }

    [MaxLength(4000)]
    public string? TextoCondicionesPrograma { get; set; }

    [MaxLength(4000)]
    public string? TextoPoliticaPuntos { get; set; }

    [MaxLength(180)]
    public string? NombreProgramaFidelizacion { get; set; }

    [MaxLength(2000)]
    public string? DescripcionProgramaFidelizacion { get; set; }

    public int? PuntosBienvenida { get; set; }
    public int? PuntosCumpleanos { get; set; }
    public decimal? ValorMonetarioPunto { get; set; }
    public int? CaducidadPuntosDias { get; set; }
    public bool PermiteRegistroPublico { get; set; }
    public bool PublicadoPortal { get; set; }
    public bool Activo { get; set; } = true;
    public bool PermiteNotificacionesPush { get; set; }
    public bool PermiteNotificacionesEmail { get; set; } = true;
    public bool PermiteNotificacionesSms { get; set; }
    public bool PermiteWalletPass { get; set; }
    public bool PermiteQrCheckIn { get; set; }
    public bool PermiteProgramaReferidos { get; set; }
    public bool PermiteCampanasAutomatizadas { get; set; }
    public bool RequiereAprobacionManualClientes { get; set; }
    public bool OcultarMarcaGeneral { get; set; }
    public int? MaximoUbicaciones { get; set; }
    public int? MaximoUsuariosBackoffice { get; set; }
    public int? MaximoClientesRegistrados { get; set; }
    public DateTime? FechaPublicacionUtc { get; set; }
    public DateTime? FechaInicioSuscripcionUtc { get; set; }
    public DateTime? FechaFinSuscripcionUtc { get; set; }
}
