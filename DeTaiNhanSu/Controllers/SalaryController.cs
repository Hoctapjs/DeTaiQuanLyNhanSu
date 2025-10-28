using DeTaiNhanSu.DbContextProject;
using DeTaiNhanSu.Models;
using DeTaiNhanSu.Enums; // ĐÃ THÊM USING ENUMS
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Linq.Dynamic.Core.Exceptions;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace DeTaiNhanSu.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SalaryController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SalaryController(AppDbContext context) => _context = context;

        // Phương thức hỗ trợ tạo phản hồi lỗi nhất quán
        private IActionResult CreateErrorResponse(int statusCode, string message)
        {
            return StatusCode(statusCode, new
            {
                statusCode = statusCode,
                success = false,
                message = message
            });
        }

        // =================================================================
        // Lấy danh sách tất cả các bản ghi lương đã chốt
        // GET /api/Salary - ĐÃ CHỈNH SỬA ENUM
        // =================================================================
        [HttpGet]
        public async Task<IActionResult> GetSalaries(
            [FromQuery] string? q,
            [FromQuery] int current = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string sort = "PayrollRun.Period desc")
        {
            var initialQuery = _context.Salaries
                .Include(s => s.Employee)
                .Include(s => s.PayrollRun)
                // ĐÃ CHỈNH SỬA: Dùng Enum PayrollRunStatus.processed hoặc .locked
                // Giả định "finalized" trong code gốc tương đương với processed/locked
                .Where(s => s.PayrollRun.Status == PayrollRunStatus.processed || s.PayrollRun.Status == PayrollRunStatus.locked)
                .AsQueryable();

            IQueryable<Salary> query = initialQuery;

            if (!string.IsNullOrEmpty(q))
            {
                query = query.Where(s =>
                    (s.Employee != null && s.Employee.FullName.Contains(q)) ||
                    (s.Employee != null && s.Employee.Code.Contains(q)) ||
                    s.PayrollRun.Period.Contains(q)
                );

                if (await initialQuery.AnyAsync() && !await query.AnyAsync())
                {
                    return CreateErrorResponse(400, $"Không tìm thấy kết quả nào cho '{q}'. Vui lòng tìm kiếm theo: Tên NV, Mã NV, hoặc Kỳ lương (YYYY-MM).");
                }
            }

            // 2. Tính tổng số lượng và phân trang
            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            List<dynamic> salaryList = new List<dynamic>();

            // 3. Sắp xếp và phân trang - BỌC TRONG KHỐI TRY-CATCH
            try
            {
                var tempSalaryList = await query
                    .OrderBy(sort)
                    .Skip((current - 1) * pageSize)
                    .Take(pageSize)
                    .Select(s => new
                    {
                        id = s.Id,
                        employeeId = s.EmployeeId,
                        employeeName = s.Employee != null ? s.Employee.FullName : "N/A",
                        payrollRunId = s.PayrollRunId,
                        period = s.PayrollRun.Period,
                        Gross = Math.Round(s.Gross, 3),
                        Net = Math.Round(s.Net, 3),
                        details = s.Details
                    })
                    .ToListAsync();

                salaryList.AddRange(tempSalaryList.Cast<dynamic>());

            }
            catch (ParseException ex)
            {
                string supportedFields = "Hỗ trợ sắp xếp theo: Gross, Net, Employee.FullName, PayrollRun.Period. (Thêm ' asc' hoặc ' desc')";
                return CreateErrorResponse(400, $"Lỗi sắp xếp: Tên cột '{sort}' không hợp lệ. {supportedFields}");
            }
            catch (Exception)
            {
                throw;
            }

            // 4. Trả về Response
            return Ok(new
            {
                statusCode = 200,
                message = $"Tìm thấy {totalCount} bản ghi lương đã chốt.",
                data = new[]
                {
                    new
                    {
                        meta = new
                        {
                            current = current,
                            pageSize = pageSize,
                            pages = totalPages,
                            total = totalCount
                        },
                        result = salaryList
                    }
                },
                success = true
            });
        }

        // =================================================================
        // API Lấy chi tiết Salary Items theo Salary ID
        // GET /api/Salary/details/{salaryId} - ĐÃ CHỈNH SỬA ENUM
        // =================================================================
        [HttpGet("details/{salaryId}")]
        public async Task<IActionResult> GetSalaryDetails(Guid salaryId)
        {
            // 1. Lấy bản ghi Salary chính và các SalaryItems liên quan
            // Giả định thuộc tính trên Salary Model là Items (từ Salary.ICollection<SalaryItem> Items)
            // Nếu thuộc tính là SalaryItems, bạn cần đổi lại .Include(s => s.SalaryItems)
            var salaryRecord = await _context.Salaries
                .Where(s => s.Id == salaryId)
                .Include(s => s.Items)
                .Include(s => s.Employee)
                .Include(s => s.PayrollRun)
                .FirstOrDefaultAsync();

            if (salaryRecord == null)
            {
                return CreateErrorResponse(404, "Không tìm thấy bản ghi lương này.");
            }

            // 2. Cấu trúc danh sách các khoản mục chi tiết
            // ĐÃ CHỈNH SỬA: item.Type là Enum, cần gọi ToString()
            var detailedItems = salaryRecord.Items.Select(item => new
            {
                itemId = item.Id,
                type = item.Type.ToString(), // SỬ DỤNG ENUM.ToString()
                amount = Math.Round(item.Amount, 3),
                note = item.Note
            }).ToList();

            // 3. Tính toán tổng cộng
            var totalEarnings = detailedItems.Where(i => i.amount >= 0).Sum(i => i.amount);
            var totalDeductions = detailedItems.Where(i => i.amount < 0).Sum(i => i.amount);

            // 4. Cấu trúc phản hồi tổng thể
            var responseData = new
            {
                employee = new
                {
                    id = salaryRecord.EmployeeId,
                    fullName = salaryRecord.Employee?.FullName,
                    code = salaryRecord.Employee?.Code,
                },
                payroll = new
                {
                    salaryId = salaryRecord.Id,
                    period = salaryRecord.PayrollRun.Period,
                    grossSalary = Math.Round(salaryRecord.Gross, 3),
                    netSalary = Math.Round(salaryRecord.Net, 3),
                    totalEarnings = Math.Round(totalEarnings, 3),
                    totalDeductions = Math.Round(totalDeductions, 3),
                    detailsJson = salaryRecord.Details
                },
                items = detailedItems // Danh sách SalaryItems
            };

            // TRẢ VỀ THÀNH CÔNG 200 OK
            return Ok(new
            {
                statusCode = 200,
                success = true,
                message = $"Chi tiết lương kỳ {salaryRecord.PayrollRun.Period}",
                data = responseData
            });
        }
    }
}