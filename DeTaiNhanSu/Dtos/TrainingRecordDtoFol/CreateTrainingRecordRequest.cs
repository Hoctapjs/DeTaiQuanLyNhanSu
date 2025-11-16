using System.ComponentModel.DataAnnotations;
using DeTaiNhanSu.Enums;

namespace DeTaiNhanSu.Dtos.TrainingRecordDtoFol
{
    public class CreateTrainingRecordRequest
    {
        //[Required]
        //public Guid EmployeeId { get; set; }

        //[Required]
        //public Guid CourseId { get; set; }

        //public DateOnly? StartDate { get; set; }
        //public DateTime? EndDate { get; set; }

        //[Range(0, 10)]
        //public decimal? Score { get; set; }

        //public TrainingStatus? Status { get; set; }

        //public Guid? EvaluatedBy { get; set; }

        //[MaxLength(1000)]
        //public string? EvaluationNote { get; set; }

        public Guid EmployeeId { get; set; }
        public Guid CourseId { get; set; }
        public decimal? Score { get; set; } // 0..100
        public TrainingStatus? Status { get; set; } // default in_progress
        public Guid? EvaluatedBy { get; set; }
        public string? EvaluationNote { get; set; }
    }
}
