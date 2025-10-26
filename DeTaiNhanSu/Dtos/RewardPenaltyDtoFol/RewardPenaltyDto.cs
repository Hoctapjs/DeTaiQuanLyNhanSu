namespace DeTaiNhanSu.Dtos.RewardPenaltyDtoFol
{
    public class RewardPenaltyDto
    {
        public Guid Id { get; set; }
        public Guid EmployeeId { get; set; }
        public Guid TypeId { get; set; }
        public string? TypeName { get; set; }
        public string? Kind { get; set; }                 // enum -> int
        public decimal? DefaultAmount { get; set; }
        public decimal? AmountOverride { get; set; }
        public decimal? FinalAmount { get; set; }      // AmountOverride ?? DefaultAmount
        public string? CustomReason { get; set; }
        public DateOnly DecidedAt { get; set; }
        public Guid DecidedBy { get; set; }
    }
}
