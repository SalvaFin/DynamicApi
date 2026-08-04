using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Dynamic.Negocios.Application.ModelBinding;

public sealed class CoordinateModelBinder : IModelBinder
{
    private const NumberStyles CoordinateNumberStyles =
        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        ValueProviderResult valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (valueProviderResult == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueProviderResult);

        string? rawValue = valueProviderResult.FirstValue?.Trim();
        if (string.IsNullOrEmpty(rawValue))
        {
            bindingContext.Result = ModelBindingResult.Success(null);
            return Task.CompletedTask;
        }

        if (TryParse(rawValue, out decimal coordinate))
        {
            bindingContext.Result = ModelBindingResult.Success(coordinate);
            return Task.CompletedTask;
        }

        bindingContext.ModelState.TryAddModelError(
            bindingContext.ModelName,
            $"El valor '{rawValue}' no es una coordenada v\u00e1lida.");

        return Task.CompletedTask;
    }

    private static bool TryParse(string value, out decimal coordinate)
    {
        if (decimal.TryParse(value, CoordinateNumberStyles, CultureInfo.InvariantCulture, out coordinate))
        {
            return true;
        }

        if (value.Contains(',') && !value.Contains('.'))
        {
            return decimal.TryParse(
                value.Replace(',', '.'),
                CoordinateNumberStyles,
                CultureInfo.InvariantCulture,
                out coordinate);
        }

        coordinate = default;
        return false;
    }
}
