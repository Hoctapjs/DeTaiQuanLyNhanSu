using Microsoft.AspNetCore.Authorization;

namespace DeTaiNhanSu.Services.Auth
{
    // note class:
        // chỉ định attribute có thể sùng trên method và class
        // kế thừa từ 
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
    public sealed class HasPermissionAttribute : AuthorizeAttribute
    {
        // note: class ko thể kế thừa, bảo vệ API theo quyền cụ thể
        // khi controller có [HasPermission("employee.view")] thì middle sẽ kiểm tra user có claim permission=employee.view hay ko

        // Policy là thuốc tính kế thừa từ AuthorizeAttribute
        public HasPermissionAttribute(string permission)
        {
            //Policy = $"perm: {permission}";
            Policy = permission;
        }
    }
}
