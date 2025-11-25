//using DeTaiNhanSu.Enums;

//namespace DeTaiNhanSu.Models
//{
//    public class TrainingRecord
//    {
//        public Guid Id { get; set; }
//        public Guid EmployeeId { get; set; }
//        public Guid CourseId { get; set; }
//        //public DateOnly? StartDate { get; set; }
//        //public DateTime? EndDate { get; set; }
//        public decimal? Score { get; set; }
//        public TrainingStatus Status { get; set; } = TrainingStatus.in_progress;
//        public Guid? EvaluatedBy { get; set; }
//        public string? EvaluationNote { get; set; }

//        public Employee Employee { get; set; } = default!;
//        public Course Course { get; set; } = default!;
//        public User? EvaluatedByUser { get; set; }
//    }
//}

using System.ComponentModel.DataAnnotations.Schema;
using DeTaiNhanSu.Enums;

namespace DeTaiNhanSu.Models
{
    public class TrainingRecord
    {
        public Guid Id { get; set; }
        public Guid EmployeeId { get; set; }
        public Guid CourseId { get; set; }

        public decimal? Score { get; set; }
        public TrainingStatus Status { get; set; } = TrainingStatus.in_progress;
        public Guid? EvaluatedBy { get; set; }
        public string? EvaluationNote { get; set; }

        // --- CẤU HÌNH KHÓA NGOẠI ---

        [ForeignKey("EmployeeId")]
        public Employee Employee { get; set; } = default!;

        // === SỬA Ở ĐÂY ===
        [ForeignKey("CourseId")]
        [InverseProperty("TrainingRecords")] // <--- THÊM DÒNG NÀY
        // Ý nghĩa: "Biến Course này trỏ ngược về danh sách 'TrainingRecords' bên bảng Course"
        public Course Course { get; set; } = default!;
        // =================

        [ForeignKey("EvaluatedBy")]
        public User? EvaluatedByUser { get; set; }
    }
}