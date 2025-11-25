using System.ComponentModel.DataAnnotations;

namespace DeTaiNhanSu.Dtos.CourseDtoFol
{
    public class CourseSummaryExcelDto
    {
        [Display(Name = "Tên Khóa Học")]
        public string CourseName { get; set; }

        [Display(Name = "Tổng Học Viên")]
        public int TotalParticipants { get; set; }

        [Display(Name = "Đạt (Completed)")]
        public int PassedCount { get; set; }

        [Display(Name = "Trượt (Failed)")]
        public int FailedCount { get; set; }

        [Display(Name = "Đang Học/Chưa Xong")]
        public int InProgressCount { get; set; }

        [Display(Name = "Điểm Trung Bình")]
        public decimal AverageScore { get; set; }
    }
}
