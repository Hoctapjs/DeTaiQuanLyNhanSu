//using DeTaiNhanSu.Common;
//using DeTaiNhanSu.DbContextProject;
//using DeTaiNhanSu.Dtos.TrainingRecordDtoFol;
//using DeTaiNhanSu.Enums;
//using DeTaiNhanSu.Models;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;

//namespace DeTaiNhanSu.Controllers
//{
//    [ApiController]
//    [Route("api/[controller]")]
//    public class TrainingRecordController : ControllerBase
//    {
//        private readonly AppDbContext _db;

//        public TrainingRecordController(AppDbContext db)
//        {
//            _db = db;
//        }

//        [HttpGet]
//        [Authorize(Roles = "HR, Admin")]
//        public async Task<IActionResult> Search(
//            [FromQuery] Guid? employeeId,
//            [FromQuery] Guid? courseId,
//            [FromQuery] TrainingStatus? status,
//            [FromQuery] decimal? minScore,
//            [FromQuery] decimal? maxScore,
//            [FromQuery] int current = 1,
//            [FromQuery] int pageSize = 20,
//            [FromQuery] string? sort = "-StartDate",
//            CancellationToken ct = default)
//        {
//            try
//            {
//                if (current < 1)
//                {
//                    current = 1;
//                }

//                if (pageSize is < 1 or > 200)
//                {
//                    pageSize = 20;
//                }

//                var q = _db.TrainingRecords
//                    .AsNoTracking()
//                    .Include(x => x.Employee)
//                    .Include(x => x.Course)
//                    .Include(x => x.EvaluatedByUser)
//                    .AsQueryable();

//                if (employeeId.HasValue)
//                {
//                    q = q.Where(x => x.EmployeeId == employeeId);
//                }

//                if (courseId.HasValue)
//                {
//                    q = q.Where(x => x.CourseId == courseId);
//                }

//                if (status.HasValue)
//                {
//                    q = q.Where(x => x.Status == status);
//                }

//                if (minScore.HasValue)
//                {
//                    q = q.Where(x => x.Score >= minScore);
//                }

//                if (maxScore.HasValue)
//                {
//                    q = q.Where(x => x.Score <= maxScore);
//                }

//                q = sort switch
//                {
//                    "StartDate" => q.OrderBy(x => x.StartDate),
//                    "-StartDate" => q.OrderByDescending(x => x.StartDate),
//                    "Score" => q.OrderBy(x => x.Score),
//                    "-Score" => q.OrderByDescending(x => x.Score),
//                    "Status" => q.OrderBy(x => x.Status),
//                    "-Status" => q.OrderByDescending(x => x.Status),
//                    _ => q.OrderByDescending(x => x.StartDate)
//                };

//                var total = await q.CountAsync(ct);

//                var result = await q
//                    .Skip((current - 1) * pageSize)
//                    .Take(pageSize)
//                    .Select(x => new TrainingRecordDto
//                    {
//                        Id = x.Id,
//                        EmployeeId = x.EmployeeId,
//                        EmployeeCode = x.Employee.Code,
//                        EmployeeName = x.Employee.FullName,
//                        CourseId = x.CourseId,
//                        CourseName = x.Course.Name,
//                        StartDate = x.StartDate,
//                        EndDate = x.EndDate,
//                        Score = x.Score,
//                        Status = x.Status,
//                        EvaluatedBy = x.EvaluatedBy,
//                        EvaluatedByUserName = x.EvaluatedByUser != null ? x.EvaluatedByUser.UserName : null,
//                        EvaluationNote = x.EvaluationNote
//                    })
//                    .ToListAsync(ct);

//                var meta = new
//                {
//                    current,
//                    pageSize,
//                    pages = (int)Math.Ceiling(total / (double)pageSize),
//                    total
//                };

//                return this.OKSingle(new { meta, result }, total > 0 ? $"Tìm thấy hồ sơ đào tạo." : "Không có kết quả.");
//            }
//            catch
//            {
//                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi tìm kiếm hồ sơ đào tạo.");
//            }
//        }

//        [HttpGet("{id:guid}")]
//        [Authorize(Roles = "HR, Admin")]
//        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
//        {
//            try
//            {
//                var x = await _db.TrainingRecords
//                    .AsNoTracking()
//                    .Include(r => r.Employee)
//                    .Include(r => r.Course)
//                    .Include(r => r.EvaluatedByUser)
//                    .FirstOrDefaultAsync(r => r.Id == id, ct);

