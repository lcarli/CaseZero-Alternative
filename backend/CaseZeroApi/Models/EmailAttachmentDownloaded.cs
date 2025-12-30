namespace CaseZeroApi.Models
{
    /// <summary>
    /// Tracks when email attachments are downloaded by users
    /// </summary>
    public class EmailAttachmentDownloaded
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string CaseId { get; set; } = string.Empty;
        public string EmailId { get; set; } = string.Empty;
        public string AssetId { get; set; } = string.Empty;
        public DateTime DownloadedAt { get; set; }

        // Navigation properties
        public virtual User User { get; set; } = null!;
    }
}
