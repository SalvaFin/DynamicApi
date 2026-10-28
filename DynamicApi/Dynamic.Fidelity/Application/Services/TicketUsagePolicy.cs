using System.Globalization;
using Dynamic.Fidelity.Application.Common;
using Dynamic.Fidelity.Domain.Entities;

namespace Dynamic.Fidelity.Application.Services;

public static class TicketUsagePolicy
{
    private static readonly string[] Days = ["", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado", "domingo"];

    public static ServiceResult ValidateConfiguration(decimal? minimum, IReadOnlyCollection<TicketUsageWindow>? windows)
    {
        if (minimum.HasValue && (minimum < 0 || minimum > 99999999.99m || decimal.Round(minimum.Value, 2) != minimum))
            return ServiceResult.Failure("validation_error", "La cesta mínima debe ser un importe no negativo con un máximo de dos decimales.");
        if (windows?.Count > 56 || windows?.Any(w => w is null || w.DayOfWeek is < 1 or > 7 ||
                ((w.StartTime is null) != (w.EndTime is null)) ||
                (w.StartTime is not null && (!TryTime(w.StartTime, out var start) || !TryTime(w.EndTime, out var end) || start == end))) == true)
            return ServiceResult.Failure("validation_error", "Indica días válidos y franjas con inicio y fin distintos (HH:mm), o deja ambas horas vacías para todo el día. Máximo 56 franjas.");
        return ServiceResult.Success();
    }

    public static ServiceResult Check(Ticket ticket, decimal? amount, DateTime nowUtc, string? timeZone)
    {
        if (amount < 0 || amount > 99999999.99m || (amount.HasValue && decimal.Round(amount.Value, 2) != amount))
            return ServiceResult.Failure("validation_error", "El importe de la cuenta debe ser no negativo y tener como máximo dos decimales.");
        if (ticket.GastoMinimoRequerido > 0 && (!amount.HasValue || amount < ticket.GastoMinimoRequerido))
            return ServiceResult.Failure("conflict", $"Este ticket requiere una cesta mínima de {ticket.GastoMinimoRequerido.Value.ToString("0.00", CultureInfo.GetCultureInfo("es-ES"))} €. " +
                (amount.HasValue ? "El importe de la cuenta no alcanza la cesta mínima." : "Introduce el importe de la cuenta para poder validarlo."));
        if (ticket.UsageWindows is not { Count: > 0 }) return ServiceResult.Success();

        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(timeZone) ? "Europe/Madrid" : timeZone); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        { return ServiceResult.Failure("conflict", "No se puede comprobar el horario: revisa la zona horaria del local."); }
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), zone);
        var day = local.DayOfWeek == System.DayOfWeek.Sunday ? 7 : (int)local.DayOfWeek;
        var previousDay = day == 1 ? 7 : day - 1;
        var time = TimeOnly.FromDateTime(local);
        foreach (var w in ticket.UsageWindows)
        {
            if (w.StartTime is null && w.EndTime is null && w.DayOfWeek == day) return ServiceResult.Success();
            if (!TryTime(w.StartTime, out var start) || !TryTime(w.EndTime, out var end)) continue;
            if (start < end ? w.DayOfWeek == day && time >= start && time < end :
                (w.DayOfWeek == day && time >= start) || (w.DayOfWeek == previousDay && time < end))
                return ServiceResult.Success();
        }
        return ServiceResult.Failure("conflict", $"Este ticket solo es válido {DescribeSchedule(ticket.UsageWindows)} (hora del local: {zone.Id}).");
    }

    public static string Describe(Ticket ticket)
    {
        List<string> parts = [];
        if (ticket.GastoMinimoRequerido > 0) parts.Add($"Cesta mínima: {ticket.GastoMinimoRequerido.Value.ToString("0.00", CultureInfo.GetCultureInfo("es-ES"))} €.");
        if (ticket.UsageWindows is { Count: > 0 }) parts.Add($"Válido {DescribeSchedule(ticket.UsageWindows)} (hora del local).");
        return string.Join(" ", parts);
    }

    private static string DescribeSchedule(IEnumerable<TicketUsageWindow> windows) => string.Join("; ", windows.Select(w =>
        $"los {Days[w.DayOfWeek]} " + (w.StartTime is null ? "todo el día" : $"de {w.StartTime} a {w.EndTime}" +
            (string.CompareOrdinal(w.StartTime, w.EndTime) > 0 ? " del día siguiente" : ""))));
    private static bool TryTime(string? value, out TimeOnly time) => TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
}