//                if (x is null)
//                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy hồ sơ đào tạo.");

//                var dto = new TrainingRecordDto
//                {
//                    Id = x.Id,
//                    EmployeeId = x.EmployeeId,
//                    EmployeeCode = x.Employee.Code,
//                    EmployeeName = x.Employee.FullName,
//                    CourseId = x.CourseId,
//                    CourseName = x.Course.Name,
//                    StartDate = x.StartDate,
//                    EndDate = x.EndDate,
//                    Score = x.Score,
//                    Status = x.Status,
//                    EvaluatedBy = x.EvaluatedBy,
//                    EvaluatedByUserName = x.EvaluatedByUser?.UserName,
//                    EvaluationNote = x.EvaluationNote
//                };

//                return this.OKSingle(dto, "Lấy thông tin hồ sơ đào tạo thành công.");
//            }
//            catch
//            {
//                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi lấy hồ sơ đào tạo.");
//            }
//        }

//        [HttpPost]
//        [Authorize(Roles = "HR, Admin")]
//        public async Task<IActionResult> Create([FromBody] CreateTrainingRecordRequest req, CancellationToken ct)
//        {
//            try
//            {
//                if (!ModelState.IsValid)
//                    return this.FAIL(StatusCodes.Status400BadRequest, "Dữ liệu không hợp lệ.");

//                if (!await _db.Employees.AnyAsync(e => e.Id == req.EmployeeId, ct))
//                    return this.FAIL(StatusCodes.Status404NotFound, "Nhân viên không tồn tại.");

//                if (!await _db.Courses.AnyAsync(c => c.Id == req.CourseId, ct))
//                    return this.FAIL(StatusCodes.Status404NotFound, "Khóa học không tồn tại.");

//                if (req.EvaluatedBy is not null &&
//                    !await _db.Users.AnyAsync(u => u.Id == req.EvaluatedBy, ct))
//                    return this.FAIL(StatusCodes.Status404NotFound, "Người đánh giá không tồn tại.");

//                var tr = new TrainingRecord
//                {
//                    Id = Guid.NewGuid(),
//                    EmployeeId = req.EmployeeId,
//                    CourseId = req.CourseId,
//                    StartDate = req.StartDate,
//                    EndDate = req.EndDate,
//                    Score = req.Score,
//                    Status = req.Status ?? TrainingStatus.in_progress,
//                    EvaluatedBy = req.EvaluatedBy,
//                    EvaluationNote = req.EvaluationNote
//                };

//                _db.TrainingRecords.Add(tr);
//                await _db.SaveChangesAsync(ct);

//                return StatusCode(StatusCodes.Status201Created, new
//                {
//                    statusCode = StatusCodes.Status201Created,
//                    message = "Tạo hồ sơ đào tạo thành công.",
//                    data = new { result = tr },
//                    success = true
//                });
//            }
//            catch
//            {
//                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi không xác định khi tạo hồ sơ đào tạo.");
//            }
//        }

//        [HttpPut("{id:guid}")]
//        [Authorize(Roles = "HR, Admin")]
//        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTrainingRecordRequest req, CancellationToken ct)
//        {
//            try
//            {
//                var tr = await _db.TrainingRecords.FirstOrDefaultAsync(x => x.Id == id, ct);
//                if (tr is null)
//                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy hồ sơ đào tạo.");

//                if (!ModelState.IsValid)
//                    return this.FAIL(StatusCodes.Status400BadRequest, "Dữ liệu không hợp lệ.");

//                if (req.StartDate.HasValue)
//                    tr.StartDate = req.StartDate;

//                if (req.EndDate.HasValue)
//                    tr.EndDate = req.EndDate;

//                if (req.Score.HasValue)
//                    tr.Score = req.Score;

//                if (req.Status.HasValue)
//                    tr.Status = req.Status.Value;

//                if (req.EvaluatedBy.HasValue)
//                {
//                    if (!await _db.Users.AnyAsync(u => u.Id == req.EvaluatedBy, ct))
//                        return this.FAIL(StatusCodes.Status404NotFound, "Người đánh giá không tồn tại.");

//                    tr.EvaluatedBy = req.EvaluatedBy;
//                }

