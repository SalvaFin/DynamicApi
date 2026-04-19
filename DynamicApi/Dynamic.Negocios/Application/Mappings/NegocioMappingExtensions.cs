using Dynamic.Negocios.Application.DTOs.Requests;
using Dynamic.Negocios.Application.DTOs.Responses;
using Dynamic.Negocios.Domain.Entities;

namespace Dynamic.Negocios.Application.Mappings;

public static class NegocioMappingExtensions
{
    public static NegocioResponse ToResponse(this Negocio negocio)
        => new()
        {
            Id = negocio.Id,
            OwnerUserId = negocio.OwnerUserId,
            NombreComercial = negocio.NombreComercial,
            SlugPortal = negocio.SlugPortal,
            CodigoInterno = negocio.CodigoInterno,
            ReferenciaExterna = negocio.ReferenciaExterna,
            RazonSocial = negocio.RazonSocial,
            DocumentoFiscal = negocio.DocumentoFiscal,
            RegistroMercantil = negocio.RegistroMercantil,
            TipoNegocio = negocio.TipoNegocio,
            Estado = negocio.Estado,
            PlanSuscripcion = negocio.PlanSuscripcion,
            CategoriaPrincipal = negocio.CategoriaPrincipal,
            Subcategoria = negocio.Subcategoria,
            Etiquetas = negocio.Etiquetas,
            DescripcionCorta = negocio.DescripcionCorta,
            DescripcionLarga = negocio.DescripcionLarga,
            Eslogan = negocio.Eslogan,
            HistoriaMarca = negocio.HistoriaMarca,
            Mision = negocio.Mision,
            Vision = negocio.Vision,
            Valores = negocio.Valores,
            PersonaObjetivo = negocio.PersonaObjetivo,
            EmailContacto = negocio.EmailContacto,
            EmailSoporte = negocio.EmailSoporte,
            TelefonoPrincipal = negocio.TelefonoPrincipal,
            TelefonoSecundario = negocio.TelefonoSecundario,
            WhatsApp = negocio.WhatsApp,
            SitioWebUrl = negocio.SitioWebUrl,
            DominioPersonalizado = negocio.DominioPersonalizado,
            RutaPortal = negocio.RutaPortal,
            DireccionLinea1 = negocio.DireccionLinea1,
            DireccionLinea2 = negocio.DireccionLinea2,
            CodigoPostal = negocio.CodigoPostal,
            Ciudad = negocio.Ciudad,
            Provincia = negocio.Provincia,
            Region = negocio.Region,
            PaisCodigoIso2 = negocio.PaisCodigoIso2,
            Latitud = negocio.Latitud,
            Longitud = negocio.Longitud,
            ZonaHoraria = negocio.ZonaHoraria,
            IdiomaPorDefecto = negocio.IdiomaPorDefecto,
            IdiomasSoportados = negocio.IdiomasSoportados,
            MonedaCodigo = negocio.MonedaCodigo,
            HorarioAperturaJson = negocio.HorarioAperturaJson,
            DiasFestivosJson = negocio.DiasFestivosJson,
            LogoPrincipalUrl = negocio.LogoPrincipalUrl,
            LogoSecundarioUrl = negocio.LogoSecundarioUrl,
            IconoUrl = negocio.IconoUrl,
            ImagenHeroUrl = negocio.ImagenHeroUrl,
            ImagenCoverUrl = negocio.ImagenCoverUrl,
            ImagenMobileUrl = negocio.ImagenMobileUrl,
            GaleriaImagenesJson = negocio.GaleriaImagenesJson,
            VideoPromocionalUrl = negocio.VideoPromocionalUrl,
            ColorPrimario = negocio.ColorPrimario,
            ColorSecundario = negocio.ColorSecundario,
            ColorAcento = negocio.ColorAcento,
            ColorFondo = negocio.ColorFondo,
            ColorTexto = negocio.ColorTexto,
            FuenteTitulo = negocio.FuenteTitulo,
            FuenteCuerpo = negocio.FuenteCuerpo,
            HeadlinePortal = negocio.HeadlinePortal,
            SubheadlinePortal = negocio.SubheadlinePortal,
            MensajeBienvenida = negocio.MensajeBienvenida,
            MensajeLegal = negocio.MensajeLegal,
            SeoTitle = negocio.SeoTitle,
            SeoDescription = negocio.SeoDescription,
            SeoKeywords = negocio.SeoKeywords,
            OpenGraphImageUrl = negocio.OpenGraphImageUrl,
            FacebookUrl = negocio.FacebookUrl,
            InstagramUrl = negocio.InstagramUrl,
            TikTokUrl = negocio.TikTokUrl,
            XUrl = negocio.XUrl,
            LinkedInUrl = negocio.LinkedInUrl,
            YoutubeUrl = negocio.YoutubeUrl,
            CondicionesUsoUrl = negocio.CondicionesUsoUrl,
            PoliticaPrivacidadUrl = negocio.PoliticaPrivacidadUrl,
            PoliticaCookiesUrl = negocio.PoliticaCookiesUrl,
            TextoCondicionesPrograma = negocio.TextoCondicionesPrograma,
            TextoPoliticaPuntos = negocio.TextoPoliticaPuntos,
            NombreProgramaFidelizacion = negocio.NombreProgramaFidelizacion,
            DescripcionProgramaFidelizacion = negocio.DescripcionProgramaFidelizacion,
            PuntosBienvenida = negocio.PuntosBienvenida,
            PuntosCumpleanos = negocio.PuntosCumpleanos,
            ValorMonetarioPunto = negocio.ValorMonetarioPunto,
            CaducidadPuntosDias = negocio.CaducidadPuntosDias,
            PermiteRegistroPublico = negocio.PermiteRegistroPublico,
            PublicadoPortal = negocio.PublicadoPortal,
            Activo = negocio.Activo,
            PermiteNotificacionesPush = negocio.PermiteNotificacionesPush,
            PermiteNotificacionesEmail = negocio.PermiteNotificacionesEmail,
            PermiteNotificacionesSms = negocio.PermiteNotificacionesSms,
            PermiteWalletPass = negocio.PermiteWalletPass,
            PermiteQrCheckIn = negocio.PermiteQrCheckIn,
            PermiteProgramaReferidos = negocio.PermiteProgramaReferidos,
            PermiteCampanasAutomatizadas = negocio.PermiteCampanasAutomatizadas,
            RequiereAprobacionManualClientes = negocio.RequiereAprobacionManualClientes,
            OcultarMarcaGeneral = negocio.OcultarMarcaGeneral,
            MaximoUbicaciones = negocio.MaximoUbicaciones,
            MaximoUsuariosBackoffice = negocio.MaximoUsuariosBackoffice,
            MaximoClientesRegistrados = negocio.MaximoClientesRegistrados,
            FechaPublicacionUtc = negocio.FechaPublicacionUtc,
            FechaInicioSuscripcionUtc = negocio.FechaInicioSuscripcionUtc,
            FechaFinSuscripcionUtc = negocio.FechaFinSuscripcionUtc,
            CreatedAtUtc = negocio.CreatedAtUtc,
            UpdatedAtUtc = negocio.UpdatedAtUtc
        };

