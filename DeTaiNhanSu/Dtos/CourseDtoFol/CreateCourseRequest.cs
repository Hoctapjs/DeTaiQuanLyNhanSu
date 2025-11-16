using System.ComponentModel.DataAnnotations;

namespace DeTaiNhanSu.Dtos.CourseDtoFol
{
    public class CreateCourseRequest
    {
        //[Required]
        //[MaxLength(200)]
        //public string Name { get; set; } = default!;
        //[MaxLength(200)]
        //public string? Provider { get; set; }
        //[Range(1, 1000)]
        //public int? Hours { get; set; }

        public string Name { get; set; } = default!;
        public string? ClassCode { get; set; } = default!;
        public int? PassThreshold { get; set; }
    }
}
