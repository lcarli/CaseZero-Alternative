using CaseZeroApi.Models;

namespace CaseZeroApi.DTOs
{
    /// <summary>
    /// DTOs ativos do sistema CaseV1
    /// DTOs obsoletos movidos para DTOs/OBSOLETE/OldCaseDtos.cs
    /// </summary>

    /// <summary>
    /// DTO para sessões de caso (CaseSessionController)
    /// </summary>
    public class CaseSessionDto
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string CaseId { get; set; } = string.Empty;
        public DateTime SessionStart { get; set; }
        public DateTime? SessionEnd { get; set; }
        public int SessionDurationMinutes { get; set; }
        public string? GameTimeAtStart { get; set; }
        public string? GameTimeAtEnd { get; set; }
        public SessionStatus Status { get; set; }
    }

    public class StartCaseSessionRequest
    {
        public required string CaseId { get; set; }
        public string? GameTimeAtStart { get; set; }
    }

    public class EndCaseSessionRequest
    {
        public string? GameTimeAtEnd { get; set; }
    }

    public class CaseSessionStateDto
    {
        public CaseSessionDto? Session { get; set; }
        public List<string> VisibleAssetIds { get; set; } = new();
        public List<string> VisibleEmailIds { get; set; } = new();
        public Dictionary<string, EmailStateDto> EmailStates { get; set; } = new();
    }

    public class EmailStateDto
    {
        public string EmailId { get; set; } = string.Empty;
        public DateTime? ReadAt { get; set; }
        public int OpenCount { get; set; }
    }

    /// <summary>
    /// DTO para assets do case.json v1.0
    /// Representa arquivos no Blob Storage filtrados por visibilidade
    /// Usado por AssetsController
    /// </summary>
    public class AssetDto
    {
        public string Id { get; set; } = string.Empty;
        public string CaseId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? FilePath { get; set; }
        public bool IsVisible { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }

    /// <summary>
    /// DTO para emails do EMAIL_SYSTEM (case.json v1.0)
    /// Representa emails filtrados por visibilidade
    /// Usado por EmailsController
    /// </summary>
    public class CaseEmailDto
    {
        public string EmailId { get; set; } = string.Empty;
        public string From { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string SentAt { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public bool HasAttachments { get; set; }
        public int AttachmentCount { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public int OpenCount { get; set; }
    }
}