//                if (req.EvaluationNote != null)
//                    tr.EvaluationNote = string.IsNullOrWhiteSpace(req.EvaluationNote)
//                        ? null : req.EvaluationNote.Trim();

//                await _db.SaveChangesAsync(ct);
//                return this.OK(message: "Cập nhật hồ sơ đào tạo thành công.");
//            }
//            catch
//            {
//                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi cập nhật hồ sơ đào tạo.");
//            }
//        }

//        [HttpDelete("{id:guid}")]
//        [Authorize(Roles = "HR, Admin")]
//        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
//        {
//            try
//            {
//                var tr = await _db.TrainingRecords.FirstOrDefaultAsync(x => x.Id == id, ct);
//                if (tr is null)
//                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy hồ sơ đào tạo.");

//                _db.TrainingRecords.Remove(tr);
//                await _db.SaveChangesAsync(ct);

//                return this.OK(message: "Xóa hồ sơ đào tạo thành công.");
//            }
//            catch (DbUpdateException)
//            {
//                return this.FAIL(StatusCodes.Status409Conflict, "Không thể xóa do đang được tham chiếu.");
//            }
//            catch
//            {
//                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi không xác định khi xóa hồ sơ đào tạo.");
//            }
//        }
//    }
//}

// new ver 02 11

