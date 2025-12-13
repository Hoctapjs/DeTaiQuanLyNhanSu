using DeTaiNhanSu.DbContextProject;
using DeTaiNhanSu.Enums;
using DeTaiNhanSu.Models;
using Microsoft.EntityFrameworkCore;

namespace DeTaiNhanSu.Services
{
    public interface IAttendanceService
    {
        Task MarkAbsentLogicAsync();
        Task AutoCheckoutLogicAsync();
    }

    public class AttendanceService : IAttendanceService
    {
        private readonly AppDbContext _context;

        public AttendanceService(AppDbContext context)
        {
            _context = context;
        }

        // Helper: Lấy giờ Việt Nam
        private (DateOnly Date, TimeOnly Time) GetVnTime()
        {
            var vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            var vnNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vnTimeZone);
            return (DateOnly.FromDateTime(vnNow), TimeOnly.FromDateTime(vnNow));
        }

        // Helper: Kiểm tra xem nhân viên có đang nghỉ phép đã duyệt không
        private async Task<bool> IsDayApprovedForLeave(Guid empId, DateOnly date)
        {
            return await _context.Requests
                .AnyAsync(r => r.EmployeeId == empId
                            && r.Status == RequestStatus.approved
                            && r.Category == RequestCategory.leave
                            && r.FromDate.HasValue && r.FromDate.Value <= date
                            && r.ToDate.HasValue && r.ToDate.Value >= date);
        }

        // ====================================================================
        // 1. LOGIC ĐÁNH VẮNG (MARK ABSENT)
        // ====================================================================
        public async Task MarkAbsentLogicAsync()
        {
            var (today, vnNowTime) = GetVnTime();
            Console.WriteLine($"[Hangfire - MarkAbsent] Bắt đầu quét lúc {vnNowTime} ngày {today}...");

            // 1. Lấy cấu hình từ GlobalSettings
            var settings = await _context.GlobalSettings.AsNoTracking().ToListAsync();

            // Lấy giờ ngưỡng (Mặc định 11:00 nếu chưa cấu hình)
            var thresholdStr = settings.FirstOrDefault(s => s.Key == "ABSENT_MARK_THRESHOLD_TIME")?.Value ?? "11:00:00";
            if (!TimeOnly.TryParse(thresholdStr, out var absentThresholdTime))
                absentThresholdTime = new TimeOnly(11, 0, 0);

            // Lấy ngày nghỉ cuối tuần
            var weekendStr = settings.FirstOrDefault(s => s.Key == "WEEKEND_DAYS")?.Value ?? "Saturday,Sunday";
            var weekendDays = weekendStr.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(s => Enum.TryParse(s, true, out DayOfWeek day) ? (DayOfWeek?)day : null)
                                        .Where(d => d.HasValue).Select(d => d.Value).ToList();

            // 2. Kiểm tra nếu hôm nay là cuối tuần -> Bỏ qua
            //if (weekendDays.Contains(today.DayOfWeek))
            //{
            //    Console.WriteLine($"[Hangfire - MarkAbsent] Hôm nay là {today.DayOfWeek} (Cuối tuần). Bỏ qua.");
            //    return;
            //}

            // 3. Quét danh sách nhân viên đang hoạt động
            var activeEmployees = await _context.Employees.Where(e => e.Status == EmployeeStatus.active).ToListAsync();
            int markedCount = 0;

            foreach (var emp in activeEmployees)
            {
                // Lấy lịch làm việc hôm nay
                var schedule = await _context.WorkSchedules
                    .AsNoTracking()
                    .Include(ws => ws.ShiftTemplate)
                    .FirstOrDefaultAsync(ws => ws.EmployeeId == emp.Id && ws.Date == today);

                // Nếu không có lịch làm việc -> Bỏ qua
                if (schedule == null) continue;

                // CHỈ XỬ LÝ nếu: 
                // (Giờ hiện tại >= Giờ ngưỡng) VÀ (Ca làm việc đã bắt đầu trước giờ ngưỡng)
                if (vnNowTime >= absentThresholdTime && schedule.ShiftTemplate.StartTime <= absentThresholdTime)
                {
                    // Kiểm tra xem đã có phép duyệt chưa
                    bool isApprovedLeave = await IsDayApprovedForLeave(emp.Id, today);
                    if (isApprovedLeave) continue; // Có phép thì không đánh vắng

                    // Kiểm tra bản ghi chấm công
                    var att = await _context.Attendances.FirstOrDefaultAsync(a => a.EmployeeId == emp.Id && a.Date == today);

                    // Nếu chưa có bản ghi HOẶC bản ghi chưa check-in
                    if (att == null || att.CheckIn == null)
                    {
                        if (att == null)
                        {
                            // Tạo mới bản ghi Vắng
                            _context.Attendances.Add(new Attendance
                            {
                                Id = Guid.NewGuid(),
                                EmployeeId = emp.Id,
                                Date = today,
                                Status = AttendanceStatus.absent,
                                Note = $"Vắng (Ca {schedule.ShiftTemplate.Code} - Auto System {vnNowTime:HH:mm})"
                            });
                        }
                        else
                        {
                            // Cập nhật bản ghi rỗng thành Vắng
                            att.Status = AttendanceStatus.absent;
                            att.Note = $"Vắng (Ca {schedule.ShiftTemplate.Code} - Auto System {vnNowTime:HH:mm})";
                        }
                        markedCount++;
                    }
                }
            }

            await _context.SaveChangesAsync();
            Console.WriteLine($"[Hangfire - MarkAbsent] Hoàn tất. Đã đánh vắng: {markedCount} nhân viên.");
        }

