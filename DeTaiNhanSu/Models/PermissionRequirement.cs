using Microsoft.AspNetCore.Authorization;

namespace DeTaiNhanSu.Models
{
    // note class: mô tả điều kiện phân quyền
    public sealed class PermissionRequirement : IAuthorizationRequirement
    {
        // thuộc tính lưu tên quyền (ex: employee.view)
        public string Permission { get; } = default!;

        public PermissionRequirement(string permission)
        {
            Permission = permission;
        }
    }
}
