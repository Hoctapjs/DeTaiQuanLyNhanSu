using System.Linq;
using DeTaiNhanSu.Common;
using DeTaiNhanSu.DbContextProject;
using DeTaiNhanSu.Dtos.CourseResultDtoFol;
using DeTaiNhanSu.Enums;
using DeTaiNhanSu.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static DeTaiNhanSu.Dtos.CourseResultDtoFol.BulkSubmitCourseAnswersRequest;

namespace DeTaiNhanSu.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CourseResultsController : ControllerBase
{
    private readonly AppDbContext _db;
    public CourseResultsController(AppDbContext db) => _db = db;

    // GET /api/courseresults?employeeId=&courseId=&isCorrect=&from=&to=&current=&pageSize=&sort=AnsweredAt|-AnsweredAt
    [HttpGet]
   //[Authorize(Roles = "HR, Admin")]
    public async Task<IActionResult> Search(
        [FromQuery] Guid? employeeId,
        [FromQuery] Guid? courseId,
        [FromQuery] bool? isCorrect,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int current = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sort = "-AnsweredAt",
        CancellationToken ct = default)
    {
        try
        {
            if (current < 1) current = 1;
            if (pageSize is < 1 or > 200) pageSize = 20;

            var q = _db.CourseResults
                .AsNoTracking()
                .Include(x => x.Course)
                .Include(x => x.Question)
                .AsQueryable();

            if (employeeId.HasValue) q = q.Where(x => x.EmployeeId == employeeId.Value);
            if (courseId.HasValue) q = q.Where(x => x.CourseId == courseId.Value);
            if (isCorrect.HasValue) q = q.Where(x => x.IsCorrect == isCorrect.Value);
            if (from.HasValue) q = q.Where(x => x.AnsweredAt >= from.Value);
            if (to.HasValue) q = q.Where(x => x.AnsweredAt < to.Value);

            q = sort?.Trim() switch
            {
                "AnsweredAt" => q.OrderBy(x => x.AnsweredAt),
                "-AnsweredAt" => q.OrderByDescending(x => x.AnsweredAt),
                _ => q.OrderByDescending(x => x.AnsweredAt)
            };

            var total = await q.CountAsync(ct);

            var result = await q
                .Skip((current - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new CourseResultDto
                {
                    EmployeeId = x.EmployeeId,
                    CourseId = x.CourseId,
                    QuestionId = x.QuestionId,
                    Chosen = x.Chosen,
                    IsCorrect = x.IsCorrect,
                    AnsweredAt = x.AnsweredAt,
                    CourseName = x.Course.Name,
                    QuestionContent = x.Question.Content
                })
                .ToListAsync(ct);

            var meta = new
            {
                current,
                pageSize,
                pages = (int)Math.Ceiling(total / (double)pageSize),
                total
            };

            return this.OKSingle(new { meta, result }, total > 0 ? "Tìm thấy kết quả." : "Không có kết quả.");
        }
        catch
        {
            return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi khi tìm kiếm kết quả khóa học.");
        }
    }

    // POST /api/courseresults/submit  (upsert 1 câu)
    //[HttpPost("submit")]
    //[Authorize] // có thể siết theo role/permission tuỳ bạn
    //public async Task<IActionResult> Submit([FromBody] SubmitCourseAnswerRequest req, CancellationToken ct)
    //{
    //    try
    //    {
    //        if (!ModelState.IsValid)
    //            return this.FAIL(StatusCodes.Status400BadRequest, "Dữ liệu không hợp lệ.");

    //        // Validate tồn tại
    //        var question = await _db.CourseQuestions
    //            .AsNoTracking()
    //            .FirstOrDefaultAsync(q => q.Id == req.QuestionId, ct);
    //        if (question is null)
    //            return this.FAIL(StatusCodes.Status404NotFound, "Câu hỏi không tồn tại.");

    //        if (question.CourseId != req.CourseId)
    //            return this.FAIL(StatusCodes.Status400BadRequest, "QuestionId không thuộc CourseId.");

    //        var employeeExists = await _db.Employees.AnyAsync(e => e.Id == req.EmployeeId, ct);
    //        if (!employeeExists)
    //            return this.FAIL(StatusCodes.Status404NotFound, "Nhân viên không tồn tại.");

    //        var courseExists = await _db.Courses.AnyAsync(c => c.Id == req.CourseId, ct);
    //        if (!courseExists)
    //            return this.FAIL(StatusCodes.Status404NotFound, "Khoá học không tồn tại.");

    //        var chosen = req.Chosen.Trim().ToUpperInvariant();
    //        var correct = string.Equals(chosen, question.Correct, StringComparison.OrdinalIgnoreCase);

    //        // Upsert theo composite key
    //        var existing = await _db.CourseResults
    //            .FirstOrDefaultAsync(x =>
    //                x.EmployeeId == req.EmployeeId &&
    //                x.CourseId == req.CourseId &&
    //                x.QuestionId == req.QuestionId, ct);

    //        if (existing is null)
    //        {
    //            var e = new CourseResult
    //            {
    //                EmployeeId = req.EmployeeId,
    //                CourseId = req.CourseId,
    //                QuestionId = req.QuestionId,
    //                Chosen = chosen,
    //                IsCorrect = correct,
    //                AnsweredAt = DateTime.UtcNow
    //            };
    //            _db.CourseResults.Add(e);
    //        }
    //        else
    //        {
    //            existing.Chosen = chosen;
    //            existing.IsCorrect = correct;
    //            existing.AnsweredAt = DateTime.UtcNow;
    //        }

    //        await _db.SaveChangesAsync(ct);

    //        return StatusCode(StatusCodes.Status201Created, new
    //        {
    //            statusCode = StatusCodes.Status201Created,
    //            message = "Ghi nhận câu trả lời thành công.",
    //            data = Array.Empty<object>(),
    //            success = true
    //        });
    //    }
    //    catch (DbUpdateException)
    //    {
    //        return this.FAIL(StatusCodes.Status409Conflict, "Xung đột khi ghi nhận câu trả lời.");
    //    }
    //    catch
    //    {
    //        return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi khi ghi nhận câu trả lời.");
    //    }
    //}

    [HttpPost("submit")]
   //[Authorize(Roles = "HR, Admin")]
    public async Task<IActionResult> Submit([FromBody] SubmitAnswerRequest req, CancellationToken ct)
    {
        // Validate cơ bản
        var question = await _db.CourseQuestions
            .AsNoTracking()
            .Include(q => q.Course)
            .FirstOrDefaultAsync(q => q.Id == req.QuestionId && q.CourseId == req.CourseId, ct);
        if (question is null)
            return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy câu hỏi thuộc khoá học.");

        var chosen = (req.Chosen ?? "").Trim().ToUpperInvariant();
        if (chosen is not ("A" or "B" or "C" or "D"))
            return this.FAIL(StatusCodes.Status400BadRequest, "Đáp án phải là A/B/C/D.");

        var isCorrect = string.Equals(chosen, question.Correct, StringComparison.OrdinalIgnoreCase);

        // Upsert theo PK (EmployeeId, CourseId, QuestionId)
        var entity = await _db.CourseResults.FirstOrDefaultAsync(x =>
            x.EmployeeId == req.EmployeeId &&
            x.CourseId == req.CourseId &&
            x.QuestionId == req.QuestionId, ct);

        if (entity is null)
        {
            entity = new CourseResult
            {
                EmployeeId = req.EmployeeId,
                CourseId = req.CourseId,
                QuestionId = req.QuestionId,
                Chosen = chosen,
                IsCorrect = isCorrect,
                AnsweredAt = DateTime.UtcNow
            };
            _db.CourseResults.Add(entity);
        }
        else
        {
            entity.Chosen = chosen;
            entity.IsCorrect = isCorrect;
            entity.AnsweredAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        var dto = new CourseResultDto
        {
            EmployeeId = entity.EmployeeId,
            CourseId = entity.CourseId,
            QuestionId = entity.QuestionId,
            Chosen = entity.Chosen,
            IsCorrect = entity.IsCorrect,
            AnsweredAt = entity.AnsweredAt,
            CourseName = question.Course?.Name,
            QuestionContent = question.Content
        };

        return StatusCode(StatusCodes.Status201Created, new
        {
            statusCode = StatusCodes.Status201Created,
            message = "Ghi nhận câu trả lời thành công.",
            data = new { result = dto },
            success = true
        });
    }


    // POST /api/courseresults/bulk-submit  (upsert nhiều câu)
    //[HttpPost("bulk-submit")]
    //[Authorize]
    //public async Task<IActionResult> BulkSubmit([FromBody] BulkSubmitCourseAnswersRequest req, CancellationToken ct)
    //{
    //    try
    //    {
    //        if (!ModelState.IsValid)
    //            return this.FAIL(StatusCodes.Status400BadRequest, "Dữ liệu không hợp lệ.");

    //        // Validate đầu vào
    //        if (!await _db.Employees.AnyAsync(e => e.Id == req.EmployeeId, ct))
    //            return this.FAIL(StatusCodes.Status404NotFound, "Nhân viên không tồn tại.");
    //        if (!await _db.Courses.AnyAsync(c => c.Id == req.CourseId, ct))
    //            return this.FAIL(StatusCodes.Status404NotFound, "Khóa học không tồn tại.");

    //        // Lấy đáp án đúng cho tất cả question
    //        var qIds = req.Answers.Select(a => a.QuestionId).Distinct().ToList();
    //        var questions = await _db.CourseQuestions
    //            .Where(q => qIds.Contains(q.Id))
    //            .ToDictionaryAsync(q => q.Id, ct);

    //        // Kiểm tra tất cả question thuộc course
    //        foreach (var qid in qIds)
    //        {
    //            if (!questions.TryGetValue(qid, out var q))
    //                return this.FAIL(StatusCodes.Status404NotFound, $"Câu hỏi {qid} không tồn tại.");
    //            if (q.CourseId != req.CourseId)
    //                return this.FAIL(StatusCodes.Status400BadRequest, $"Câu hỏi {qid} không thuộc khóa học.");
    //        }

    //        // Upsert từng câu
    //        foreach (var a in req.Answers)
    //        {
    //            var chosen = a.Chosen.Trim().ToUpperInvariant();
    //            var correct = string.Equals(chosen, questions[a.QuestionId].Correct, StringComparison.OrdinalIgnoreCase);

    //            var existing = await _db.CourseResults.FirstOrDefaultAsync(x =>
    //                x.EmployeeId == req.EmployeeId &&
    //                x.CourseId == req.CourseId &&
    //                x.QuestionId == a.QuestionId, ct);

    //            if (existing is null)
    //            {
    //                _db.CourseResults.Add(new CourseResult
    //                {
    //                    EmployeeId = req.EmployeeId,
    //                    CourseId = req.CourseId,
    //                    QuestionId = a.QuestionId,
    //                    Chosen = chosen,
    //                    IsCorrect = correct,
    //                    AnsweredAt = DateTime.UtcNow
    //                });
    //            }
    //            else
    //            {
    //                existing.Chosen = chosen;
    //                existing.IsCorrect = correct;
    //                existing.AnsweredAt = DateTime.UtcNow;
    //            }
    //        }

    //        await _db.SaveChangesAsync(ct);

    //        return StatusCode(StatusCodes.Status201Created, new
    //        {
    //            statusCode = StatusCodes.Status201Created,
    //            message = "Ghi nhận đáp án hàng loạt thành công.",
    //            data = Array.Empty<object>(),
    //            success = true
    //        });
    //    }
    //    catch
    //    {
    //        return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi khi ghi nhận đáp án hàng loạt.");
    //    }
    //}

    //[HttpPost("bulk-submit")]
    //[Authorize(Roles = "HR, Admin")]
    //public async Task<IActionResult> BulkSubmit([FromBody] List<SubmitAnswerRequest> reqs, CancellationToken ct)
    //{
    //    if (reqs is null || reqs.Count == 0)
    //        return this.FAIL(StatusCodes.Status400BadRequest, "Danh sách rỗng.");

    //    var outDtos = new List<CourseResultDto>();

    //    // Tối ưu: load trước toàn bộ câu hỏi cần thiết
    //    var qIds = reqs.Select(r => r.QuestionId).Distinct().ToList();
    //    var questions = await _db.CourseQuestions
    //        .Where(q => qIds.Contains(q.Id))
    //        .ToDictionaryAsync(q => q.Id, ct);

    //    foreach (var r in reqs)
    //    {
    //        if (!questions.TryGetValue(r.QuestionId, out var q) || q.CourseId != r.CourseId)
    //            continue; // hoặc gom lỗi tuỳ bạn

    //        var chosen = (r.Chosen ?? "").Trim().ToUpperInvariant();
    //        if (chosen is not ("A" or "B" or "C" or "D")) continue;

    //        var isCorrect = string.Equals(chosen, q.Correct, StringComparison.OrdinalIgnoreCase);

    //        var entity = await _db.CourseResults.FirstOrDefaultAsync(x =>
    //            x.EmployeeId == r.EmployeeId &&
    //            x.CourseId == r.CourseId &&
    //            x.QuestionId == r.QuestionId, ct);

    //        if (entity is null)
    //        {
    //            entity = new CourseResult
    //            {
    //                EmployeeId = r.EmployeeId,
    //                CourseId = r.CourseId,
    //                QuestionId = r.QuestionId,
    //                Chosen = chosen,
    //                IsCorrect = isCorrect,
    //                AnsweredAt = DateTime.UtcNow
    //            };
    //            _db.CourseResults.Add(entity);
    //        }
    //        else
    //        {
    //            entity.Chosen = chosen;
    //            entity.IsCorrect = isCorrect;
    //            entity.AnsweredAt = DateTime.UtcNow;
    //        }

    //        outDtos.Add(new CourseResultDto
    //        {
    //            EmployeeId = r.EmployeeId,
    //            CourseId = r.CourseId,
    //            QuestionId = r.QuestionId,
    //            Chosen = chosen,
    //            IsCorrect = isCorrect,
    //            AnsweredAt = entity.AnsweredAt
    //        });
    //    }

    //    await _db.SaveChangesAsync(ct);

    //    return StatusCode(StatusCodes.Status201Created, new
    //    {
    //        statusCode = StatusCodes.Status201Created,
    //        message = "Ghi nhận đáp án hàng loạt thành công.",
    //        data = new { result = outDtos },
    //        success = true
    //    });
    //}

    [HttpPost("bulk-submit")]
   //[Authorize(Roles = "HR, Admin")]
    public async Task<IActionResult> BulkSubmit([FromBody] BulkSubmitRequest req, CancellationToken ct)
    {
        // --- Validate đầu vào cơ bản ---
        if (req is null || req.Answers is null || req.Answers.Count == 0)
            return this.FAIL(StatusCodes.Status400BadRequest, "Danh sách đáp án trống.");

        // Chuẩn hóa/lọc đáp án hợp lệ A/B/C/D
        static bool IsValidChosen(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            var v = s.Trim().ToUpperInvariant();
            return v is "A" or "B" or "C" or "D";
        }

        // Lấy toàn bộ câu hỏi liên quan (để có Correct + ràng buộc CourseId)
        var qIds = req.Answers.Select(a => a.QuestionId).Distinct().ToList();
        var questions = await _db.CourseQuestions
            .Where(q => qIds.Contains(q.Id))
            .Include(q => q.Course)
            .ToDictionaryAsync(q => q.Id, ct);

        var outDtos = new List<CourseResultDto>();

        foreach (var a in req.Answers)
        {
            if (!questions.TryGetValue(a.QuestionId, out var q)) continue;                // câu hỏi không tồn tại
            if (q.CourseId != req.CourseId) continue;                                      // câu hỏi không thuộc course
            if (!IsValidChosen(a.Chosen)) continue;                                        // đáp án không hợp lệ

            var chosen = a.Chosen.Trim().ToUpperInvariant();
            var isCorrect = string.Equals(chosen, q.Correct, StringComparison.OrdinalIgnoreCase);

            // Kiểm tra đã có bản ghi kết quả chưa
            var entity = await _db.CourseResults.FirstOrDefaultAsync(x =>
                x.EmployeeId == req.EmployeeId &&
                x.CourseId == req.CourseId &&
                x.QuestionId == a.QuestionId, ct);

            if (entity is null)
            {
                entity = new CourseResult
                {
                    EmployeeId = req.EmployeeId,
                    CourseId = req.CourseId,
                    QuestionId = a.QuestionId,
                    Chosen = chosen,
                    IsCorrect = isCorrect,
                    AnsweredAt = DateTime.UtcNow
                };
                _db.CourseResults.Add(entity);
            }
            else
            {
                entity.Chosen = chosen;
                entity.IsCorrect = isCorrect;
                entity.AnsweredAt = DateTime.UtcNow;
            }

            outDtos.Add(new CourseResultDto
            {
                EmployeeId = req.EmployeeId,
                CourseId = req.CourseId,
                QuestionId = a.QuestionId,
                Chosen = chosen,
                IsCorrect = isCorrect,
                AnsweredAt = entity.AnsweredAt,
                CourseName = q.Course?.Name,
                QuestionContent = q.Content
            });
        }

        await _db.SaveChangesAsync(ct);

        return StatusCode(StatusCodes.Status201Created, new
        {
            statusCode = StatusCodes.Status201Created,
            message = "Ghi nhận đáp án hàng loạt thành công.",
            data = new { result = outDtos },   // <-- giờ trả đúng mảng kết quả
            success = true
        });
    }



    // GET /api/courseresults/score?employeeId=&courseId=&updateTrainingRecord=false
    //[HttpGet("score")]
    //[Authorize(Roles = "HR, Admin")]
    //public async Task<IActionResult> GetScore(
    //    [FromQuery] Guid employeeId,
    //    [FromQuery] Guid courseId,
    //    [FromQuery] bool updateTrainingRecord = false,
    //    CancellationToken ct = default)
    //{
    //    try
    //    {
    //        // Tổng số câu của khóa
    //        var totalQ = await _db.CourseQuestions.CountAsync(q => q.CourseId == courseId, ct);
    //        if (totalQ == 0)
    //            return this.FAIL(StatusCodes.Status400BadRequest, "Khóa học chưa có câu hỏi.");

    //        // Thống kê
    //        var stats = await _db.CourseResults
    //            .Where(r => r.EmployeeId == employeeId && r.CourseId == courseId)
    //            .GroupBy(r => 1)
    //            .Select(g => new
    //            {
    //                Answered = g.Count(),
    //                Correct = g.Count(x => x.IsCorrect)
    //            })
    //            .FirstOrDefaultAsync(ct) ?? new { Answered = 0, Correct = 0 };

    //        var score = Math.Round(totalQ == 0 ? 0m : (decimal)stats.Correct * 100m / totalQ, 2);

    //        // Pass/Fail nếu đã làm đủ hết câu
    //        bool? passed = null;
    //        var course = await _db.Courses.AsNoTracking().FirstAsync(c => c.Id == courseId, ct);
    //        if (stats.Answered == totalQ)
    //            passed = score >= course.PassThreshold;

    //        var dto = new CourseScoreDto
    //        {
    //            EmployeeId = employeeId,
    //            CourseId = courseId,
    //            TotalQuestions = totalQ,
    //            Answered = stats.Answered,
    //            Correct = stats.Correct,
    //            ScorePercent = score,
    //            Passed = passed
    //        };

    //        // (Tuỳ chọn) cập nhật TrainingRecord.Score & Status
    //        if (updateTrainingRecord)
    //        {
    //            var tr = await _db.TrainingRecords.FirstOrDefaultAsync(x =>
    //                x.EmployeeId == employeeId && x.CourseId == courseId, ct);

    //            if (tr is null)
    //            {
    //                tr = new TrainingRecord
    //                {
    //                    Id = Guid.NewGuid(),
    //                    EmployeeId = employeeId,
    //                    CourseId = courseId,
    //                    Score = score,
    //                    Status = (passed == true) ? TrainingStatus.completed : TrainingStatus.in_progress
    //                };
    //                _db.TrainingRecords.Add(tr);
    //            }
    //            else
    //            {
    //                tr.Score = score;
    //                if (passed == true) tr.Status = TrainingStatus.completed;
    //            }

    //            await _db.SaveChangesAsync(ct);
    //        }

    //        return this.OKSingle(dto, "Tính điểm thành công.");
    //    }
    //    catch
    //    {
    //        return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi khi tính điểm.");
    //    }
    //}

    [HttpGet("score")]
   //[Authorize(Roles = "HR, Admin")]
    public async Task<IActionResult> GetScore([FromQuery] Guid employeeId, [FromQuery] Guid courseId, CancellationToken ct)
    {
        var course = await _db.Courses
            .AsNoTracking()
            .Include(c => c.Questions)
            .FirstOrDefaultAsync(c => c.Id == courseId, ct);

        if (course is null)
            return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy khoá học.");

        var totalQuestions = course.Questions.Count;
        if (totalQuestions == 0)
            return this.OKSingle(new
            {
                employeeId,
                courseId,
                totalQuestions = 0,
                answered = 0,
                correct = 0,
                scorePercent = 0m,
                passed = false
            }, "Khoá học chưa có câu hỏi.");

        var results = await _db.CourseResults
            .AsNoTracking()
            .Where(r => r.EmployeeId == employeeId && r.CourseId == courseId)
            .ToListAsync(ct);

        var answered = results.Count;
        var correct = results.Count(r => r.IsCorrect);
        var scorePercent = Math.Round(100m * correct / totalQuestions, 2, MidpointRounding.AwayFromZero);
        var passed = scorePercent >= course.PassThreshold; // TRUE/FALSE rõ ràng

        var payload = new
        {
            employeeId,
            courseId,
            totalQuestions,
            answered,
            correct,
            scorePercent,
            passed
        };

        return this.OKSingle(payload, "Tính điểm thành công.");
    }


    // DELETE /api/courseresults/{employeeId}/{courseId}/{questionId}
    [HttpDelete("{employeeId:guid}/{courseId:guid}/{questionId:guid}")]
   //[Authorize(Roles = "HR, Admin")]
    public async Task<IActionResult> Delete(Guid employeeId, Guid courseId, Guid questionId, CancellationToken ct)
    {
        try
        {
            var e = await _db.CourseResults.FirstOrDefaultAsync(x =>
                x.EmployeeId == employeeId &&
                x.CourseId == courseId &&
                x.QuestionId == questionId, ct);

            if (e is null) return this.FAIL(StatusCodes.Status404NotFound, "Không tìm thấy bản ghi.");

            _db.CourseResults.Remove(e);
            await _db.SaveChangesAsync(ct);

            return this.OK(message: "Xoá kết quả câu hỏi thành công.");
        }
        catch (DbUpdateException)
        {
            return this.FAIL(StatusCodes.Status409Conflict, "Không thể xoá do đang được tham chiếu.");
        }
        catch
        {
            return this.FAIL(StatusCodes.Status500InternalServerError, "Lỗi khi xoá kết quả.");
        }
    }
}