        // ====================================================================
        // 2. LOGIC TỰ ĐỘNG CHECKOUT (AUTO CHECKOUT)
        // ====================================================================
        public async Task AutoCheckoutLogicAsync()
        {
            var (today, vnNowTime) = GetVnTime();
            Console.WriteLine($"[Hangfire - AutoCheckout] Bắt đầu quét lúc {vnNowTime} ngày {today}...");

            // 1. Lấy cấu hình giờ checkout
            var settingVal = await _context.GlobalSettings
                .Where(s => s.Key == "AUTO_CHECKOUT_TIME")
                .Select(s => s.Value)
                .FirstOrDefaultAsync();

            if (!TimeOnly.TryParse(settingVal, out var autoCheckoutTime))
                autoCheckoutTime = new TimeOnly(23, 59, 0); // Mặc định

            // 2. Xử lý trường hợp chạy sau nửa đêm (ví dụ Job chạy lúc 00:01 sáng hôm sau)
            // Nếu giờ hiện tại nhỏ hơn 4 giờ sáng, ta hiểu là đang checkout cho NGÀY HÔM QUA
            DateOnly targetDate = today;
            if (vnNowTime.Hour < 4)
            {
                targetDate = today.AddDays(-1);
                Console.WriteLine($"[Hangfire - AutoCheckout] Phát hiện chạy rạng sáng. Đang xử lý cho ngày: {targetDate}");
            }

            // 3. Tìm các bản ghi chưa checkout
            // Điều kiện: Đúng ngày, Chưa checkout, Không phải vắng
            var pendingList = await _context.Attendances
                .Where(a => a.Date == targetDate
                         && a.CheckOut == null
                         && a.Status != AttendanceStatus.absent)
                .ToListAsync();

            if (!pendingList.Any())
            {
                Console.WriteLine("[Hangfire - AutoCheckout] Không có nhân viên nào cần auto-checkout.");
                return;
            }

            // 4. Cập nhật dữ liệu
            foreach (var att in pendingList)
            {
                att.CheckOut = autoCheckoutTime;

                // Nếu đang là 'present' (có mặt) thì chuyển thành 'completed'
                // Nếu đang là 'late' (đi muộn) thì giữ nguyên 'late' (hoặc logic tùy bạn)
                if (att.Status == AttendanceStatus.present)
                {
                    att.Status = AttendanceStatus.completed;
                }

                att.Note = (att.Note ?? "") + $" | Auto-out System {autoCheckoutTime:HH:mm}";
            }

            await _context.SaveChangesAsync();
            Console.WriteLine($"[Hangfire - AutoCheckout] Hoàn tất. Đã checkout tự động cho {pendingList.Count} nhân viên.");
        }
    }
}