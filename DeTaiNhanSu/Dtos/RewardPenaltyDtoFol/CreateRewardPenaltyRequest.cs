namespace DeTaiNhanSu.Dtos.RewardPenaltyDtoFol
{
    public class CreateRewardPenaltyRequest
    {
        public Guid EmployeeId { get; set; }
        public Guid TypeId { get; set; }
        public decimal? AmountOverride { get; set; }
        public string? CustomReason { get; set; }
        public DateOnly? DecidedAt { get; set; }
        public Guid DecidedBy { get; set; }
    }
}
