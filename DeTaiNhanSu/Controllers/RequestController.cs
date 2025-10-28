using DeTaiNhanSu.DbContextProject;
using DeTaiNhanSu.Models;
using DeTaiNhanSu.Enums; // Đảm bảo đã import Enums
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Linq.Dynamic.Core.Exceptions;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DeTaiNhanSu.Controllers
{
    // =================================================================
    //                  DTOs (Data Transfer Objects) - ĐÃ CHỈNH SỬA
    // =================================================================
    public class CreateRequestSeparatedDto
    {
        public Guid EmployeeId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public RequestCategory Category { get; set; } // SỬ DỤNG ENUM

        public DateOnly FromDate { get; set; } // SỬ DỤNG DATEONLY
        public DateOnly? ToDate { get; set; } // SỬ DỤNG DATEONLY?

        public string StartTime { get; set; } = string.Empty; // Giữ string để Parse TimeSpan
        public string? EndTime { get; set; }
    }

    public class ProcessRequestDto
    {
        public RequestStatus NewStatus { get; set; } // SỬ DỤNG ENUM
        public Guid ApproverUserId { get; set; }
        public decimal ApprovedHours { get; set; }
        public decimal Rate { get; set; } = 1.5m;
        public string? Reason { get; set; }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class RequestController : ControllerBase
    {
        private readonly AppDbContext _context;

        public RequestController(AppDbContext context) => _context = context;

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
        //                  API 1: TẠO YÊU CẦU MỚI (POST) - ĐÃ CHỈNH SỬA
        // =================================================================
        [HttpPost]
        public async Task<IActionResult> CreateRequest([FromBody] CreateRequestSeparatedDto newRequest)
        {
            // 1. Kiểm tra đầu vào và Logic
            if (newRequest.EmployeeId == Guid.Empty || newRequest.FromDate == DateOnly.MinValue) // Dùng DateOnly.MinValue
            {
                return CreateErrorResponse(400, "Thiếu thông tin bắt buộc (EmployeeId, Category, hoặc FromDate).");
            }

            // Kiểm tra Category chỉ cho phép ot hoặc leave
            if (newRequest.Category != RequestCategory.ot && newRequest.Category != RequestCategory.leave)
            {
                return CreateErrorResponse(400, $"Loại yêu cầu (Category) không hợp lệ. Chỉ chấp nhận '{RequestCategory.ot}' hoặc '{RequestCategory.leave}'.");
            }

            // 2. CHUYỂN ĐỔI CHUỖI GIỜ SANG TIMESPAN AN TOÀN
            TimeSpan startTimeSpan;
            TimeSpan? endTimeSpan = null;

            if (!TimeSpan.TryParse(newRequest.StartTime, out startTimeSpan))
            {
                return CreateErrorResponse(400, "Định dạng StartTime không hợp lệ. Vui lòng nhập giờ theo định dạng HH:mm:ss.");
            }

            if (!string.IsNullOrEmpty(newRequest.EndTime) && TimeSpan.TryParse(newRequest.EndTime, out TimeSpan tempEndTime))
            {
                endTimeSpan = tempEndTime;
            }

            // 3. CHUẨN BỊ VÀ XÁC THỰC NGÀY (SỬ DỤNG DATEONLY)
            DateOnly today = DateOnly.FromDateTime(DateTime.Today);
            DateOnly vnStartDate = newRequest.FromDate;
            DateOnly? vnEndDate = newRequest.ToDate;

            if (vnStartDate < today)
            {
                return CreateErrorResponse(400, "Không thể tạo yêu cầu cho ngày trong quá khứ.");
            }
            if (vnEndDate.HasValue && vnEndDate.Value < vnStartDate)
            {
                return CreateErrorResponse(400, "Ngày kết thúc không được trước ngày bắt đầu.");
            }

            // KIỂM TRA NHÂN VIÊN TỒN TẠI
            var employeeExists = await _context.Employees.AnyAsync(e => e.Id == newRequest.EmployeeId);
            if (!employeeExists)
            {
                return CreateErrorResponse(404, "Không tìm thấy nhân viên với ID được cung cấp.");
            }

            // Kiểm tra chồng chéo (Dùng DateOnly và Enum RequestStatus)
            var isDuplicate = await _context.Requests
                .AnyAsync(r => r.EmployeeId == newRequest.EmployeeId &&
                                r.FromDate.HasValue && r.FromDate.Value == vnStartDate &&
                                r.Status != RequestStatus.rejected); // SỬ DỤNG ENUM

            if (isDuplicate)
            {
                return CreateErrorResponse(400, $"Đã có yêu cầu {newRequest.Category} (hoặc yêu cầu khác) tồn tại cho ngày {vnStartDate:dd/MM/yyyy}.");
            }

            // 4. Tạo bản ghi Request (SỬ DỤNG ENUM, DATEONLY, TIMESPAN)
            var requestModel = new Request
            {
                Id = Guid.NewGuid(),
                EmployeeId = newRequest.EmployeeId,
                Title = newRequest.Title,
                Description = newRequest.Description,
                Category = newRequest.Category, // Gán Enum
                FromDate = vnStartDate,        // Gán DateOnly
                ToDate = vnEndDate,            // Gán DateOnly?
                StartTime = startTimeSpan,     // Gán TimeSpan
                EndTime = endTimeSpan,         // Gán TimeSpan?
                Status = RequestStatus.pending, // Gán Enum
                ApprovedBy = null,
                CreatedAt = DateTime.Now
            };

            _context.Requests.Add(requestModel);

            // 5. TẠO BẢN GHI ATTENDANCES TẠM THỜI CHO LEAVE
            if (requestModel.Category == RequestCategory.leave)
            {
                DateOnly startDate = requestModel.FromDate.Value;
                DateOnly endDate = requestModel.ToDate ?? startDate;

                // Để lặp qua ngày (chúng ta cần một cách an toàn để chuyển DateOnly sang DateTime)
                DateTime loopStart = startDate.ToDateTime(TimeOnly.MinValue);
                DateTime loopEnd = endDate.ToDateTime(TimeOnly.MinValue);

                for (DateTime date = loopStart; date <= loopEnd; date = date.AddDays(1))
                {
                    if (date.DayOfWeek == DayOfWeek.Sunday) continue;

                    DateOnly currentDayOnly = DateOnly.FromDateTime(date);

                    // Giả định Attendance Model dùng DateOnly
                    var attendance = await _context.Attendances
                        .FirstOrDefaultAsync(a => a.EmployeeId == requestModel.EmployeeId && a.Date == currentDayOnly);

                    if (attendance == null)
                    {
                        // Giả định Attendance Model dùng Enum AttendanceStatus
                        attendance = new Attendance
                        {
                            Id = Guid.NewGuid(),
                            EmployeeId = requestModel.EmployeeId,
                            Date = currentDayOnly,
                            Status = AttendanceStatus.absent, // SỬ DỤNG ENUM
                            Note = $"Vắng mặt do yêu cầu nghỉ phép đang chờ duyệt: {requestModel.Title}"
                        };
                        _context.Attendances.Add(attendance);
                    }
                }
            }

            await _context.SaveChangesAsync();

            // 6. TRẢ VỀ RESPONSE THÀNH CÔNG (201 Created)
            return StatusCode(201, new
            {
                statusCode = 201,
                success = true,
                message = $"Đã tạo yêu cầu {requestModel.Category} thành công và ghi nhận trạng thái tạm thời."
            });
        }


        // =================================================================
        //                  API 2: LẤY DANH SÁCH YÊU CẦU (GET) - ĐÃ CHỈNH SỬA
        // =================================================================
        [HttpGet]
        public async Task<IActionResult> GetRequests(
           [FromQuery] string? q,
           [FromQuery] int current = 1,
           [FromQuery] int pageSize = 20,
           [FromQuery] string sort = "CreatedAt desc")
        {
            var initialQuery = _context.Requests
                .Include(r => r.Employee)
                .AsQueryable();

            IQueryable<Request> query = initialQuery;

            // 1. KIỂM TRA ĐIỀU KIỆN 404
            bool hasAnyRequests = await initialQuery.AnyAsync();
            if (!hasAnyRequests)
            {
                return CreateErrorResponse(404, "Hệ thống chưa có bất kỳ bản ghi yêu cầu nào.");
            }

            // 2. Lọc theo chuỗi tìm kiếm 'q' (Dùng Enum.ToString() cho truy vấn an toàn)
            if (!string.IsNullOrEmpty(q))
            {
                query = query.Where(r =>
                    r.Title.Contains(q) ||
                    (r.Description != null && r.Description.Contains(q)) ||
                    r.Category.ToString().Contains(q) || // SỬ DỤNG ENUM.ToString()
                    r.Status.ToString().Contains(q) ||  // SỬ DỤNG ENUM.ToString()
                    (r.Employee != null && r.Employee.FullName.Contains(q)) ||
                    (r.Employee != null && r.Employee.Code.Contains(q))
                );
            }

            // 3. Tính tổng số lượng và phân trang
            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            bool filtersApplied = !string.IsNullOrEmpty(q);
            if (totalCount == 0 && filtersApplied)
            {
                string supportedSearchFields = "Tên NV, Mã NV, Tiêu đề, Mô tả, Loại yêu cầu (Category), hoặc Trạng thái (Status).";
                return CreateErrorResponse(400, $"Không tìm thấy yêu cầu nào khớp với '{q}'. Vui lòng tìm kiếm theo: {supportedSearchFields}");
            }

            string responseMessage = $"Tìm thấy {totalCount} bản ghi yêu cầu.";

            List<dynamic> requestList = new List<dynamic>();

            // 4. Sắp xếp và phân trang - BỌC TRONG KHỐI TRY-CATCH
            try
            {
                if (totalCount > 0)
                {
                    var tempRequestList = await query
                        .OrderBy(sort)
                        .Skip((current - 1) * pageSize)
                        .Take(pageSize)
                        .Select(r => new
                        {
                            id = r.Id,
                            employeeId = r.EmployeeId,
                            employeeName = r.Employee != null ? r.Employee.FullName : "N/A",
                            employeeCode = r.Employee != null ? r.Employee.Code : "N/A",
                            title = r.Title,
                            description = r.Description ?? string.Empty,
                            category = r.Category.ToString(), // Trả về Enum dạng string
                            status = r.Status.ToString(),     // Trả về Enum dạng string

                            // Dùng DateOnly.ToString
                            date = r.FromDate.HasValue ? r.FromDate.Value.ToString("yyyy-MM-dd") : null,
                            fromDate = r.FromDate.HasValue ? r.FromDate.Value.ToString("yyyy-MM-dd") : string.Empty,
                            toDate = r.ToDate.HasValue ? r.ToDate.Value.ToString("yyyy-MM-dd") : string.Empty,

                            // Dùng TimeSpan.ToString
                            startTime = r.StartTime.HasValue ? r.StartTime.Value.ToString(@"hh\:mm\:ss") : string.Empty,
                            endTime = r.EndTime.HasValue ? r.EndTime.Value.ToString(@"hh\:mm\:ss") : string.Empty,

                            createdAt = r.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                            approvedBy = r.ApprovedBy
                        })
                        .ToListAsync();

                    requestList.AddRange(tempRequestList.Cast<dynamic>());
                }
            }
            catch (ParseException ex)
            {
                string supportedFields = "Title, Category, Status, CreatedAt, Employee.FullName. (Thêm ' asc' hoặc ' desc')";
                return CreateErrorResponse(400, $"Lỗi sắp xếp: Tên cột '{sort}' không hợp lệ. Hỗ trợ sắp xếp theo: {supportedFields}");
            }
            catch (Exception)
            {
                throw;
            }

            // 5. Trả về Response
            return Ok(new
            {
                statusCode = 200,
                message = responseMessage,
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
                        result = requestList
                    }
                },
                success = true
            });
        }

        // =================================================================
        //                  API 3: DUYỆT HOẶC TỪ CHỐI - ĐÃ CHỈNH SỬA
        // =================================================================
        [HttpPut("process/{requestId}")]
        public async Task<IActionResult> ProcessRequest(Guid requestId, [FromBody] ProcessRequestDto request)
        {
            // 1. Kiểm tra đầu vào (Dùng Enum)
            if (request.ApproverUserId == Guid.Empty ||
                (request.NewStatus != RequestStatus.approved && request.NewStatus != RequestStatus.rejected))
            {
                return CreateErrorResponse(400, "Thiếu ID người duyệt hoặc NewStatus không hợp lệ (Chỉ chấp nhận 'approved' hoặc 'rejected').");
            }

            // KIỂM TRA APPROVERUSER TỒN TẠI
            var approverExists = await _context.Users.AnyAsync(u => u.Id == request.ApproverUserId);
            if (!approverExists)
            {
                return CreateErrorResponse(404, "ID người duyệt (ApproverUserId) không tồn tại trong hệ thống User.");
            }

            // 2. Tìm yêu cầu
            var currentRequest = await _context.Requests
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (currentRequest == null)
            {
                return CreateErrorResponse(404, "Không tìm thấy yêu cầu này.");
            }
            if (currentRequest.Status != RequestStatus.pending) // Dùng Enum
            {
                return CreateErrorResponse(400, $"Yêu cầu đã ở trạng thái '{currentRequest.Status}'. Không thể xử lý lại.");
            }
            if (!currentRequest.FromDate.HasValue)
            {
                return CreateErrorResponse(400, "Yêu cầu thiếu ngày bắt đầu (FromDate).");
            }

            string finalMessage;

            // 3. XỬ LÝ TỪ CHỐI (REJECTED) (Dùng Enum)
            if (request.NewStatus == RequestStatus.rejected)
            {
                currentRequest.Status = RequestStatus.rejected;
                currentRequest.ApprovedBy = request.ApproverUserId;

                string rejectionNote = $"[REJECTED by {request.ApproverUserId}]: {request.Reason ?? "Không có lý do cụ thể."}";
                currentRequest.Description = (currentRequest.Description ?? "") + "\n--- " + rejectionNote;

                finalMessage = $"Đã từ chối yêu cầu {currentRequest.Category} thành công.";

                // CẬP NHẬT ATTENDANCE KHI BỊ TỪ CHỐI (Dùng Enum và DateOnly)
                if (currentRequest.Category == RequestCategory.leave)
                {
                    DateOnly startDate = currentRequest.FromDate.Value;
                    DateOnly endDate = currentRequest.ToDate ?? startDate;

                    // Chuyển sang DateTime để lặp (hoặc dùng loop DateOnly)
                    DateTime loopStart = startDate.ToDateTime(TimeOnly.MinValue);
                    DateTime loopEnd = endDate.ToDateTime(TimeOnly.MinValue);

                    for (DateTime date = loopStart; date <= loopEnd; date = date.AddDays(1))
                    {
                        if (date.DayOfWeek == DayOfWeek.Sunday) continue;

                        DateOnly currentDayOnly = DateOnly.FromDateTime(date);

                        var attendance = await _context.Attendances
                            .FirstOrDefaultAsync(a => a.EmployeeId == currentRequest.EmployeeId &&
                                                        a.Date == currentDayOnly && // Dùng DateOnly
                                                        a.Status == AttendanceStatus.absent); // Dùng Enum

                        if (attendance != null)
                        {
                            // GIỮ NGUYÊN STATUS = "absent", chỉ cập nhật NOTE
                            attendance.Note = $"Yêu cầu nghỉ phép bị từ chối: {request.Reason ?? "Không lý do."}";
                        }
                    }
                }
            }
            // 4. XỬ LÝ PHÊ DUYỆT (APPROVED) (Dùng Enum)
            else
            {
                currentRequest.Status = RequestStatus.approved;
                currentRequest.ApprovedBy = request.ApproverUserId;

                if (currentRequest.Category == RequestCategory.ot)
                {
                    // --- DUYỆT OT ---
                    if (request.ApprovedHours <= 0)
                    {
                        return CreateErrorResponse(400, "Số giờ duyệt OT phải lớn hơn 0.");
                    }
                    if (!currentRequest.FromDate.HasValue || !currentRequest.StartTime.HasValue)
                    {
                        return CreateErrorResponse(400, "Yêu cầu OT thiếu ngày hoặc giờ bắt đầu.");
                    }

                    // Tạo bản ghi trong bảng Overtimes (Giả định Overtime Model dùng DateOnly)
                    // NOTE: Giả định Overtime model có DateOnly Date
                    var newOvertime = new Overtime
                    {
                        Id = Guid.NewGuid(),
                        EmployeeId = currentRequest.EmployeeId,
                        Date = currentRequest.FromDate.Value, // Dùng DateOnly
                        Hours = request.ApprovedHours,
                        Rate = request.Rate,
                        // Thêm các thuộc tính cần thiết khác cho Overtime
                        Reason = $"OT từ {currentRequest.StartTime.Value:hh\\:mm\\:ss} | {currentRequest.Title}"
                    };
                    _context.Overtimes.Add(newOvertime);
                    finalMessage = $"Đã phê duyệt yêu cầu OT thành công. {request.ApprovedHours} giờ đã được thêm vào hệ thống tính lương.";
                }
                else if (currentRequest.Category == RequestCategory.leave)
                {
                    // --- DUYỆT NGHỈ PHÉP ---
                    DateOnly startDate = currentRequest.FromDate.Value;
                    DateOnly endDate = currentRequest.ToDate ?? startDate;
                    int daysProcessed = 0;

                    DateTime loopStart = startDate.ToDateTime(TimeOnly.MinValue);
                    DateTime loopEnd = endDate.ToDateTime(TimeOnly.MinValue);

                    for (DateTime date = loopStart; date <= loopEnd; date = date.AddDays(1))
                    {
                        if (date.DayOfWeek == DayOfWeek.Sunday) continue;

                        DateOnly currentDayOnly = DateOnly.FromDateTime(date);

                        var attendance = await _context.Attendances
                            .FirstOrDefaultAsync(a => a.EmployeeId == currentRequest.EmployeeId && a.Date == currentDayOnly);

                        if (attendance == null)
                        {
                            attendance = new Attendance
                            {
                                Id = Guid.NewGuid(),
                                EmployeeId = currentRequest.EmployeeId,
                                Date = currentDayOnly,
                                // Thêm các thuộc tính CheckIn/CheckOut nếu cần
                            };
                            _context.Attendances.Add(attendance);
                        }

                        // CHUYỂN BẢN GHI TẠM THỜI (hoặc mới) sang "leave" (Dùng Enum)
                        attendance.Status = AttendanceStatus.leave;
                        attendance.Note = $"Nghỉ phép được duyệt: {currentRequest.Title}";
                        daysProcessed++;
                    }
                    finalMessage = $"Đã phê duyệt yêu cầu nghỉ phép thành công. {daysProcessed} ngày công đã được đánh dấu là nghỉ phép có lương.";
                }
                else
                {
                    finalMessage = $"Đã phê duyệt yêu cầu {currentRequest.Category} thành công (Không cần cập nhật Attendance/Overtime).";
                }
            }

            // 5. Lưu tất cả thay đổi
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = finalMessage,
                requestId = currentRequest.Id
            });
        }
    }
}