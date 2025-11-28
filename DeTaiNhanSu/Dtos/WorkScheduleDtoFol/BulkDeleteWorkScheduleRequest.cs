using System.ComponentModel.DataAnnotations;

namespace DeTaiNhanSu.Dtos.WorkScheduleDtoFol
{
    public class BulkDeleteWorkScheduleRequest
    {
        // Chọn danh sách nhân viên cụ thể
        public List<Guid>? EmployeeIds { get; set; }

        // HOẶC chọn theo phòng ban
        public Guid? DepartmentId { get; set; }

        [Required]
        public DateOnly FromDate { get; set; }

        [Required]
        public DateOnly ToDate { get; set; }
    }
}
