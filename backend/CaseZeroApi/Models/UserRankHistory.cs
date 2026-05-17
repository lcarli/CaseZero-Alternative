namespace CaseZeroApi.Models
{
    /// <summary>
    /// Chronological log of a user's rank transitions. One row is inserted at
    /// account creation (with PreviousRank = null), and one additional row per
    /// promotion event. Powers the rank progression timeline on the profile page.
    /// </summary>
    public class UserRankHistory
    {
        public int Id { get; set; }
        public required string UserId { get; set; }
        public DetectiveRank? PreviousRank { get; set; }
        public DetectiveRank NewRank { get; set; }
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
        public string? Reason { get; set; }

        public virtual User User { get; set; } = null!;
    }
}
