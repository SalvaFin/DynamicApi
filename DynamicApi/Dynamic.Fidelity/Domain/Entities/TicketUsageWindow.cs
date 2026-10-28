namespace Dynamic.Fidelity.Domain.Entities;

public sealed record TicketUsageWindow
{
    // ISO weekday: Monday = 1, Sunday = 7. Empty times allow the whole day.
    public int DayOfWeek { get; init; }
    public string? StartTime { get; init; }
    public string? EndTime { get; init; }
}