using DeTaiNhanSu.Common;
using DeTaiNhanSu.DbContextProject;
using DeTaiNhanSu.Dtos.TrainingRecordDtoFol;
using DeTaiNhanSu.Enums;
using DeTaiNhanSu.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeTaiNhanSu.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TrainingRecordController : ControllerBase
    {
        private readonly AppDbContext _db;
        public TrainingRecordController(AppDbContext db) => _db = db;

        [HttpGet]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> Search(
            [FromQuery] Guid? employeeId,
            [FromQuery] Guid? courseId,
            [FromQuery] TrainingStatus? status,
            [FromQuery] decimal? minScore,
            [FromQuery] decimal? maxScore,
            [FromQuery] int current = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? sort = "-Score",
            CancellationToken ct = default)
        {
            try
            {
                if (current < 1) current = 1;
                if (pageSize is < 1 or > 200) pageSize = 20;

                var q = _db.TrainingRecords
                    .AsNoTracking()
                    .Include(x => x.Employee)
                    .Include(x => x.Course)
                    .Include(x => x.EvaluatedByUser)
                    .AsQueryable();

                if (employeeId.HasValue)
                    q = q.Where(x => x.EmployeeId == employeeId.Value);

                if (courseId.HasValue)
                    q = q.Where(x => x.CourseId == courseId.Value);

                if (status.HasValue)
                    q = q.Where(x => x.Status == status.Value);

                if (minScore.HasValue)
                    q = q.Where(x => x.Score >= minScore.Value);

                if (maxScore.HasValue)
                    q = q.Where(x => x.Score <= maxScore.Value);

                q = sort?.Trim() switch
                {
                    "Score" => q.OrderBy(x => x.Score),
                    "-Score" => q.OrderByDescending(x => x.Score),
                    "Status" => q.OrderBy(x => x.Status),
                    "-Status" => q.OrderByDescending(x => x.Status),
                    "EmployeeName" => q.OrderBy(x => x.Employee.FullName),
                    "-EmployeeName" => q.OrderByDescending(x => x.Employee.FullName),
                    "CourseName" => q.OrderBy(x => x.Course.Name),
                    "-CourseName" => q.OrderByDescending(x => x.Course.Name),
                    _ => q.OrderByDescending(x => x.Score).ThenByDescending(x => x.Id)
                };

                var total = await q.CountAsync(ct);

                var result = await q
                    .Skip((current - 1) * pageSize)
                    .Take(pageSize)
                    .Select(x => new TrainingRecordDto
                    {
                        Id = x.Id,
                        EmployeeId = x.EmployeeId,
                        EmployeeCode = x.Employee.Code,
                        EmployeeName = x.Employee.FullName,
                        CourseId = x.CourseId,
                        CourseName = x.Course.Name,
                        Score = x.Score,
                        Status = x.Status,
                        EvaluatedBy = x.EvaluatedBy,
                        EvaluatedByUserName = x.EvaluatedByUser != null ? x.EvaluatedByUser.UserName : null,
                        EvaluationNote = x.EvaluationNote
                    })
                    .ToListAsync(ct);

                var meta = new
                {
                    current,
                    pageSize,
                    pages = (int)Math.Ceiling(total / (double)pageSize),
                    total
                };

                return this.OKSingle(new { meta, result }, total > 0 ? "Tìm thấy hồ sơ đào tạo." : "Không có kết quả.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi tìm kiếm hồ sơ đào tạo.");
            }
        }

        [HttpGet("{id:guid}")]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            try
            {
                var x = await _db.TrainingRecords
                    .AsNoTracking()
                    .Include(r => r.Employee)
                    .Include(r => r.Course)
                    .Include(r => r.EvaluatedByUser)
                    .FirstOrDefaultAsync(r => r.Id == id, ct);

                if (x is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy hồ sơ đào tạo.");

                var dto = new TrainingRecordDto
                {
                    Id = x.Id,
                    EmployeeId = x.EmployeeId,
                    EmployeeCode = x.Employee.Code,
                    EmployeeName = x.Employee.FullName,
                    CourseId = x.CourseId,
                    CourseName = x.Course.Name,
                    Score = x.Score,
                    Status = x.Status,
                    EvaluatedBy = x.EvaluatedBy,
                    EvaluatedByUserName = x.EvaluatedByUser?.UserName,
                    EvaluationNote = x.EvaluationNote
                };

                return this.OKSingle(dto, "Lấy thông tin hồ sơ đào tạo thành công.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi lấy hồ sơ đào tạo.");
            }
        }

        //[HttpPost]
        //[Authorize(Roles = "HR, Admin")]
        //public async Task<IActionResult> Create([FromBody] CreateTrainingRecordRequest req, CancellationToken ct)
        //{
        //    try
        //    {
        //        if (!ModelState.IsValid)
        //            return this.FAIL(StatusCodes.Status400BadRequest, "Dữ liệu không hợp lệ.");

        //        if (!await _db.Employees.AnyAsync(e => e.Id == req.EmployeeId, ct))
        //            return this.FAIL(StatusCodes.Status404NotFound, "Nhân viên không tồn tại.");

        //        if (!await _db.Courses.AnyAsync(c => c.Id == req.CourseId, ct))
        //            return this.FAIL(StatusCodes.Status404NotFound, "Khóa học không tồn tại.");

        //        if (req.EvaluatedBy is not null &&
        //            !await _db.Users.AnyAsync(u => u.Id == req.EvaluatedBy, ct))
        //            return this.FAIL(StatusCodes.Status404NotFound, "Người đánh giá không tồn tại.");

        //        if (req.Score is < 0 or > 100)
        //            return this.FAIL(StatusCodes.Status400BadRequest, "Score phải trong khoảng 0..100.");

        //        var tr = new TrainingRecord
        //        {
        //            Id = Guid.NewGuid(),
        //            EmployeeId = req.EmployeeId,
        //            CourseId = req.CourseId,
        //            Score = req.Score,
        //            Status = req.Status ?? TrainingStatus.in_progress,
        //            EvaluatedBy = req.EvaluatedBy,
        //            EvaluationNote = string.IsNullOrWhiteSpace(req.EvaluationNote) ? null : req.EvaluationNote.Trim()
        //        };

        //        _db.TrainingRecords.Add(tr);
        //        await _db.SaveChangesAsync(ct);

        //        return StatusCode(StatusCodes.Status201Created, new
        //        {
        //            statusCode = StatusCodes.Status201Created,
        //            message = "Tạo hồ sơ đào tạo thành công.",
        //            data = new { result = new { tr.Id } },
        //            success = true
        //        });
        //    }
        //    catch
        //    {
        //        return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi không xác định khi tạo hồ sơ đào tạo.");
        //    }
        //}

        //[HttpPost]
        //[Authorize(Roles = "HR, Admin")]
        //public async Task<IActionResult> Create([FromBody] CreateTrainingRecordRequest req, CancellationToken ct)
        //{
        //    try
        //    {
        //        if (!ModelState.IsValid)
        //            return this.FAIL(StatusCodes.Status400BadRequest, "Dữ liệu không hợp lệ.");

        //        // Tồn tại các thực thể?
        //        var employeeExists = await _db.Employees.AnyAsync(e => e.Id == req.EmployeeId, ct);
        //        if (!employeeExists) return this.FAIL(StatusCodes.Status404NotFound, "Nhân viên không tồn tại.");

        //        var course = await _db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == req.CourseId, ct);
        //        if (course is null) return this.FAIL(StatusCodes.Status404NotFound, "Khóa học không tồn tại.");

        //        if (req.EvaluatedBy is not null &&
        //            !await _db.Users.AnyAsync(u => u.Id == req.EvaluatedBy, ct))
        //            return this.FAIL(StatusCodes.Status404NotFound, "Người đánh giá không tồn tại.");

        //        // ---- TÍNH ĐIỂM TỰ ĐỘNG ----
        //        var totalQuestions = await _db.CourseQuestions
        //            .AsNoTracking()
        //            .CountAsync(q => q.CourseId == req.CourseId, ct);

        //        // Nếu course chưa có câu hỏi → score = 0, in_progress (hoặc bạn có thể cho completed)
        //        int answered = 0, correct = 0;
        //        if (totalQuestions > 0)
        //        {
        //            answered = await _db.CourseResults
        //                .AsNoTracking()
        //                .CountAsync(r => r.EmployeeId == req.EmployeeId && r.CourseId == req.CourseId, ct);

        //            correct = await _db.CourseResults
        //                .AsNoTracking()
        //                .CountAsync(r => r.EmployeeId == req.EmployeeId && r.CourseId == req.CourseId && r.IsCorrect, ct);
        //        }

        //        decimal scorePercent = 0m;
        //        if (totalQuestions > 0)
        //            scorePercent = Math.Round((decimal)correct / totalQuestions * 100m, 2);

        //        // Quy tắc Status
        //        var status = TrainingStatus.in_progress;
        //        if (totalQuestions > 0 && answered >= totalQuestions)
        //        {
        //            status = (scorePercent >= course.PassThreshold)
        //                ? TrainingStatus.completed    // Đạt
        //                : TrainingStatus.failed;      // Không đạt
        //        }

        //        var tr = new TrainingRecord
        //        {
        //            Id = Guid.NewGuid(),
        //            EmployeeId = req.EmployeeId,
        //            CourseId = req.CourseId,
        //            Score = scorePercent, // <- tự tính, không lấy từ request
        //            Status = req.Status ?? status,   // cho phép override nếu muốn, hoặc dùng luôn 'status'
        //            EvaluatedBy = req.EvaluatedBy,
        //            EvaluationNote = string.IsNullOrWhiteSpace(req.EvaluationNote) ? null : req.EvaluationNote.Trim()
        //        };

        //        _db.TrainingRecords.Add(tr);
        //        await _db.SaveChangesAsync(ct);

        //        // trả về theo schema của bạn
        //        return StatusCode(StatusCodes.Status201Created, new
        //        {
        //            statusCode = StatusCodes.Status201Created,
        //            message = "Tạo hồ sơ đào tạo thành công (điểm được tính tự động).",
        //            data = new
        //            {
        //                result = new
        //                {
        //                    tr.Id,
        //                    tr.EmployeeId,
        //                    tr.CourseId,
        //                    tr.Score,
        //                    tr.Status,
        //                    totalQuestions,
        //                    answered,
        //                    correct,
        //                    passThreshold = course.PassThreshold
        //                }
        //            },
        //            success = true
        //        });
        //    }
        //    catch
        //    {
        //        return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi không xác định khi tạo hồ sơ đào tạo.");
        //    }
        //}

        [HttpPost]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> Create([FromBody] CreateTrainingRecordRequest req, CancellationToken ct)
        {
            try
            {
                if (!ModelState.IsValid)
                    return this.FAIL(StatusCodes.Status400BadRequest, "Dữ liệu không hợp lệ.");

                var employeeExists = await _db.Employees.AnyAsync(e => e.Id == req.EmployeeId, ct);
                if (!employeeExists) return this.FAIL(StatusCodes.Status404NotFound, "Nhân viên không tồn tại.");

                var course = await _db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == req.CourseId, ct);
                if (course is null) return this.FAIL(StatusCodes.Status404NotFound, "Khóa học không tồn tại.");

                if (req.EvaluatedBy is not null &&
                    !await _db.Users.AnyAsync(u => u.Id == req.EvaluatedBy, ct))
                    return this.FAIL(StatusCodes.Status404NotFound, "Người đánh giá không tồn tại.");

                // --- TÍNH ĐIỂM ---
                var totalQuestions = await _db.CourseQuestions
                    .AsNoTracking()
                    .CountAsync(q => q.CourseId == req.CourseId, ct);

                var answered = (totalQuestions == 0) ? 0
                    : await _db.CourseResults.AsNoTracking()
                        .CountAsync(r => r.EmployeeId == req.EmployeeId && r.CourseId == req.CourseId, ct);

                var correct = (totalQuestions == 0) ? 0
                    : await _db.CourseResults.AsNoTracking()
                        .CountAsync(r => r.EmployeeId == req.EmployeeId && r.CourseId == req.CourseId && r.IsCorrect, ct);

                decimal scorePercent = 0m;
                if (totalQuestions > 0)
                    scorePercent = Math.Round((decimal)correct / totalQuestions * 100m, 2);

                // --- QUY TẮC STATUS ---
                TrainingStatus status;
                if (totalQuestions == 0)
                {
                    // Không có câu hỏi → chưa thể hoàn thành
                    status = TrainingStatus.not_completed;
                }
                else if (answered < totalQuestions)
                {
                    status = TrainingStatus.in_progress;
                }
                else
                {
                    status = (scorePercent >= course.PassThreshold)
                        ? TrainingStatus.completed
                        : TrainingStatus.failed;
                }

                var tr = new TrainingRecord
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = req.EmployeeId,
                    CourseId = req.CourseId,
                    Score = scorePercent,
                    Status = status,
                    EvaluatedBy = req.EvaluatedBy,
                    EvaluationNote = string.IsNullOrWhiteSpace(req.EvaluationNote) ? null : req.EvaluationNote.Trim()
                };

                _db.TrainingRecords.Add(tr);
                await _db.SaveChangesAsync(ct);

                return StatusCode(StatusCodes.Status201Created, new
                {
                    statusCode = StatusCodes.Status201Created,
                    message = "Tạo hồ sơ đào tạo thành công (điểm tính tự động).",
                    data = new
                    {
                        result = new
                        {
                            tr.Id,
                            tr.EmployeeId,
                            tr.CourseId,
                            score = tr.Score,
                            status = tr.Status.ToString(),
                            totalQuestions,
                            answered,
                            correct,
                            passThreshold = course.PassThreshold,
                            passed = (status == TrainingStatus.completed)
                        }
                    },
                    success = true
                });
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi không xác định khi tạo hồ sơ đào tạo.");
            }
        }



        [HttpPut("{id:guid}")]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTrainingRecordRequest req, CancellationToken ct)
        {
            try
            {
                var tr = await _db.TrainingRecords.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (tr is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy hồ sơ đào tạo.");

                if (!ModelState.IsValid)
                    return this.FAIL(StatusCodes.Status400BadRequest, "Dữ liệu không hợp lệ.");

                if (req.Score.HasValue)
                {
                    var sc = req.Score.Value;
                    if (sc is < 0 or > 100)
                        return this.FAIL(StatusCodes.Status400BadRequest, "Score phải trong khoảng 0..100.");
                    tr.Score = sc;
                }

                if (req.Status.HasValue)
                    tr.Status = req.Status.Value;

                if (req.EvaluatedBy.HasValue)
                {
                    if (!await _db.Users.AnyAsync(u => u.Id == req.EvaluatedBy, ct))
                        return this.FAIL(StatusCodes.Status404NotFound, "Người đánh giá không tồn tại.");
                    tr.EvaluatedBy = req.EvaluatedBy.Value;
                }

                if (req.EvaluationNote != null)
                    tr.EvaluationNote = string.IsNullOrWhiteSpace(req.EvaluationNote) ? null : req.EvaluationNote.Trim();

                await _db.SaveChangesAsync(ct);
                return this.OK(message: "Cập nhật hồ sơ đào tạo thành công.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi cập nhật hồ sơ đào tạo.");
            }
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            try
            {
                var tr = await _db.TrainingRecords.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (tr is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy hồ sơ đào tạo.");

                _db.TrainingRecords.Remove(tr);
                await _db.SaveChangesAsync(ct);

                return this.OK(message: "Xóa hồ sơ đào tạo thành công.");
            }
            catch (DbUpdateException)
            {
                return this.FAIL(StatusCodes.Status409Conflict, "Không thể xóa do đang được tham chiếu.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi không xác định khi xóa hồ sơ đào tạo.");
            }
        }
    }
}