    public static Negocio ToEntity(this CrearNegocioRequest request)
        => new();

    public static NegocioUsuarioVinculacionResponse ToResponse(this NegocioUsuarioVinculacion vinculacion)
        => new()
        {
            VinculacionId = vinculacion.Id,
            NegocioId = vinculacion.NegocioId,
            UserId = vinculacion.UserId,
            TipoVinculacion = vinculacion.TipoVinculacion,
            TituloRelacion = vinculacion.TituloRelacion,
            Activa = vinculacion.Activa,
            EsPrincipal = vinculacion.EsPrincipal,
            PuedeAccederBackoffice = vinculacion.PuedeAccederBackoffice,
            PuedeGestionarNegocio = vinculacion.PuedeGestionarNegocio,
            PuedeGestionarClientes = vinculacion.PuedeGestionarClientes,
            PuedeGestionarCampanas = vinculacion.PuedeGestionarCampanas,
            PuedeGestionarPuntos = vinculacion.PuedeGestionarPuntos,
            PuedeValidarTickets = vinculacion.PuedeValidarTickets,
            PuedeVerReportes = vinculacion.PuedeVerReportes,
            NotasInternas = vinculacion.NotasInternas,
            OrigenVinculacion = vinculacion.OrigenVinculacion,
            FechaInvitacionUtc = vinculacion.FechaInvitacionUtc,
            FechaAceptacionUtc = vinculacion.FechaAceptacionUtc,
            FechaInicioUtc = vinculacion.FechaInicioUtc,
            FechaFinUtc = vinculacion.FechaFinUtc,
            CreatedAtUtc = vinculacion.CreatedAtUtc,
            UpdatedAtUtc = vinculacion.UpdatedAtUtc,
            RevokedAtUtc = vinculacion.RevokedAtUtc
        };

