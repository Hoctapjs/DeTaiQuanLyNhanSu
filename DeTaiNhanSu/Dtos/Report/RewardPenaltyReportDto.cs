using System.ComponentModel.DataAnnotations;

namespace DeTaiNhanSu.Dtos.Report
{
    public class RewardPenaltyReportDto
    {
        [Display(Name = "Ngày Quyết Định")]
        public string DecidedAt { get; set; }

        [Display(Name = "Mã NV")]
        public string EmployeeCode { get; set; }

        [Display(Name = "Họ Tên")]
        public string EmployeeName { get; set; }

        [Display(Name = "Phòng Ban")]
        public string Department { get; set; }

        [Display(Name = "Loại")]
        public string Kind { get; set; } // "Khen thưởng" hoặc "Kỷ luật"

        [Display(Name = "Hình Thức")]
        public string TypeName { get; set; } // Ví dụ: "Đi trễ", "Xuất sắc"...

        [Display(Name = "Số Tiền")]
        public decimal Amount { get; set; }

        [Display(Name = "Lý Do / Ghi Chú")]
        public string Reason { get; set; }
    }
}
