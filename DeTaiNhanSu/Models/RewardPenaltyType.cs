using DeTaiNhanSu.Enums;

namespace DeTaiNhanSu.Models
{
    public class RewardPenaltyType
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public RewardPenaltyKind Type { get; set; }
        public decimal? DefaultAmount { get; set; }
        public SeverityLevel Level { get; set; }
        public ActionForm Form { get; set; }
        public string? Description { get; set; }
    }
}