    public static NegocioVinculadoResponse ToNegocioVinculadoResponse(this NegocioUsuarioVinculacion vinculacion)
        => new()
        {
            VinculacionId = vinculacion.Id,
            NegocioId = vinculacion.NegocioId,
            NombreComercial = vinculacion.Negocio?.NombreComercial ?? string.Empty,
            SlugPortal = vinculacion.Negocio?.SlugPortal ?? string.Empty,
            LogoPrincipalUrl = vinculacion.Negocio?.LogoPrincipalUrl,
            ImagenHeroUrl = vinculacion.Negocio?.ImagenHeroUrl,
            ColorPrimario = vinculacion.Negocio?.ColorPrimario,
            ColorSecundario = vinculacion.Negocio?.ColorSecundario,
            NegocioActivo = vinculacion.Negocio?.Activo ?? false,
            PortalPublicado = vinculacion.Negocio?.PublicadoPortal ?? false,
            TipoVinculacion = vinculacion.TipoVinculacion,
            TituloRelacion = vinculacion.TituloRelacion,
            EsPrincipal = vinculacion.EsPrincipal,
            PuedeAccederBackoffice = vinculacion.PuedeAccederBackoffice,
            PuedeGestionarNegocio = vinculacion.PuedeGestionarNegocio,
            PuedeGestionarClientes = vinculacion.PuedeGestionarClientes,
            PuedeGestionarCampanas = vinculacion.PuedeGestionarCampanas,
            PuedeGestionarPuntos = vinculacion.PuedeGestionarPuntos,
            PuedeValidarTickets = vinculacion.PuedeValidarTickets,
            PuedeVerReportes = vinculacion.PuedeVerReportes,
            FechaInicioUtc = vinculacion.FechaInicioUtc,
            FechaFinUtc = vinculacion.FechaFinUtc,
            FechaVinculacionUtc = vinculacion.CreatedAtUtc
        };

