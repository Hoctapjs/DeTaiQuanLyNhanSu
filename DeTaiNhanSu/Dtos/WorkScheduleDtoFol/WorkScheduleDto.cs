namespace DeTaiNhanSu.Dtos.WorkScheduleDtoFol
{
    public class WorkScheduleDto
    {
        public Guid Id { get; set; }
        public Guid EmployeeId { get; set; }
        public string? EmployeeFullName { get; set; }
        public DateOnly Date { get; set; }

        //  ID của mẫu ca làm việc được áp dụng
        public Guid ShiftTemplateId { get; set; }

        //  Tên ca làm việc (Lấy từ ShiftTemplate.Name)
        public string? ShiftName { get; set; }

        //  Giờ bắt đầu (Lấy từ ShiftTemplate.StartTime)
        public TimeOnly? ShiftStartTime { get; set; }

        //  Giờ kết thúc (Lấy từ ShiftTemplate.EndTime)
        public TimeOnly? ShiftEndTime { get; set; }

        public string? Note { get; set; }

        public decimal TotalWorkingHours { get; set; }
        // công
        public double WorkDay { get; set; }
    }
}
