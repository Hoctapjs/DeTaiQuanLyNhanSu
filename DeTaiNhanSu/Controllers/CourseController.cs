//using System.Text.Json;
//using DeTaiNhanSu.Common;
//using DeTaiNhanSu.DbContextProject;
//using DeTaiNhanSu.Dtos.CourseDtoFol;
//using DeTaiNhanSu.Models;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
//using Org.BouncyCastle.Ocsp;

//namespace DeTaiNhanSu.Controllers
//{
//    [ApiController]
//    [Route("api/[controller]")]
//    public class CourseController : ControllerBase
//    {
//        private readonly AppDbContext _db;

//        public CourseController(AppDbContext db)
//        {
//            _db = db;
//        }

//        [HttpGet]
//       //[Authorize(Roles = "HR, Admin")]
//        public async Task<IActionResult> Search(
//            [FromQuery] string? q,
//            [FromQuery] int current = 1,
//            [FromQuery] int pageSize = 20,
//            [FromQuery] string? sort = "Name",
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

//                var query = _db.Courses.AsNoTracking().AsQueryable();

//                if (!string.IsNullOrWhiteSpace(q))
//                {
//                    q = q.Trim();
//                    query = query.Where(x => x.Name.Contains(q) || (x.Provider != null && x.Provider.Contains(q)));
//                }

//                query = sort?.Trim() switch
//                {
//                    "-Name" => query.OrderByDescending(x => x.Name),
//                    "Provider" => query.OrderBy(x => x.Provider),
//                    "-Provider" => query.OrderByDescending(x => x.Provider),
//                    "Hours" => query.OrderBy(x => x.Hours),
//                    "-Hours" => query.OrderByDescending(x => x.Hours),
//                    _ => query.OrderBy(x => x.Name)
//                };

//                var total = await query.CountAsync(ct);

//                var result = await query
//                    .Skip((current - 1) * pageSize)
//                    .Take(pageSize)
//                    .Select(x => new CourseDto
//                    {
//                        Id = x.Id,
//                        Name = x.Name,
//                        Provider = x.Provider,
//                        Hours = x.Hours
//                    }).ToListAsync();

//                var meta = new
//                {
//                    current,
//                    pageSize,
//                    pages = (int)Math.Ceiling(total / (double)pageSize),
//                    total
//                };

//                return this.OKSingle(new { meta, result }, total > 0 ? $"Tìm thấy {total} khóa học." : "Không có kết quả.");
//            }
//            catch (Exception)
//            {
//                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi tìm kiếm khóa học");
//            }
//        }

//        [HttpGet("{id:guid}")]
//       //[Authorize(Roles = "HR, Admin")]
//        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
//        {
//            try
//            {
//                var course = await _db.Courses.AsNoTracking()
//                    .FirstOrDefaultAsync(x => x.Id == id, ct);

//                if (course is null)
//                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy khóa học.");

//                var dto = new CourseDto
//                {
//                    Id = course.Id,
//                    Name = course.Name,
//                    Provider = course.Provider,
//                    Hours = course.Hours
//                };

//                return this.OKSingle(dto, "Lấy thông tin khóa học thành công.");
//            }
//            catch
//            {
//                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi lấy thông tin khóa học.");
//            }
//        }

//        [HttpPost]
//       //[Authorize(Roles = "HR, Admin")]
//        public async Task<IActionResult> Create([FromBody] CreateCourseRequest req, CancellationToken ct)
//        {
//            try
//            {
//                if (!ModelState.IsValid)
//                {
//                    return this.FAIL(StatusCodes.Status400BadRequest, "Dữ liệu không hợp lệ");
//                }

//                if (string.IsNullOrWhiteSpace(req.Name))
//                    return this.FAIL(StatusCodes.Status400BadRequest, "Tên khóa học là bắt buộc.");

//                if (await _db.Courses.AnyAsync(x => x.Name == req.Name.Trim(), ct))
//                    return this.FAIL(StatusCodes.Status409Conflict, "Tên khóa học đã tồn tại.");

//                var course = new Course
//                {
//                    Id = Guid.NewGuid(),
//                    Name = req.Name.Trim(),
//                    Provider = string.IsNullOrWhiteSpace(req.Provider) ? null : req.Provider.Trim(),
//                    Hours = req.Hours
//                };

//                _db.Courses.Add(course);
//                await _db.SaveChangesAsync(ct);

//                return StatusCode(StatusCodes.Status201Created, new
//                {
//                    statusCode = StatusCodes.Status201Created,
//                    message = "Tạo khóa học thành công.",
//                    data = new { result = course },
//                    success = true
//                });
//            }
//            catch (DbUpdateException)
//            {
//                return this.FAIL(StatusCodes.Status409Conflict, "Không thể tạo khóa học do xung đột dữ liệu.");
//            }
//            catch
//            {
//                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi không xác định khi tạo khóa học.");
//            }
//        }

