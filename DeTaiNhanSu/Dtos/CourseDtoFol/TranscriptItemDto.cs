using System.ComponentModel.DataAnnotations;

namespace DeTaiNhanSu.Dtos.CourseDtoFol
{
    public class TranscriptItemDto
    {
        [Display(Name = "Tên Khóa Học")]
        public string CourseName { get; set; }

        [Display(Name = "Mã Lớp")]
        public string ClassCode { get; set; }

        [Display(Name = "Ngày Kết Thúc")]
        public DateOnly? EndDate { get; set; }

        [Display(Name = "Điểm Số")]
        public decimal? Score { get; set; }

        [Display(Name = "Trạng Thái")]
        public string Status { get; set; }

        [Display(Name = "Đánh Giá Bởi")]
        public string EvaluatedBy { get; set; }
    }
}
