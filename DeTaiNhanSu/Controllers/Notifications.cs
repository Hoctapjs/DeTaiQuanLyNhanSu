using DeTaiNhanSu.DbContextProject;
using DeTaiNhanSu.Models;
using DeTaiNhanSu.Services.Notification;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq.Dynamic.Core; // <--- THÊM USING NÀY
using System.Linq.Dynamic.Core.Exceptions;
using NotificationModel = DeTaiNhanSu.Models.Notification; // Add this alias to resolve ambiguity

namespace DeTaiNhanSu.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class Notifications : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly AppDbContext _context; // Đã inject _context

        // Helper method để lấy thời gian Việt Nam
        private static DateTime GetVietnamTime()
        {
            var vietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vietnamTimeZone);
        }

        public Notifications(INotificationService notificationService, AppDbContext context)
        {
            _notificationService = notificationService;
            _context = context;
        }
        // =================================================================
        // ✅ API MỚI: LẤY DANH SÁCH TỪ BẢNG NOTIFICATIONS (BẢNG GỐC)
        // =================================================================
        [HttpGet] // Đặt một tên route mới
        [Authorize(Roles = "Admin, HR")] // (Đề xuất: Chỉ Admin nên xem bảng gốc này)
        public async Task<IActionResult> GetRootNotifications(
            [FromQuery] string? q,
            [FromQuery] int current = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string sort = "CreatedAt desc") // Sắp xếp theo ngày tạo mới nhất
        {
            try
            {
                // 1. Base Query: Chỉ truy vấn bảng Notifications
                var initialQuery = _context.Notifications
                                        .Include(n => n.Actor)
                                            .ThenInclude(a => a.Employee)
                                        .AsQueryable();

                IQueryable<NotificationModel> query = initialQuery;

                // 2. Filtering (Lọc)
                if (!string.IsNullOrEmpty(q))
                {
                    string searchTrimmed = q.Trim();
                    string searchLower = searchTrimmed.ToLower();
                    bool isGuid = Guid.TryParse(searchTrimmed, out Guid searchGuid);

                    query = query.Where(n =>
                        // Tìm bằng ID (nếu q là Guid)
                        (isGuid && n.Id == searchGuid) ||

                        // Tìm bằng văn bản
                        n.Title.ToLower().Contains(searchLower) ||
                        n.Content.ToLower().Contains(searchLower) ||
                        n.Type.ToLower().Contains(searchLower) ||

                        // Tìm bằng tên người tạo (Actor)
                        (n.Actor != null && n.Actor.Employee != null &&
                         n.Actor.Employee.FullName.ToLower().Contains(searchLower))
                    );

                    // Bắt lỗi nếu không tìm thấy gì (giống hàm cũ)
                    if (await initialQuery.AnyAsync() && !await query.AnyAsync())
                    {
                        return BadRequest(new
                        {
                            statusCode = 400,
                            message = $"Không tìm thấy thông báo nào cho từ khóa '{searchTrimmed}'.",
                            success = false
                        });
                    }
                }

                // 3. Pagination (Phân trang)
                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                List<dynamic> notificationList = new List<dynamic>();

                // 4. Sorting & Selection (Sắp xếp & Lấy dữ liệu)
                try
                {
                    // Xử lý các alias (bí danh) cho sort
                    string sortQuery = sort;
                    if (sortQuery.Contains("actorName", StringComparison.OrdinalIgnoreCase))
                    {
                        sortQuery = sortQuery.Replace("actorName", "Actor.Employee.FullName", StringComparison.OrdinalIgnoreCase);
                    }
                    if (sortQuery.Contains("Id", StringComparison.OrdinalIgnoreCase))
                    {
                        sortQuery = sortQuery.Replace("Id", "CreatedAt", StringComparison.OrdinalIgnoreCase);
                    }

                    var tempList = await query
                        .OrderBy(sortQuery) // Dùng System.Linq.Dynamic.Core
                        .Skip((current - 1) * pageSize)
                        .Take(pageSize)
                        .Select(n => new // Định hình dữ liệu trả về
                        {
                            id = n.Id,
                            type = n.Type,
                            title = n.Title,
                            content = n.Content,
                            createdAt = n.CreatedAt,
                            actorId = n.ActorId,
                            actorName = (n.Actor != null && n.Actor.Employee != null)
                                        ? n.Actor.Employee.FullName
                                        : (n.ActorId == null ? "System" : "Unknown"),
                            actionUrl = n.ActionUrl
                            // Lưu ý: Không có 'readAt' hoặc 'userId' vì đây là bảng gốc
                        })
                        .ToListAsync();

                    notificationList.AddRange(tempList.Cast<dynamic>());
                }
                // Bắt lỗi sắp xếp (giống hàm cũ)
                catch (ParseException)
                {
                    string supportedFields = "Hỗ trợ sắp xếp theo: CreatedAt (hoặc Id), Title, Type, actorName.";
                    return BadRequest(new
                    {
                        statusCode = 400,
                        message = $"Lỗi sắp xếp: Tên cột '{sort}' không hợp lệ. {supportedFields}",
                        success = false
                    });
                }

                // 5. Trả về cấu trúc (giống hàm cũ)
                return Ok(new
                {
                    statusCode = 200,
                    message = $"Tìm thấy {totalCount} thông báo.",
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
                            result = notificationList
                        }
                    },
                    success = true
                });
            }
            // Bắt lỗi chung (ngoại lệ)
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    StatusCode = 500,
                    Message = $"Đã xảy ra lỗi máy chủ nội bộ: {ex.Message}"
                });
            }
        } // Kết thúc hàm GetRootNotifications

        // =================================================================
        // PHẦN SỬA LỖI [HttpGet("list")]
        // =================================================================
        [HttpGet("list")]
        public async Task<IActionResult> GetNotifications(
              [FromQuery] string? q,
              [FromQuery] int current = 1,
              [FromQuery] int pageSize = 20,
              [FromQuery] string sort = "Id desc")
        {
            var initialQuery = _context.UserNotifications
                .Include(un => un.Notification)
                    .ThenInclude(n => n.Actor)
                        .ThenInclude(a => a.Employee)
                .Include(un => un.User)
                    .ThenInclude(u => u.Employee)
                .AsQueryable();

            IQueryable<UserNotification> query = initialQuery;

            if (!string.IsNullOrEmpty(q))
            {
                string searchTrimmed = q.Trim();
                string searchLower = searchTrimmed.ToLower();

                // Cố gắng parse 'q' thành GUID
                bool isGuid = Guid.TryParse(searchTrimmed, out Guid searchGuid);

                // === BẮT ĐẦU SỬA LOGIC TÌM KIẾM ===
                query = query.Where(un =>
                    // 1. Nếu 'q' LÀ một GUID, kiểm tra khớp chính xác với NotificationId HOẶC UserId
                    (isGuid && (un.NotificationId == searchGuid || un.UserId == searchGuid)) ||

                    // 2. Kiểm tra các trường văn bản
                    un.Notification.Title.ToLower().Contains(searchLower) ||
                    un.Notification.Content.ToLower().Contains(searchLower) ||
                    un.Notification.Type.ToLower().Contains(searchLower) ||
                    (un.User != null && un.User.Employee != null && un.User.Employee.FullName.ToLower().Contains(searchLower)) ||

                    // 3. (MỚI) Tìm kiếm 'q' (dạng text) bên trong chuỗi UserId
                    un.UserId.ToString().Contains(searchLower)
                );
                // === KẾT THÚC SỬA LOGIC TÌM KIẾM ===

                if (await initialQuery.AnyAsync() && !await query.AnyAsync())
                {
                    string searchNote = Guid.TryParse(searchTrimmed, out _) ? $"ID '{searchTrimmed}'" : $"từ khóa '{searchTrimmed}'";
                    return BadRequest(new
                    {
                        statusCode = 400,
                        message = $"Không tìm thấy kết quả nào cho {searchNote}. Vui lòng tìm kiếm theo: ID, UserID, Tiêu đề, Nội dung, Loại, hoặc Tên người nhận.",
                        success = false
                    });
                }
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            List<dynamic> notificationList = new List<dynamic>();

            try
            {
                // SỬA: Xử lý sort cho UserNotifications
                string sortQuery = sort;
                if (sortQuery.Contains("userName", StringComparison.OrdinalIgnoreCase))
                {
                    sortQuery = sortQuery.Replace("userName", "User.Employee.FullName", StringComparison.OrdinalIgnoreCase);
                }
                if (sortQuery.Contains("Id", StringComparison.OrdinalIgnoreCase))
                {
                    // Ưu tiên sắp xếp theo thời gian tạo (CreatedAt) của thông báo
                    sortQuery = sortQuery.Replace("Id", "Notification.CreatedAt", StringComparison.OrdinalIgnoreCase);
                }
                // Nếu 'sort' mặc định là "Id desc", nó sẽ trở thành "Notification.CreatedAt desc"

                var tempList = await query
                    .OrderBy(sortQuery) // Sử dụng logic sort đã sửa
                    .Skip((current - 1) * pageSize)
                    .Take(pageSize)
                    .Select(un => new
                    {
                        id = un.NotificationId,
                        userId = un.UserId,
                        userName = (un.User != null && un.User.Employee != null) ? un.User.Employee.FullName : "N/A",
                        type = un.Notification.Type,
                        title = un.Notification.Title,
                        content = un.Notification.Content,
                        readAt = un.ReadAt, // Từ UserNotification
                        createdAt = un.Notification.CreatedAt, // Từ Notification
                        actorId = un.Notification.ActorId,
                        actorName = (un.Notification.Actor != null && un.Notification.Actor.Employee != null)
                            ? un.Notification.Actor.Employee.FullName
                            : (un.Notification.ActorId == null ? "System" : "Unknown"),
                        actionUrl = un.Notification.ActionUrl
                    })
                    .ToListAsync();

                notificationList.AddRange(tempList.Cast<dynamic>());
            }
            catch (ParseException ex)
            {
                string supportedFields = "Hỗ trợ sắp xếp theo: CreatedAt (hoặc Id), Title, Type, userName, ReadAt. (Thêm ' asc' hoặc ' desc')";
                return BadRequest(new
                {
                    statusCode = 400,
                    message = $"Lỗi sắp xếp: Tên cột '{sort}' không hợp lệ. {supportedFields}",
                    success = false
                });
            }
            catch (Exception)
            {
                throw;
            }

            return Ok(new
            {
                statusCode = 200,
                message = $"Tìm thấy {totalCount} thông báo.",
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
                        result = notificationList
                    }
                },
                success = true
            });
        }

        [HttpPut("mark-as-read/{notificationId}")]
        public async Task<IActionResult> MarkAsRead(Guid notificationId, [FromQuery] Guid userId)
        {
            try
            {
                var userNotification = await _context.UserNotifications
                    .FirstOrDefaultAsync(un => un.NotificationId == notificationId && un.UserId == userId);

                if (userNotification == null)
                {
                    return NotFound(new { Success = false, Message = "Notification not found for this user" });
                }

                userNotification.ReadAt = GetVietnamTime(); // Sử dụng thời gian Việt Nam
                await _context.SaveChangesAsync();

                return Ok(new { Success = true, Message = "Notification marked as read" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = $"Error: {ex.Message}" });
            }
        }


        // ==========================================================
        // API UPDATE VỚI RÀNG BUỘC VÀ STATUSCODE (Giữ nguyên)
        // (Giờ hàm này sẽ không báo lỗi `_notificationService` nữa)
        // ==========================================================
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin, HR")]
        public async Task<IActionResult> UpdateNotification(Guid id, [FromBody] UpdateNotificationRequest request)
        {
            try
            {
                // Ràng buộc: Title/Content luôn bắt buộc
                if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
                {
                    return BadRequest(new { Success = false, StatusCode = 400, Message = "Yêu cầu thất bại: Title và Content không được để trống." });
                }

                // === GỌI HÀM "THÔNG MINH" DUY NHẤT ===
                // Service sẽ tự động xử lý logic (Ưu tiên UserId, bỏ qua TargetUserIds)
                var result = await _notificationService.UpdateNotificationAsync(id, request);

                if (!result)
                {
                    return NotFound(new
                    {
                        Success = false,
                        StatusCode = 404,
                        Message = $"Không tìm thấy thông báo (ID: {id}) hoặc liên kết người dùng (User: {request.UserId})."
                    });
                }

                return Ok(new { Success = true, StatusCode = 200, Message = "Cập nhật thông báo thành công." });
            }
            catch (DbUpdateException dbEx)
            {
                return StatusCode(500, new { Success = false, StatusCode = 500, Message = $"Lỗi cơ sở dữ liệu: {dbEx.InnerException?.Message ?? dbEx.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, StatusCode = 500, Message = $"Đã xảy ra lỗi máy chủ nội bộ: {ex.Message}" });
            }
        }  
       
        // Endpoint để tạo thông báo HR tùy chỉnh
        [HttpPost("create-hr")]
        [Authorize(Roles = "HR")]
        public async Task<IActionResult> CreateHRNotification([FromBody] CreateNotificationRequest request)
        {
            try
            {
                // Kiểm tra dữ liệu đầu vào trước khi tạo notification
                if (string.IsNullOrWhiteSpace(request.Title))
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message = "Title is required"
                    });
                }

                if (string.IsNullOrWhiteSpace(request.Content))
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message = "Content is required"
                    });
                }

                // Kiểm tra ActorId có tồn tại trong database không (nếu được cung cấp)
                if (request.ActorId.HasValue)
                {
                    var actorExists = await _context.Users.AnyAsync(u => u.Id == request.ActorId.Value);
                    if (!actorExists)
                    {
                        return BadRequest(new
                        {
                            Success = false,
                            Message = $"Actor with ID {request.ActorId.Value} does not exist"
                        });
                    }
                }

                // Kiểm tra TargetUserIds có tồn tại trong database không (nếu được cung cấp)
                if (request.TargetUserIds != null && request.TargetUserIds.Any())
                {
                    var existingUserIds = await _context.Users
                        .Where(u => request.TargetUserIds.Contains(u.Id))
                        .Select(u => u.Id)
                        .ToListAsync();

                    var nonExistentUserIds = request.TargetUserIds.Except(existingUserIds).ToList();
                    if (nonExistentUserIds.Any())
                    {
                        return BadRequest(new
                        {
                            Success = false,
                            Message = $"The following user IDs do not exist: {string.Join(", ", nonExistentUserIds)}"
                        });
                    }
                }

                var notification = new NotificationModel
                {
                    Id = Guid.NewGuid(),
                    Type = request.Type ?? "general",
                    Title = request.Title,
                    Content = request.Content,
                    CreatedAt = GetVietnamTime(), // Sử dụng thời gian Việt Nam
                    ActorId = request.ActorId,
                    ActionUrl = request.ActionUrl
                };

                await _notificationService.SendHRNotificationAsync(notification, request.TargetUserIds);

                return Ok(new
                {
                    Success = true,
                    Message = "Notification created successfully",
                    NotificationId = notification.Id
                });
            }
            catch (DbUpdateException dbEx)
            {
                // Log chi tiết lỗi database
                var innerMessage = dbEx.InnerException?.Message ?? "No inner exception";
                var fullMessage = $"Database error: {dbEx.Message}. Inner: {innerMessage}";

                return BadRequest(new
                {
                    Success = false,
                    Message = fullMessage,
                    Details = new
                    {
                        Exception = dbEx.GetType().Name,
                        InnerException = dbEx.InnerException?.GetType().Name,
                        StackTrace = dbEx.StackTrace
                    }
                });
            }
            catch (Exception ex)
            {
                // Log chi tiết lỗi chung
                var innerMessage = ex.InnerException?.Message ?? "No inner exception";
                var fullMessage = $"Error creating notification: {ex.Message}. Inner: {innerMessage}";

                return BadRequest(new
                {
                    Success = false,
                    Message = fullMessage,
                    Details = new
                    {
                        Exception = ex.GetType().Name,
                        InnerException = ex.InnerException?.GetType().Name,
                        StackTrace = ex.StackTrace
                    }
                });
            }
        }

        // Endpoint đơn giản để tạo thông báo lương
        [HttpPost("create-payroll")]
        [Authorize(Roles = "HR")]
        public async Task<IActionResult> CreatePayrollNotification([FromBody] SimpleNotificationRequest request)
        {
            try
            {
                await _notificationService.SendPayrollNotificationAsync(
                    request.Title,
                    request.Content,
                    request.TargetUserIds
                );

                return Ok(new
                {
                    Success = true,
                    Message = "Payroll notification sent successfully"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message = $"Error sending payroll notification: {ex.Message}"
                });
            }
        }

        // Endpoint để tạo thông báo chấm công
        [HttpPost("create-attendance")]
        [Authorize(Roles = "HR")]
        public async Task<IActionResult> CreateAttendanceNotification([FromBody] SimpleNotificationRequest request)
        {
            try
            {
                await _notificationService.SendAttendanceNotificationAsync(
                    request.Title,
                    request.Content,
                    request.TargetUserIds
                );

                return Ok(new
                {
                    Success = true,
                    Message = "Attendance notification sent successfully"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message = $"Error sending attendance notification: {ex.Message}"
                });
            }
        }

        // Endpoint để tạo thông báo nghỉ phép
        [HttpPost("create-leave-request")]
        [Authorize(Roles = "HR")]
        public async Task<IActionResult> CreateLeaveRequestNotification([FromBody] SimpleNotificationRequest request)
        {
            try
            {
                await _notificationService.SendLeaveRequestNotificationAsync(
                    request.Title,
                    request.Content,
                    request.TargetUserIds
                );

                return Ok(new
                {
                    Success = true,
                    Message = "Leave request notification sent successfully"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message = $"Error sending leave request notification: {ex.Message}"
                });
            }
        }

        // Endpoint demo để test nhanh - chỉ cần bấm vào
        [HttpGet("test-notification")]
        public async Task<IActionResult> TestNotification()
        {
            try
            {
                await _notificationService.SendHRNotificationAsync(new NotificationModel
                {
                    Id = Guid.NewGuid(),
                    Type = "test",
                    Title = "Test Notification",
                    Content = "This is a test notification created from the endpoint!",
                    CreatedAt = GetVietnamTime(), // Sử dụng thời gian Việt Nam
                    ActorId = Guid.Parse("95149717-529B-499C-82D2-1CC47D0B01C9"),
                    ActionUrl = null // Hoặc có thể gán URL test
                });

                return Ok(new
                {
                    Success = true,
                    Message = "Test notification sent successfully!"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message = $"Error sending test notification: {ex.Message}"
                });
            }
        }
    }

    // DTO classes for request data
    public class CreateNotificationRequest
    {
        public string? Type { get; set; }
        public string Title { get; set; } = default!;
        public string Content { get; set; } = default!;
        public Guid? ActorId { get; set; }
        public string? ActionUrl { get; set; } // Đổi thành nullable để tùy chọn
        public List<Guid>? TargetUserIds { get; set; }
    }

    public class SimpleNotificationRequest
    {
        public string Title { get; set; } = default!;
        public string Content { get; set; } = default!;
        public List<Guid>? TargetUserIds { get; set; }
    }
    // ✅ THÊM DTO CHO REQUEST UPDATE
    public class UpdateNotificationRequest
    {
        // Bắt buộc
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;

        // Tùy chọn (nếu null, service sẽ bỏ qua và giữ giá trị cũ)
        public string? Type { get; set; }
        public Guid? ActorId { get; set; }
        public string? ActionUrl { get; set; }

        // Tùy chọn (nếu null, service sẽ bỏ qua và giữ nguyên)
        // Nếu là list rỗng [], service sẽ xóa hết người nhận
        public List<Guid>? TargetUserIds { get; set; }
        public Guid? UserId { get; set; }
    }

}