//        [HttpPut("{id:guid}")]
//       //[Authorize(Roles = "HR, Admin")]
//        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCourseRequest req, CancellationToken ct)
//        {
//            try
//            {
//                var c = await _db.Courses.FirstOrDefaultAsync(x => x.Id == id, ct);

//                if (c is null)
//                {
//                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy khóa học.");
//                }

//                if (!ModelState.IsValid)
//                {
//                    return this.FAIL(StatusCodes.Status400BadRequest, "Không tìm thấy khóa học.");
//                }

//                if (!string.IsNullOrWhiteSpace(req.Name))
//                {
//                    var newName = req.Name.Trim();
//                    if (!string.Equals(c.Name, newName, StringComparison.OrdinalIgnoreCase) &&
//                        await _db.Courses.AnyAsync(x => x.Name == newName, ct))
//                        return this.FAIL(StatusCodes.Status409Conflict, "Tên khóa học đã tồn tại.");

//                    c.Name = newName;
//                }
//                if (req.Provider != null)
//                    c.Provider = string.IsNullOrWhiteSpace(req.Provider) ? null : req.Provider.Trim();

//                if (req.Hours.HasValue)
//                    c.Hours = req.Hours;

//                await _db.SaveChangesAsync(ct);

//                return this.OK(message: "Cập nhật khóa học thành công.");
//            }
//            catch (DbUpdateConcurrencyException)
//            {
//                return this.FAIL(StatusCodes.Status409Conflict, "Xung đột cập nhật: bản ghi đã thay đổi trước đó.");
//            }
//            catch
//            {
//                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi cập nhật khóa học.");
//            }
//        }

//        [HttpDelete("{id:guid}")]
//       //[Authorize(Roles = "HR, Admin")]
//        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
//        {
//            try
//            {
//                var c = await _db.Courses.FirstOrDefaultAsync(x => x.Id == id, ct);

//                if (c is null)
//                {
//                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy khóa học.");
//                }

//                _db.Courses.Remove(c);

//                await _db.SaveChangesAsync(ct);

//                return this.OK(message: "Xoá khóa học thành công.");

//            }
//            catch (DbUpdateException)
//            {
//                return this.FAIL(StatusCodes.Status409Conflict, "Không thể xoá do đang được tham chiếu bởi dữ liệu khác.");
//            }
//            catch
//            {
//                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi không xác định khi xoá khóa học.");
//            }
//        }
//    }
//}

// new ver 02 11

