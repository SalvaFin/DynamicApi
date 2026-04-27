using Dynamic.Negocios.Application.Common;
using Dynamic.Negocios.Application.Contracts.Services;
using Dynamic.Negocios.Application.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dynamic.Negocios.Infrastructure.Services;

public class NegocioMediaStorageService : INegocioMediaStorageService
{
    private static readonly HashSet<string> AllowedExtensions =
    [
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".gif",
        ".svg"
    ];

    private readonly NegocioMediaOptions _options;
    private readonly IHostEnvironment _hostEnvironment;

    public NegocioMediaStorageService(IOptions<NegocioMediaOptions> options, IHostEnvironment hostEnvironment)
    {
        _options = options.Value;
        _hostEnvironment = hostEnvironment;
    }

    public async Task<ServiceResult<string>> SaveImageAsync(
        Guid negocioId,
        string imageSlot,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (file.Length <= 0)
        {
            return ServiceResult<string>.Failure("validation_error", "El fichero de imagen est\u00e1 vac\u00edo.");
        }

        if (file.Length > _options.MaxFileSizeBytes)
        {
            return ServiceResult<string>.Failure("validation_error", $"La imagen supera el tama\u00f1o m\u00e1ximo permitido de {_options.MaxFileSizeBytes} bytes.");
        }

        if (string.IsNullOrWhiteSpace(file.ContentType) || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<string>.Failure("validation_error", "El fichero enviado no es una imagen v\u00e1lida.");
        }

        string extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension.ToLowerInvariant()))
        {
            return ServiceResult<string>.Failure("validation_error", "La extensi\u00f3n de la imagen no est\u00e1 permitida.");
        }

        string storageRootPath = ResolveStorageRootPath();
        string negocioFolderPath = Path.Combine(storageRootPath, negocioId.ToString("N"));
        Directory.CreateDirectory(negocioFolderPath);

        string safeSlot = string.Concat(imageSlot.Where(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_'));
        string fileName = $"{safeSlot}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        string absoluteFilePath = Path.Combine(negocioFolderPath, fileName);

        await using FileStream stream = new(absoluteFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await file.CopyToAsync(stream, cancellationToken);

        string relativePublicPath = $"{NormalizePublicPathPrefix()}/{negocioId:N}/{fileName}";
        string publicUrl = BuildPublicUrl(relativePublicPath);

        return ServiceResult<string>.Success(publicUrl);
    }

    private string ResolveStorageRootPath()
        => Path.IsPathRooted(_options.StorageRootPath)
            ? _options.StorageRootPath
            : Path.GetFullPath(Path.Combine(_hostEnvironment.ContentRootPath, _options.StorageRootPath));

    private string NormalizePublicPathPrefix()
    {
        string normalized = string.IsNullOrWhiteSpace(_options.PublicPathPrefix)
            ? "/negocios-media"
            : _options.PublicPathPrefix.Trim();

        if (!normalized.StartsWith('/'))
        {
            normalized = $"/{normalized}";
        }

        return normalized.TrimEnd('/');
    }

    private string BuildPublicUrl(string relativePublicPath)
    {
        if (string.IsNullOrWhiteSpace(_options.PublicBaseUrl))
        {
            return relativePublicPath;
        }

        return $"{_options.PublicBaseUrl.TrimEnd('/')}{relativePublicPath}";
    }
}
