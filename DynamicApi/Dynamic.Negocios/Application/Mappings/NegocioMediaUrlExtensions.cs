using System.Text.Json;
using Dynamic.Negocios.Application.DTOs.Responses;
using Dynamic.Negocios.Application.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Dynamic.Negocios.Application.Mappings;

public static class NegocioMediaUrlExtensions
{
    public static NegocioResponse WithResolvedMediaUrls(this NegocioResponse response, HttpRequest request)
    {
        response.LogoPrincipalUrl = ToAbsoluteUrl(response.LogoPrincipalUrl, request);
        response.LogoSecundarioUrl = ToAbsoluteUrl(response.LogoSecundarioUrl, request);
        response.IconoUrl = ToAbsoluteUrl(response.IconoUrl, request);
        response.ImagenHeroUrl = ToAbsoluteUrl(response.ImagenHeroUrl, request);
        response.ImagenCoverUrl = ToAbsoluteUrl(response.ImagenCoverUrl, request);
        response.ImagenMobileUrl = ToAbsoluteUrl(response.ImagenMobileUrl, request);
        response.OpenGraphImageUrl = ToAbsoluteUrl(response.OpenGraphImageUrl, request);
        response.GaleriaImagenesJson = ToAbsoluteGalleryJson(response.GaleriaImagenesJson, request);
        return response;
    }

    public static IReadOnlyCollection<NegocioResponse> WithResolvedMediaUrls(
        this IReadOnlyCollection<NegocioResponse> responses,
        HttpRequest request)
        => responses.Select(response => response.WithResolvedMediaUrls(request)).ToArray();

    public static ExplorarNegociosResponse WithResolvedMediaUrls(this ExplorarNegociosResponse response, HttpRequest request)
    {
        response.Items = response.Items.Select(item => item.WithResolvedMediaUrls(request)).ToArray();
        return response;
    }

    public static ExplorarNegocioResponse WithResolvedMediaUrls(this ExplorarNegocioResponse response, HttpRequest request)
    {
        response.LogoPrincipalUrl = ToAbsoluteUrl(response.LogoPrincipalUrl, request);
        response.IconoUrl = ToAbsoluteUrl(response.IconoUrl, request);
        response.ImagenCoverUrl = ToAbsoluteUrl(response.ImagenCoverUrl, request);
        response.ImagenMobileUrl = ToAbsoluteUrl(response.ImagenMobileUrl, request);
        return response;
    }

    public static NegocioVinculadoResponse WithResolvedMediaUrls(this NegocioVinculadoResponse response, HttpRequest request)
    {
        response.LogoPrincipalUrl = ToAbsoluteUrl(response.LogoPrincipalUrl, request);
        response.ImagenHeroUrl = ToAbsoluteUrl(response.ImagenHeroUrl, request);
        return response;
    }

    public static IReadOnlyCollection<NegocioVinculadoResponse> WithResolvedMediaUrls(
        this IReadOnlyCollection<NegocioVinculadoResponse> responses,
        HttpRequest request)
        => responses.Select(response => response.WithResolvedMediaUrls(request)).ToArray();

    private static string? ToAbsoluteGalleryJson(string? galleryJson, HttpRequest request)
    {
        if (string.IsNullOrWhiteSpace(galleryJson))
        {
            return galleryJson;
        }

        try
        {
            List<string>? urls = JsonSerializer.Deserialize<List<string>>(galleryJson);
            if (urls is null)
            {
                return galleryJson;
            }

            List<string> resolved = urls
                .Select(url => ToAbsoluteUrl(url, request) ?? string.Empty)
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .ToList();

            return JsonSerializer.Serialize(resolved);
        }
        catch
        {
            return galleryJson;
        }
    }

    private static string? ToAbsoluteUrl(string? url, HttpRequest request)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            return url;
        }

        string normalizedPath = url.StartsWith('/') ? url : $"/{url}";
        NegocioMediaOptions? mediaOptions =
            request.HttpContext.RequestServices.GetService(typeof(IOptions<NegocioMediaOptions>)) is IOptions<NegocioMediaOptions> optionsAccessor
                ? optionsAccessor.Value
                : null;

        if (!string.IsNullOrWhiteSpace(mediaOptions?.PublicBaseUrl))
        {
            string normalizedBaseUrl = mediaOptions.PublicBaseUrl.TrimEnd('/');
            return $"{normalizedBaseUrl}{normalizedPath}";
        }

        return $"{request.Scheme}://{request.Host}{request.PathBase}{normalizedPath}";
    }
}
