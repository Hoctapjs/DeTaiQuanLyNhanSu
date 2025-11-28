namespace DeTaiNhanSu.Models
{
    public class WorkSchedule
    {
        //public Guid Id { get; set; }
        //public Guid EmployeeId { get; set; }
        //public DateOnly Date { get; set; }
        //public string? Shift { get; set; }
        //public TimeOnly? StartTime { get; set; }
        //public TimeOnly? EndTime { get; set; }
        //public string? Note { get; set; }

        //public Employee Employee { get; set; } = default!;

        public Guid Id { get; set; }
        public Guid EmployeeId { get; set; }
        public DateOnly Date { get; set; }

        // [MỚI] Liên kết tới ca làm việc chuẩn
        public Guid ShiftTemplateId { get; set; }

        // Ta giữ lại Start/EndTime/Note chỉ khi cần ghi đè ca chuẩn hoặc chi tiết khác biệt.
        // Tuy nhiên, để đơn giản hóa, ta sẽ xóa chúng và chỉ dùng ShiftTemplateId.
        public string? Note { get; set; }

        // Navigation properties
        public Employee Employee { get; set; } = default!;
        public ShiftTemplate ShiftTemplate { get; set; } = default!;
    }
}
