namespace DeTaiNhanSu.Models
{
    public class ShiftTemplate
    {
        public Guid Id { get; set; }

        // Tên ca làm việc (Ví dụ: "Hành chính", "Ca Đêm")
        public string Name { get; set; } = default!;

        // Mã ca (Ví dụ: "HC", "SANG", "DEM" - Giúp phân biệt nhanh)
        public string Code { get; set; } = default!;

        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }

        // Thời gian nghỉ giữa ca (phút), có thể ảnh hưởng đến tổng giờ làm
        public int BreakDurationMinutes { get; set; }

        // Tổng số giờ làm việc thực tế (có thể tính toán)
        public decimal TotalWorkingHours { get; set; }

        // Ghi chú về quy tắc của ca này
        public string? Description { get; set; }
    }
}
