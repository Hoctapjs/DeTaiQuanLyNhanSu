using System.ComponentModel.DataAnnotations;

namespace DeTaiNhanSu.Dtos.Report
{
    public class SalaryReportRowDto
    {
        [Display(Name = "Mã NV")]
        public string EmployeeCode { get; set; }

        [Display(Name = "Họ Tên")]
        public string EmployeeName { get; set; }

        [Display(Name = "Phòng Ban")]
        public string Department { get; set; }

        [Display(Name = "Tổng Thu Nhập")]
        public decimal GrossSalary { get; set; }

        [Display(Name = "Tổng Khấu Trừ")]
        public decimal TotalDeductions { get; set; }

        [Display(Name = "Thực Lĩnh (Net)")]
        public decimal NetSalary { get; set; }

        // Dictionary để lưu các khoản mục động (Key: Tên khoản mục, Value: Số tiền)
        public Dictionary<string, decimal> DynamicItems { get; set; } = new();
    }
}
