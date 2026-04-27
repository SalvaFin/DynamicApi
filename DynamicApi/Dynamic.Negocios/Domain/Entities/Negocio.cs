using Dynamic.Negocios.Domain.Enums;

namespace Dynamic.Negocios.Domain.Entities;

public class Negocio
{
    public Guid Id { get; set; }
    public Guid? OwnerUserId { get; set; }
    public Guid? BonoBienvenidaTicketId { get; set; }
    public Guid? BonoInvitacionNuevoClienteTicketId { get; set; }
    public string NombreComercial { get; set; } = string.Empty;
    public string SlugPortal { get; set; } = string.Empty;
    public string? CodigoInterno { get; set; }
    public string? ReferenciaExterna { get; set; }
    public string? RazonSocial { get; set; }
    public string? DocumentoFiscal { get; set; }
    public string? RegistroMercantil { get; set; }
    public TipoNegocio TipoNegocio { get; set; } = TipoNegocio.Otro;
    public EstadoNegocio Estado { get; set; } = EstadoNegocio.Borrador;
    public PlanSuscripcionNegocio PlanSuscripcion { get; set; } = PlanSuscripcionNegocio.Starter;
    public string? CategoriaPrincipal { get; set; }
    public string? Subcategoria { get; set; }
    public string? Etiquetas { get; set; }
    public string? DescripcionCorta { get; set; }
    public string? DescripcionLarga { get; set; }
    public string? Eslogan { get; set; }
    public string? HistoriaMarca { get; set; }
    public string? Mision { get; set; }
    public string? Vision { get; set; }
    public string? Valores { get; set; }
    public string? PersonaObjetivo { get; set; }
    public string? EmailContacto { get; set; }
    public string? EmailSoporte { get; set; }
    public string? TelefonoPrincipal { get; set; }
    public string? TelefonoSecundario { get; set; }
    public string? WhatsApp { get; set; }
    public string? SitioWebUrl { get; set; }
    public string? DominioPersonalizado { get; set; }
    public string? RutaPortal { get; set; }
    public string? DireccionLinea1 { get; set; }
    public string? DireccionLinea2 { get; set; }
    public string? CodigoPostal { get; set; }
    public string? Ciudad { get; set; }
    public string? Provincia { get; set; }
    public string? Region { get; set; }
    public string? PaisCodigoIso2 { get; set; }
    public decimal? Latitud { get; set; }
    public decimal? Longitud { get; set; }
    public string? ZonaHoraria { get; set; }
    public string? IdiomaPorDefecto { get; set; }
    public string? IdiomasSoportados { get; set; }
    public string? MonedaCodigo { get; set; }
    public string? HorarioAperturaJson { get; set; }
    public string? DiasFestivosJson { get; set; }
    public string? LogoPrincipalUrl { get; set; }
    public string? LogoSecundarioUrl { get; set; }
    public string? IconoUrl { get; set; }
    public string? ImagenHeroUrl { get; set; }
    public string? ImagenCoverUrl { get; set; }
    public string? ImagenMobileUrl { get; set; }
    public string? GaleriaImagenesJson { get; set; }
    public string? VideoPromocionalUrl { get; set; }
    public string? ColorPrimario { get; set; }
    public string? ColorSecundario { get; set; }
    public string? ColorAcento { get; set; }
    public string? ColorFondo { get; set; }
    public string? ColorTexto { get; set; }
    public string? FuenteTitulo { get; set; }
    public string? FuenteCuerpo { get; set; }
    public string? HeadlinePortal { get; set; }
    public string? SubheadlinePortal { get; set; }
    public string? MensajeBienvenida { get; set; }
    public string? MensajeLegal { get; set; }
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public string? SeoKeywords { get; set; }
    public string? OpenGraphImageUrl { get; set; }
    public string? FacebookUrl { get; set; }
    public string? InstagramUrl { get; set; }
    public string? TikTokUrl { get; set; }
    public string? XUrl { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? YoutubeUrl { get; set; }
    public string? CondicionesUsoUrl { get; set; }
    public string? PoliticaPrivacidadUrl { get; set; }
    public string? PoliticaCookiesUrl { get; set; }
    public string? TextoCondicionesPrograma { get; set; }
    public string? TextoPoliticaPuntos { get; set; }
    public string? NombreProgramaFidelizacion { get; set; }
    public string? DescripcionProgramaFidelizacion { get; set; }
    public decimal? RatioConversionEurosAPuntos { get; set; }
    public string? ClaveMaestraLocalHash { get; set; }
    public DateTime? ClaveMaestraLocalUpdatedAtUtc { get; set; }
    public int? PuntosBienvenida { get; set; }
    public int? PuntosCumpleanos { get; set; }
    public decimal? ValorMonetarioPunto { get; set; }
    public int? CaducidadPuntosDias { get; set; }
    public bool PermiteRegistroPublico { get; set; }
    public bool PublicadoPortal { get; set; }
    public bool Activo { get; set; }
    public bool PermiteNotificacionesPush { get; set; }
    public bool PermiteNotificacionesEmail { get; set; }
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
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<NegocioUsuarioVinculacion> VinculacionesUsuarios { get; set; } = [];
}
