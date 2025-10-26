using Microsoft.AspNetCore.Mvc;

namespace DeTaiNhanSu.Common
{
    /// <summary>
    /// Extension để gọi ngắn gọn trong Controller: this.OK(...), this.FAIL(...), ...
    /// </summary>
    public static class ControllerExtensions
    {
        public static IActionResult OK(this ControllerBase _, object? data = null, string? message = null)
            => ApiResult.Ok(data, message);

        public static IActionResult OKList<T>(this ControllerBase _, IEnumerable<T> items, string? message = null)
            => ApiResult.OkList(items, message);

        public static IActionResult OKSingle(this ControllerBase _, object item, string? message = null)
            => ApiResult.OkSingle(item, message);

        public static IActionResult CREATED(this ControllerBase _, string? message = null)
            => ApiResult.Created(message);

        public static IActionResult FAIL(this ControllerBase _, int code, string message)
            => ApiResult.Fail(code, message);

        public static IActionResult UNAUTHORIZED(this ControllerBase _, string? message = null)
            => ApiResult.Unauthorized(message);

        public static IActionResult FORBIDDEN(this ControllerBase _, string? message = null)
            => ApiResult.Forbidden(message);
    }
}
