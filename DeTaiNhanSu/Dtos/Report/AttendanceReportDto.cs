using System.ComponentModel.DataAnnotations;

namespace DeTaiNhanSu.Dtos.Report
{
    public class AttendanceReportDto
    {
        [Display(Name = "Ngày")]
        public string Date { get; set; } // Dạng chuỗi "dd/MM/yyyy"

        [Display(Name = "Mã NV")]
        public string EmployeeCode { get; set; }

        [Display(Name = "Họ Tên")]
        public string EmployeeName { get; set; }

        [Display(Name = "Phòng Ban")]
        public string Department { get; set; }

        [Display(Name = "Giờ Vào")]
        public string CheckIn { get; set; }

        [Display(Name = "Giờ Ra")]
        public string CheckOut { get; set; }

        [Display(Name = "Trạng Thái")]
        public string Status { get; set; }

        [Display(Name = "Ghi Chú")]
        public string Note { get; set; }
    }
}
