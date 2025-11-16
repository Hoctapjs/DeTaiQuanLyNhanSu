using System.Text.Json;
using DeTaiNhanSu.Common;
using DeTaiNhanSu.DbContextProject;
using DeTaiNhanSu.Dtos.CourseQuestionDtoFol;
using DeTaiNhanSu.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeTaiNhanSu.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CourseQuestionsController : ControllerBase
    {
        private readonly AppDbContext _db;
        public CourseQuestionsController(AppDbContext db) => _db = db;

        // endpoint mẫu
        // GET /api/coursequestions?courseId=&q=&current=&pageSize=&sort=Content|-Content
        [HttpGet]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> Search(
            [FromQuery] Guid? courseId,
            [FromQuery] string? q,
            [FromQuery] int current = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? sort = "Content",
            CancellationToken ct = default)
        {
            try
            {
                if (current < 1) current = 1;
                if (pageSize is < 1 or > 200) pageSize = 20;

                var query = _db.CourseQuestions
                    .AsNoTracking()
                    .Include(x => x.Course)
                    .AsQueryable();

                if (courseId.HasValue)
                    query = query.Where(x => x.CourseId == courseId);

                if (!string.IsNullOrWhiteSpace(q))
                {
                    q = q.Trim();
                    query = query.Where(x =>
                        x.Content.Contains(q) ||
                        x.A.Contains(q) || x.B.Contains(q) || x.C.Contains(q) || x.D.Contains(q));
                }

                query = sort?.Trim() switch
                {
                    "-Content" => query.OrderByDescending(x => x.Content),
                    "Course" => query.OrderBy(x => x.Course!.Name),
                    "-Course" => query.OrderByDescending(x => x.Course!.Name),
                    _ => query.OrderBy(x => x.Content)
                };

                var total = await query.CountAsync(ct);

                var result = await query
                    .Skip((current - 1) * pageSize)
                    .Take(pageSize)
                    .Select(x => new
                    {
                        x.Id,
                        x.CourseId,
                        CourseName = x.Course!.Name,
                        x.Content,
                        x.A,
                        x.B,
                        x.C,
                        x.D,
                        x.Correct
                    })
                    .ToListAsync(ct);

                var meta = new
                {
                    current,
                    pageSize,
                    pages = (int)Math.Ceiling(total / (double)pageSize),
                    total
                };

                return this.OKSingle(new { meta, result },
                    total > 0 ? $"Tìm thấy {total} câu hỏi." : "Không có kết quả.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi khi tìm kiếm câu hỏi.");
            }
        }

        // GET /api/coursequestions/{id}
        [HttpGet("{id:guid}")]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            try
            {
                var q = await _db.CourseQuestions
                    .AsNoTracking()
                    .Include(x => x.Course)
                    .FirstOrDefaultAsync(x => x.Id == id, ct);

                if (q is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy câu hỏi.");

                var dto = new
                {
                    q.Id,
                    q.CourseId,
                    CourseName = q.Course!.Name,
                    q.Content,
                    q.A,
                    q.B,
                    q.C,
                    q.D,
                    q.Correct
                };

                return this.OKSingle(dto, "Lấy thông tin câu hỏi thành công.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi khi lấy câu hỏi.");
            }
        }

        // POST /api/coursequestions
        [HttpPost]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> Create([FromBody] CreateCourseQuestionRequest req, CancellationToken ct)
        {
            try
            {
                if (!ModelState.IsValid)
                    return this.FAIL(StatusCodes.Status400BadRequest, "Dữ liệu không hợp lệ.");

                var courseExists = await _db.Courses.AnyAsync(c => c.Id == req.CourseId, ct);
                if (!courseExists) return this.FAIL(StatusCodes.Status404NotFound, "Khóa học không tồn tại.");

                var e = new CourseQuestion
                {
                    Id = Guid.NewGuid(),
                    CourseId = req.CourseId,
                    Content = req.Content.Trim(),
                    A = req.A.Trim(),
                    B = req.B.Trim(),
                    C = req.C.Trim(),
                    D = req.D.Trim(),
                    Correct = req.Correct.Trim().ToUpperInvariant()
                };

                _db.CourseQuestions.Add(e);
                await _db.SaveChangesAsync(ct);

                var dto = await _db.CourseQuestions.AsNoTracking()
                    .Include(x => x.Course)
                    .Where(x => x.Id == e.Id)
                    .Select(x => new CourseQuestionDto
                    {
                        Id = x.Id,
                        CourseId = x.CourseId,
                        CourseName = x.Course!.Name,
                        Content = x.Content,
                        A = x.A,
                        B = x.B,
                        C = x.C,
                        D = x.D,
                        Correct = x.Correct
                    })
                    .FirstAsync(ct);

                return StatusCode(StatusCodes.Status201Created, new
                {
                    statusCode = StatusCodes.Status201Created,
                    message = "Tạo câu hỏi thành công.",
                    data = new { result = dto },
                    success = true
                });
            }
            catch (DbUpdateException)
            {
                return this.FAIL(StatusCodes.Status409Conflict, "Không thể tạo câu hỏi do xung đột dữ liệu.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi không xác định khi tạo câu hỏi.");
            }
        }


        // PUT /api/coursequestions/{id}
        // Cập nhật từng phần qua JSON object
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCourseQuestionRequest req, CancellationToken ct)
        {
            try
            {
                var q = await _db.CourseQuestions.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (q is null) return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy câu hỏi.");

                if (!ModelState.IsValid)
                    return this.FAIL(StatusCodes.Status400BadRequest, "Dữ liệu không hợp lệ.");

                if (req.CourseId.HasValue)
                {
                    var ok = await _db.Courses.AnyAsync(c => c.Id == req.CourseId.Value, ct);
                    if (!ok) return this.FAIL(StatusCodes.Status404NotFound, "Khóa học không tồn tại.");
                    q.CourseId = req.CourseId.Value;
                }
                if (!string.IsNullOrWhiteSpace(req.Content)) q.Content = req.Content.Trim();
                if (req.A is not null) q.A = req.A.Trim();
                if (req.B is not null) q.B = req.B.Trim();
                if (req.C is not null) q.C = req.C.Trim();
                if (req.D is not null) q.D = req.D.Trim();
                if (!string.IsNullOrWhiteSpace(req.Correct)) q.Correct = req.Correct.Trim().ToUpperInvariant();

                await _db.SaveChangesAsync(ct);

                var dto = await _db.CourseQuestions.AsNoTracking()
                    .Include(x => x.Course)
                    .Where(x => x.Id == q.Id)
                    .Select(x => new CourseQuestionDto
                    {
                        Id = x.Id,
                        CourseId = x.CourseId,
                        CourseName = x.Course!.Name,
                        Content = x.Content,
                        A = x.A,
                        B = x.B,
                        C = x.C,
                        D = x.D,
                        Correct = x.Correct
                    })
                    .FirstAsync(ct);

                return StatusCode(StatusCodes.Status200OK, new
                {
                    statusCode = StatusCodes.Status200OK,
                    message = "Cập nhật câu hỏi thành công.",
                    data = new { result = dto },
                    success = true
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                return this.FAIL(StatusCodes.Status409Conflict, "Xung đột cập nhật: bản ghi đã thay đổi trước đó.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi khi cập nhật câu hỏi.");
            }
        }


        // DELETE /api/coursequestions/{id}
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            try
            {
                var q = await _db.CourseQuestions.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (q is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy câu hỏi.");

                // Chặn xóa nếu đã có kết quả làm bài (nếu bạn muốn an toàn dữ liệu)
                var hasResults = await _db.CourseResults.AnyAsync(r => r.QuestionId == id, ct);
                if (hasResults)
                    return this.FAIL(StatusCodes.Status409Conflict, "Không thể xóa câu hỏi vì đã có bài làm liên quan.");

                _db.CourseQuestions.Remove(q);
                await _db.SaveChangesAsync(ct);

                return this.OK(message: "Xóa câu hỏi thành công.");
            }
            catch (DbUpdateException)
            {
                return this.FAIL(StatusCodes.Status409Conflict, "Không thể xóa do ràng buộc dữ liệu.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi khi xóa câu hỏi.");
            }
        }
    }
}
