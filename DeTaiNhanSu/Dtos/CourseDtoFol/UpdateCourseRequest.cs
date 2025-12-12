namespace DeTaiNhanSu.Dtos.CourseDtoFol
{
    public class UpdateCourseRequest
    {
        public string? Name { get; set; } = default!;
        public string? ClassCode { get; set; } = default!;
        public int? PassThreshold { get; set; }
    }
}