    public static void Apply(this CrearNegocioRequest request, Negocio negocio)
    {
        negocio.OwnerUserId = request.OwnerUserId;
        negocio.NombreComercial = request.NombreComercial.Trim();
        negocio.SlugPortal = request.SlugPortal.Trim().ToLowerInvariant();
        negocio.CodigoInterno = Normalize(request.CodigoInterno);
        negocio.ReferenciaExterna = Normalize(request.ReferenciaExterna);
        negocio.RazonSocial = Normalize(request.RazonSocial);
        negocio.DocumentoFiscal = Normalize(request.DocumentoFiscal);
        negocio.RegistroMercantil = Normalize(request.RegistroMercantil);
        negocio.TipoNegocio = request.TipoNegocio;
        negocio.Estado = request.Estado;
        negocio.PlanSuscripcion = request.PlanSuscripcion;
        negocio.CategoriaPrincipal = Normalize(request.CategoriaPrincipal);
        negocio.Subcategoria = Normalize(request.Subcategoria);
        negocio.Etiquetas = Normalize(request.Etiquetas);
        negocio.DescripcionCorta = Normalize(request.DescripcionCorta);
        negocio.DescripcionLarga = Normalize(request.DescripcionLarga);
        negocio.Eslogan = Normalize(request.Eslogan);
        negocio.HistoriaMarca = Normalize(request.HistoriaMarca);
        negocio.Mision = Normalize(request.Mision);
        negocio.Vision = Normalize(request.Vision);
        negocio.Valores = Normalize(request.Valores);
        negocio.PersonaObjetivo = Normalize(request.PersonaObjetivo);
        negocio.EmailContacto = Normalize(request.EmailContacto);
        negocio.EmailSoporte = Normalize(request.EmailSoporte);
        negocio.TelefonoPrincipal = Normalize(request.TelefonoPrincipal);
        negocio.TelefonoSecundario = Normalize(request.TelefonoSecundario);
        negocio.WhatsApp = Normalize(request.WhatsApp);
        negocio.SitioWebUrl = Normalize(request.SitioWebUrl);
        negocio.DominioPersonalizado = Normalize(request.DominioPersonalizado);
        negocio.RutaPortal = Normalize(request.RutaPortal);
        negocio.DireccionLinea1 = Normalize(request.DireccionLinea1);
        negocio.DireccionLinea2 = Normalize(request.DireccionLinea2);
        negocio.CodigoPostal = Normalize(request.CodigoPostal);
        negocio.Ciudad = Normalize(request.Ciudad);
        negocio.Provincia = Normalize(request.Provincia);
        negocio.Region = Normalize(request.Region);
        negocio.PaisCodigoIso2 = Normalize(request.PaisCodigoIso2)?.ToUpperInvariant();
        negocio.Latitud = request.Latitud;
        negocio.Longitud = request.Longitud;
        negocio.ZonaHoraria = Normalize(request.ZonaHoraria);
        negocio.IdiomaPorDefecto = Normalize(request.IdiomaPorDefecto);
        negocio.IdiomasSoportados = Normalize(request.IdiomasSoportados);
        negocio.MonedaCodigo = Normalize(request.MonedaCodigo)?.ToUpperInvariant();
        negocio.HorarioAperturaJson = Normalize(request.HorarioAperturaJson);
        negocio.DiasFestivosJson = Normalize(request.DiasFestivosJson);
        negocio.LogoPrincipalUrl = Normalize(request.LogoPrincipalUrl);
        negocio.LogoSecundarioUrl = Normalize(request.LogoSecundarioUrl);
        negocio.IconoUrl = Normalize(request.IconoUrl);
        negocio.ImagenHeroUrl = Normalize(request.ImagenHeroUrl);
        negocio.ImagenCoverUrl = Normalize(request.ImagenCoverUrl);
        negocio.ImagenMobileUrl = Normalize(request.ImagenMobileUrl);
        negocio.GaleriaImagenesJson = Normalize(request.GaleriaImagenesJson);
        negocio.VideoPromocionalUrl = Normalize(request.VideoPromocionalUrl);
        negocio.ColorPrimario = Normalize(request.ColorPrimario);
        negocio.ColorSecundario = Normalize(request.ColorSecundario);
        negocio.ColorAcento = Normalize(request.ColorAcento);
        negocio.ColorFondo = Normalize(request.ColorFondo);
        negocio.ColorTexto = Normalize(request.ColorTexto);
        negocio.FuenteTitulo = Normalize(request.FuenteTitulo);
        negocio.FuenteCuerpo = Normalize(request.FuenteCuerpo);
        negocio.HeadlinePortal = Normalize(request.HeadlinePortal);
        negocio.SubheadlinePortal = Normalize(request.SubheadlinePortal);
        negocio.MensajeBienvenida = Normalize(request.MensajeBienvenida);
        negocio.MensajeLegal = Normalize(request.MensajeLegal);
        negocio.SeoTitle = Normalize(request.SeoTitle);
        negocio.SeoDescription = Normalize(request.SeoDescription);
        negocio.SeoKeywords = Normalize(request.SeoKeywords);
        negocio.OpenGraphImageUrl = Normalize(request.OpenGraphImageUrl);
        negocio.FacebookUrl = Normalize(request.FacebookUrl);
        negocio.InstagramUrl = Normalize(request.InstagramUrl);
        negocio.TikTokUrl = Normalize(request.TikTokUrl);
        negocio.XUrl = Normalize(request.XUrl);
        negocio.LinkedInUrl = Normalize(request.LinkedInUrl);
        negocio.YoutubeUrl = Normalize(request.YoutubeUrl);
        negocio.CondicionesUsoUrl = Normalize(request.CondicionesUsoUrl);
        negocio.PoliticaPrivacidadUrl = Normalize(request.PoliticaPrivacidadUrl);
        negocio.PoliticaCookiesUrl = Normalize(request.PoliticaCookiesUrl);
        negocio.TextoCondicionesPrograma = Normalize(request.TextoCondicionesPrograma);
        negocio.TextoPoliticaPuntos = Normalize(request.TextoPoliticaPuntos);
        negocio.NombreProgramaFidelizacion = Normalize(request.NombreProgramaFidelizacion);
        negocio.DescripcionProgramaFidelizacion = Normalize(request.DescripcionProgramaFidelizacion);
        negocio.PuntosBienvenida = request.PuntosBienvenida;
        negocio.PuntosCumpleanos = request.PuntosCumpleanos;
        negocio.ValorMonetarioPunto = request.ValorMonetarioPunto;
        negocio.CaducidadPuntosDias = request.CaducidadPuntosDias;
        negocio.PermiteRegistroPublico = request.PermiteRegistroPublico;
        negocio.PublicadoPortal = request.PublicadoPortal;
        negocio.Activo = request.Activo;
        negocio.PermiteNotificacionesPush = request.PermiteNotificacionesPush;
        negocio.PermiteNotificacionesEmail = request.PermiteNotificacionesEmail;
        negocio.PermiteNotificacionesSms = request.PermiteNotificacionesSms;
        negocio.PermiteWalletPass = request.PermiteWalletPass;
        negocio.PermiteQrCheckIn = request.PermiteQrCheckIn;
        negocio.PermiteProgramaReferidos = request.PermiteProgramaReferidos;
        negocio.PermiteCampanasAutomatizadas = request.PermiteCampanasAutomatizadas;
        negocio.RequiereAprobacionManualClientes = request.RequiereAprobacionManualClientes;
        negocio.OcultarMarcaGeneral = request.OcultarMarcaGeneral;
        negocio.MaximoUbicaciones = request.MaximoUbicaciones;
        negocio.MaximoUsuariosBackoffice = request.MaximoUsuariosBackoffice;
        negocio.MaximoClientesRegistrados = request.MaximoClientesRegistrados;
        negocio.FechaPublicacionUtc = request.FechaPublicacionUtc;
        negocio.FechaInicioSuscripcionUtc = request.FechaInicioSuscripcionUtc;
        negocio.FechaFinSuscripcionUtc = request.FechaFinSuscripcionUtc;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
