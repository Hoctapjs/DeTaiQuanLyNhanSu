using DeTaiNhanSu.DbContextProject;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DeTaiNhanSu.Services.Scope
{
    public interface IDataScopeService
    {
        // Hàm này sẽ trả về DepartmentId "hợp pháp" mà user được phép xem
        Task<Guid?> GetAllowedDepartmentIdAsync(Guid? requestedDeptId, CancellationToken ct = default);
    }

    public class DataScopeService : IDataScopeService
    {
        private readonly AppDbContext _db;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public DataScopeService(AppDbContext db, IHttpContextAccessor httpContextAccessor)
        {
            _db = db;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<Guid?> GetAllowedDepartmentIdAsync(Guid? requestedDeptId, CancellationToken ct = default)
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null) return null; // Chưa đăng nhập

            // 1. Nếu là Admin hoặc HR: Được quyền xem tất cả, hoặc xem theo ý thích
            if (user.IsInRole("Admin") || user.IsInRole("HR"))
            {
                return requestedDeptId; // Tôn trọng lựa chọn của họ
            }

            // 2. Nếu là Manager : Bị ép buộc xem phòng ban của mình
            var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(userIdStr, out var userId))
            {
                // Truy vấn DB để lấy DepartmentId của Manager
                var managerDeptId = await _db.Users
                    .AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => u.Employee.DepartmentId)
                    .FirstOrDefaultAsync(ct);

                // Trả về phòng của Manager (Ghi đè requestedDeptId)    
                return managerDeptId;
            }

            // Trường hợp không xác định: Không cho xem gì cả (hoặc trả về requestedDeptId nhưng rủi ro)
            // Ở đây ta trả về một Guid rỗng để query không ra kết quả nào cho an toàn
            return Guid.Empty;
        }
    }
}
