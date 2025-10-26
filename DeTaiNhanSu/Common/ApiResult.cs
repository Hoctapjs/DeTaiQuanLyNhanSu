using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DeTaiNhanSu.Common
{
    /// <summary>
    /// Chuẩn hóa response theo schema: { statusCode, message, data: [], success }
    /// </summary>
    public static class ApiResult
    {
        /// <summary>
        /// 200 OK – data có thể là mảng bất kỳ (mặc định là [])
        /// </summary>
        public static IActionResult Ok(object? data = null, string? message = null)
            => new OkObjectResult(new
            {
                statusCode = StatusCodes.Status200OK,
                message = message ?? "Thành công.",
                data = data ?? Array.Empty<object>(),
                success = true
            });

        /// <summary>
        /// 201 Created – theo schema, luôn data = []
        /// </summary>
        public static IActionResult Created(string? message = null)
            => new ObjectResult(new
            {
                statusCode = StatusCodes.Status201Created,
                message = message ?? "Tạo thành công.",
                data = Array.Empty<object>(),
                success = true
            })
            { StatusCode = StatusCodes.Status201Created };

        /// <summary>
        /// Lỗi tùy mã – theo schema, luôn data = []
        /// </summary>
        public static IActionResult Fail(int code, string message, object? data = null)
            => new ObjectResult(new
            {
                statusCode = code,
                message,
                data,
                success = false
            })
            { StatusCode = code };

        // 401 Unauthorized
        public static IActionResult Unauthorized(string? message = null)
            => Fail(StatusCodes.Status401Unauthorized, message ?? "Bạn chưa đăng nhập hoặc token không hợp lệ.");

        // 403 Forbidden
        public static IActionResult Forbidden(string? message = null)
            => Fail(StatusCodes.Status403Forbidden, message ?? "Bạn không có quyền truy cập tài nguyên này.");

        /// <summary>
        /// Trả về danh sách (đảm bảo data là mảng). Ví dụ: return ApiResult.OkList(items, "…");
        /// </summary>
        public static IActionResult OkList<T>(IEnumerable<T> items, string? message = null)
            => Ok(items?.ToArray() ?? Array.Empty<T>(), message);

        /// <summary>
        /// Gói 1 object vào data[0] (giữ đúng schema mảng). Ví dụ: return ApiResult.OkSingle(payload, "…");
        /// </summary>
        public static IActionResult OkSingle(object item, string? message = null)
            => Ok(new[] { item }, message);
    }
}
