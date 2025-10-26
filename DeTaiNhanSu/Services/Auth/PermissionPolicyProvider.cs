using DeTaiNhanSu.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace DeTaiNhanSu.Services.Auth
{
    public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
    {
        // note: class không thể kế thừa, tùy biến dịch vụ có sẵn để tự động tạo policy động dựa trên permission name
            
        public DefaultAuthorizationPolicyProvider Fallback { get; }

        public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
        {
            Fallback = new DefaultAuthorizationPolicyProvider(options);
        }

        public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
        {
            return Fallback.GetDefaultPolicyAsync();
        }

        public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
        {
            return Fallback.GetFallbackPolicyAsync();
        }

        // func note:
            // khi API yêu cầu policy xxx, provider sẽ tạo policy động
            // policy sẽ yêu cầu PermissionRequirement(policyName)
            // Handler được đăng ký riêng sẽ xác thực claim của user
        public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        {
            var policy = new AuthorizationPolicyBuilder()
            .AddRequirements(new PermissionRequirement(policyName))
            .Build();

            return Task.FromResult<AuthorizationPolicy?>(policy);
        }
    }
}
