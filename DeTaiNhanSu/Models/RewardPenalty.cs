namespace DeTaiNhanSu.Models
{
    public class RewardPenalty
    {
        public Guid Id { get; set; }
        public Guid EmployeeId { get; set; }
        public Guid TypeId { get; set; }
        public decimal? AmountOverride { get; set; }
        public string? CustomReason { get; set; }
        public DateOnly DecidedAt { get; set; }
        public Guid DecidedBy { get; set; }

        public Employee Employee { get; set; } = default!;
        public RewardPenaltyType Type { get; set; } = default!;
        public User DecidedByUser { get; set; } = default!;
    }
}
