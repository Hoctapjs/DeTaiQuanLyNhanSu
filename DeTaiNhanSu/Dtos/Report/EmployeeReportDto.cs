using System.ComponentModel.DataAnnotations;

namespace DeTaiNhanSu.Dtos.Report
{
    public class EmployeeReportDto
    {
        [Display(Name = "Mã NV")]
        public string? EmployeeCode { get; set; }

        [Display(Name = "Họ Tên")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "Email")]
        public string? Email { get; set; }

        [Display(Name = "Phòng Ban")]
        public string? DepartmentName { get; set; }

        [Display(Name = "Chức Vụ")]
        public string? PositionName { get; set; } 

        [Display(Name = "Ngày Vào Làm")]
        public DateOnly? HireDate { get; set; }

        [Display(Name = "Trạng thái HĐ")]
        public string ContractStatus { get; set; } = string.Empty;
    }
}
