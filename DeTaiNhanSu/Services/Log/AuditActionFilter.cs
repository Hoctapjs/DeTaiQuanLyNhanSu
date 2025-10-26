using Microsoft.AspNetCore.Mvc.Filters;

namespace DeTaiNhanSu.Services.Log
{
    public sealed class AuditActionFilter : IAsyncActionFilter
    {
        private readonly IAuditScope _scope;
        public AuditActionFilter(IAuditScope scope) => _scope = scope;

        public async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate next)
        {
            var skip = ctx.ActionDescriptor.EndpointMetadata.OfType<SkipAuditAttribute>().Any();
            var prev = _scope.Suppress;
            _scope.Suppress = skip || prev;   // nếu đã suppress rồi thì giữ nguyên
            try { await next(); }
            finally { _scope.Suppress = prev; }
        }
    }
}
