using Microsoft.AspNetCore.Authorization;

namespace DeTaiNhanSu.Models
{
    // note class: xử lý điều kiện phân quyền, kiểm tra xem người dùng có quyền phù hợp hay ko
        // kế thừa AuthorizationHandler<PermissionRequirement> : chuyên xử lý những yêu cầu thuộc loại PermissionRequirement
    public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
    {
        protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement req)
        {
            // lấy list claim từ JWT token của user
            // lọc type permission
            var ok = context.User.Claims.Any(c =>
                c.Type == "permission" &&
                string.Equals(c.Value, req.Permission, StringComparison.OrdinalIgnoreCase));

            // so sánh quyền có trùng với trong req ko
            if (ok) context.Succeed(req);
            else context.Fail(); // return 403

            return Task.CompletedTask;
        }
    }
}
