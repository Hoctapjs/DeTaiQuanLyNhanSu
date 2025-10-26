namespace DeTaiNhanSu.Dtos
{
    public class TerminateContractRequest
    {
        public DateOnly? TerminatedAt { get; set; }
        public string? Reason { get; set; }
    }
}
