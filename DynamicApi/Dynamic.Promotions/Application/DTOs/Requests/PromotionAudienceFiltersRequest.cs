using Dynamic.Users.Domain.Enums;

namespace Dynamic.Promotions.Application.DTOs.Requests;

public class PromotionAudienceFiltersRequest
{
    public IReadOnlyCollection<UserGender>? Genders { get; set; }
    public int? MinimumAge { get; set; }
    public int? MaximumAge { get; set; }
    public int? MinimumCurrentPoints { get; set; }
    public int? MaximumCurrentPoints { get; set; }
    public int? MinimumTotalPointsEarned { get; set; }
    public int? MaximumTotalPointsEarned { get; set; }
    public int? MinimumTotalPointsSpent { get; set; }
    public int? MaximumTotalPointsSpent { get; set; }
    public DateTime? LastPointsEarnedBeforeUtc { get; set; }
    public DateTime? LastPointsEarnedAfterUtc { get; set; }
    public DateTime? LastPointsSpentBeforeUtc { get; set; }
    public DateTime? LastPointsSpentAfterUtc { get; set; }
    public int? MinimumDaysSinceLastPointsEarned { get; set; }
    public int? MaximumDaysSinceLastPointsEarned { get; set; }
    public bool IncludeUsersWithoutPointEarnings { get; set; }
    public DateTime? LastActivityBeforeUtc { get; set; }
    public DateTime? LastActivityAfterUtc { get; set; }
    public DateTime? CustomerSinceBeforeUtc { get; set; }
    public DateTime? CustomerSinceAfterUtc { get; set; }
    public DateTime? RegisteredBeforeUtc { get; set; }
    public DateTime? RegisteredAfterUtc { get; set; }
    public DateTime? LastAppSeenBeforeUtc { get; set; }
    public DateTime? LastAppSeenAfterUtc { get; set; }
    public int? MinimumDaysSinceLastAppSeen { get; set; }
    public int? MaximumDaysSinceLastAppSeen { get; set; }
    public int? BirthMonth { get; set; }
    public IReadOnlyCollection<string>? PostalCodes { get; set; }
    public IReadOnlyCollection<string>? Regions { get; set; }
    public IReadOnlyCollection<string>? CountryCodes { get; set; }
    public IReadOnlyCollection<string>? Languages { get; set; }
    public bool? HasAnyPoints { get; set; }
    public bool? HasAnyTickets { get; set; }
    public bool? HasActiveTickets { get; set; }
    public bool? HasEverUsedTicket { get; set; }
    public int? MinimumTicketCount { get; set; }
    public int? MaximumTicketCount { get; set; }
    public int? MinimumUsedTicketCount { get; set; }
    public int? MaximumUsedTicketCount { get; set; }
    public bool? HasActivePushNotifications { get; set; }
    public bool? HasConfirmedEmail { get; set; }
}
