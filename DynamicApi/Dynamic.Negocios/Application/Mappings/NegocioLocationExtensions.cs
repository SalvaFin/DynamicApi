using Dynamic.Negocios.Domain.Entities;

namespace Dynamic.Negocios.Application.Mappings;

public static class NegocioLocationExtensions
{
    public static bool HasValidCoordinates(this Negocio negocio)
        => negocio.Latitud is >= -90 and <= 90 &&
           negocio.Longitud is >= -180 and <= 180;
}
