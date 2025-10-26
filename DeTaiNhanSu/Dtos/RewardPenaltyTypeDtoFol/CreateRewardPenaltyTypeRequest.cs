using DeTaiNhanSu.Enums;

namespace DeTaiNhanSu.Dtos.RewardPenaltyTypeDtoFol
{
    public class CreateRewardPenaltyTypeRequest
    {
        public string Name { get; set; } = default!;
        public RewardPenaltyKind? Type { get; set; }
        public decimal? DefaultAmount { get; set; }
        public SeverityLevel? Level { get; set; }
        public ActionForm? Form { get; set; }
        public string? Description { get; set; }
    }
}
