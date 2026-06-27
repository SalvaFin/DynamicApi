namespace Dynamic.Promotions.Application.Options;

public class FirebasePushOptions
{
    public const string SectionName = "Promotions:Firebase";

    public bool Enabled { get; set; }
    public string ProjectId { get; set; } = string.Empty;
    public string ServiceAccountJson { get; set; } = string.Empty;
    public string AndroidChannelId { get; set; } = "promotions";
}
