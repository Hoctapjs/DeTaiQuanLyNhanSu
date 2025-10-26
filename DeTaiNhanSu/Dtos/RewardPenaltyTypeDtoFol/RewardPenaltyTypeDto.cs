namespace DeTaiNhanSu.Dtos.RewardPenaltyTypeDtoFol
{
    public class RewardPenaltyTypeDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string Type { get; set; } = default!;
        public decimal? DefaultAmount { get; set; }
        public string Level { get; set; } = default!;
        public string Form { get; set; } = default!;
        public string? Description { get; set; }
    }
}
