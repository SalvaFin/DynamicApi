using System.ComponentModel.DataAnnotations;

namespace Dynamic.Users.Application.DTOs.Requests;

public class ResolveSplitRecipientRequest
{
    [MaxLength(256)]
    public string? Email { get; set; }

    [MaxLength(2048)]
    public string? ShareQrToken { get; set; }
}

public class CreatePointsSplitRequest
{
    [Required]
    [MaxLength(9)]
    public List<string> RecipientEmails { get; set; } = [];
}
