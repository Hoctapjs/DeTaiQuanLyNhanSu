using System.ComponentModel.DataAnnotations;

namespace DeTaiNhanSu.Dtos.WorkScheduleDtoFol
{
    public class BulkCreateWorkScheduleRequest
    {
        // --- 1. ĐỐI TƯỢNG ÁP DỤNG ---
        // Có thể truyền danh sách ID nhân viên cụ thể...
        public List<Guid>? EmployeeIds { get; set; }

        // ...HOẶC truyền ID phòng ban (sẽ lấy tất cả nhân viên active của phòng đó)
        public Guid? DepartmentId { get; set; }

        // --- 2. THỜI GIAN ---
        [Required]
        public DateOnly FromDate { get; set; }

        [Required]
        public DateOnly ToDate { get; set; }

        // Tùy chọn: Chỉ áp dụng cho những ngày cụ thể trong tuần
        // Ví dụ: [1, 2, 3, 4, 5] tương ứng T2-T6. (0 = Sunday, 1 = Monday...)
        public List<DayOfWeek>? DaysOfWeek { get; set; }

        // --- 3. CA LÀM VIỆC ---
        [Required]
        public Guid ShiftTemplateId { get; set; }

        public string? Note { get; set; }

        // Tùy chọn: Có ghi đè lịch cũ nếu đã tồn tại không?
        public bool Overwrite { get; set; } = false;
    }
}
