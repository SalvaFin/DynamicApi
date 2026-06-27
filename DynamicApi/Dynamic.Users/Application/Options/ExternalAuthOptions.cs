namespace Dynamic.Users.Application.Options;

public class ExternalAuthOptions
{
    public const string SectionName = "ExternalAuth";

    public List<string> GoogleClientIds { get; set; } = [];
    public List<string> AppleClientIds { get; set; } = [];
    public bool LinkExistingUsersByVerifiedEmail { get; set; } = true;
}
