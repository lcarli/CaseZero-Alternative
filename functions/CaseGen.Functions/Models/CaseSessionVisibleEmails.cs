namespace CaseGen.Functions.Models;

public class CaseSessionVisibleEmails
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string CaseId { get; set; } = string.Empty;
    public string EmailId { get; set; } = string.Empty;
    public DateTime RevealedAt { get; set; }
}
