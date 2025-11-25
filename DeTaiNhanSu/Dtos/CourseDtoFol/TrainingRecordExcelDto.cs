using System.ComponentModel.DataAnnotations;

namespace DeTaiNhanSu.Dtos.CourseDtoFol
{
    public class TrainingRecordExcelDto
    {
        [Display(Name = "Mã NV")]
        public string EmployeeCode { get; set; }

        [Display(Name = "Họ Tên")]
        public string EmployeeName { get; set; }

        [Display(Name = "Khóa Học")]
        public string CourseName { get; set; }

        [Display(Name = "Điểm Số")]
        public decimal? Score { get; set; }

        [Display(Name = "Trạng Thái")]
        public string Status { get; set; } // "Completed", "Failed"...

        [Display(Name = "Người Đánh Giá")]
        public string EvaluatedBy { get; set; }

        [Display(Name = "Ghi Chú")]
        public string Note { get; set; }
    }
}
