using DeTaiNhanSu.Enums;

namespace DeTaiNhanSu.Dtos.TrainingRecordDtoFol
{
    public class TrainingRecordDto
    {
        //public Guid Id { get; set; }
        //public Guid EmployeeId { get; set; }
        //public string EmployeeCode { get; set; } = default!;
        //public string EmployeeName { get; set; } = default!;
        //public Guid CourseId { get; set; }
        //public string CourseName { get; set; } = default!;
        //public DateOnly? StartDate { get; set; }
        //public DateTime? EndDate { get; set; }
        //public decimal? Score { get; set; }
        //public TrainingStatus? Status { get; set; }
        //public Guid? EvaluatedBy { get; set; }
        //public string? EvaluatedByUserName { get; set; }
        //public string? EvaluationNote { get; set; }

        public Guid Id { get; set; }
        public Guid EmployeeId { get; set; }
        public string EmployeeCode { get; set; } = default!;
        public string EmployeeName { get; set; } = default!;
        public Guid CourseId { get; set; }
        public string CourseName { get; set; } = default!;
        public decimal? Score { get; set; }
        public TrainingStatus Status { get; set; }
        public Guid? EvaluatedBy { get; set; }
        public string? EvaluatedByUserName { get; set; }
        public string? EvaluationNote { get; set; }
    }
}