using DeTaiNhanSu.Common;
using DeTaiNhanSu.DbContextProject;
using DeTaiNhanSu.Dtos.CourseDtoFol;
using DeTaiNhanSu.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeTaiNhanSu.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CourseController : ControllerBase
    {
        private readonly AppDbContext _db;
        public CourseController(AppDbContext db) => _db = db;

        [HttpGet]
       //[Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> Search(
            [FromQuery] string? q,
            [FromQuery] int current = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? sort = "Name",
            CancellationToken ct = default)
        {
            try
            {
                if (current < 1) current = 1;
                if (pageSize is < 1 or > 200) pageSize = 20;

                var query = _db.Courses.AsNoTracking().AsQueryable();

                if (!string.IsNullOrWhiteSpace(q))
                {
                    q = q.Trim();
                    query = query.Where(x =>
                        x.Name.Contains(q) ||
                        x.ClassCode.Contains(q));
                }

                query = sort?.Trim() switch
                {
                    "-Name" => query.OrderByDescending(x => x.Name),
                    "ClassCode" => query.OrderBy(x => x.ClassCode),
                    "-ClassCode" => query.OrderByDescending(x => x.ClassCode),
                    "PassThreshold" => query.OrderBy(x => x.PassThreshold),
                    "-PassThreshold" => query.OrderByDescending(x => x.PassThreshold),
                    "CreatedAt" => query.OrderBy(x => x.CreatedAt),
                    "-CreatedAt" => query.OrderByDescending(x => x.CreatedAt),
                    _ => query.OrderBy(x => x.Name)
                };

                var total = await query.CountAsync(ct);

                var result = await query
                    .Skip((current - 1) * pageSize)
                    .Take(pageSize)
                    .Select(x => new CourseDto
                    {
                        Id = x.Id,
                        Name = x.Name,
                        ClassCode = x.ClassCode,
                        PassThreshold = x.PassThreshold,
                        CreatedAt = x.CreatedAt,
                        QuestionCount = x.Questions.Count
                    })
                    .ToListAsync(ct);

                var meta = new
                {
                    current,
                    pageSize,
                    pages = (int)Math.Ceiling(total / (double)pageSize),
                    total
                };

                return this.OKSingle(new { meta, result }, total > 0 ? $"Tìm thấy {total} khóa học." : "Không có kết quả.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi tìm kiếm khóa học.");
            }
        }

        [HttpGet("{id:guid}")]
       //[Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            try
            {
                var dto = await _db.Courses.AsNoTracking()
                    .Where(x => x.Id == id)
                    .Select(x => new CourseDto
                    {
                        Id = x.Id,
                        Name = x.Name,
                        ClassCode = x.ClassCode,
                        PassThreshold = x.PassThreshold,
                        CreatedAt = x.CreatedAt,
                        QuestionCount = x.Questions.Count
                    })
                    .FirstOrDefaultAsync(ct);

                if (dto is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy khóa học.");

                return this.OKSingle(dto, "Lấy thông tin khóa học thành công.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi lấy thông tin khóa học.");
            }
        }

        [HttpPost]
       //[Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> Create([FromBody] CreateCourseRequest req, CancellationToken ct)
        {
            try
            {
                if (!ModelState.IsValid)
                    return this.FAIL(StatusCodes.Status400BadRequest, "Dữ liệu không hợp lệ.");

                if (string.IsNullOrWhiteSpace(req.Name))
                    return this.FAIL(StatusCodes.Status400BadRequest, "Tên khóa học là bắt buộc.");

                if (string.IsNullOrWhiteSpace(req.ClassCode))
                    return this.FAIL(StatusCodes.Status400BadRequest, "Mã lớp (ClassCode) là bắt buộc.");

                var name = req.Name.Trim();
                var code = req.ClassCode.Trim();

                // ClassCode nên unique
                if (await _db.Courses.AnyAsync(x => x.ClassCode == code, ct))
                    return this.FAIL(StatusCodes.Status409Conflict, "ClassCode đã tồn tại.");

                var pass = req.PassThreshold ?? 70;
                if (pass is < 0 or > 100)
                    return this.FAIL(StatusCodes.Status400BadRequest, "PassThreshold phải trong khoảng 0..100.");

                var course = new Course
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    ClassCode = code,
                    PassThreshold = pass,
                    CreatedAt = DateTime.UtcNow
                };

                _db.Courses.Add(course);
                await _db.SaveChangesAsync(ct);

                var dto = new CourseDto
                {
                    Id = course.Id,
                    Name = course.Name,
                    ClassCode = course.ClassCode,
                    PassThreshold = course.PassThreshold,
                    CreatedAt = course.CreatedAt,
                    QuestionCount = 0
                };

                return StatusCode(StatusCodes.Status201Created, new
                {
                    statusCode = StatusCodes.Status201Created,
                    message = "Tạo khóa học thành công.",
                    data = new { result = dto },
                    success = true
                });
            }
            catch (DbUpdateException)
            {
                return this.FAIL(StatusCodes.Status409Conflict, "Không thể tạo khóa học do xung đột dữ liệu.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi không xác định khi tạo khóa học.");
            }
        }

        [HttpPut("{id:guid}")]
       //[Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCourseRequest req, CancellationToken ct)
        {
            try
            {
                var c = await _db.Courses.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (c is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy khóa học.");

                if (!ModelState.IsValid)
                    return this.FAIL(StatusCodes.Status400BadRequest, "Dữ liệu không hợp lệ.");

                if (!string.IsNullOrWhiteSpace(req.Name))
                {
                    var newName = req.Name.Trim();
                    c.Name = newName;
                }

                if (!string.IsNullOrWhiteSpace(req.ClassCode))
                {
                    var newCode = req.ClassCode.Trim();
                    if (!string.Equals(c.ClassCode, newCode, StringComparison.OrdinalIgnoreCase) &&
                        await _db.Courses.AnyAsync(x => x.ClassCode == newCode, ct))
                        return this.FAIL(StatusCodes.Status409Conflict, "ClassCode đã tồn tại.");

                    c.ClassCode = newCode;
                }

                if (req.PassThreshold.HasValue)
                {
                    var pass = req.PassThreshold.Value;
                    if (pass is < 0 or > 100)
                        return this.FAIL(StatusCodes.Status400BadRequest, "PassThreshold phải trong khoảng 0..100.");
                    c.PassThreshold = pass;
                }

                await _db.SaveChangesAsync(ct);
                return this.OK(message: "Cập nhật khóa học thành công.");
            }
            catch (DbUpdateConcurrencyException)
            {
                return this.FAIL(StatusCodes.Status409Conflict, "Xung đột cập nhật: bản ghi đã thay đổi trước đó.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi khi cập nhật khóa học.");
            }
        }

        [HttpDelete("{id:guid}")]
       //[Authorize(Roles = "HR, Admin")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            try
            {
                var c = await _db.Courses.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (c is null)
                    return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy khóa học.");

                _db.Courses.Remove(c);
                await _db.SaveChangesAsync(ct);

                return this.OK(message: "Xoá khóa học thành công.");
            }
            catch (DbUpdateException)
            {
                return this.FAIL(StatusCodes.Status409Conflict, "Không thể xoá do đang được tham chiếu bởi dữ liệu khác.");
            }
            catch
            {
                return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi không xác định khi xoá khóa học.");
            }
        }
    }
}
