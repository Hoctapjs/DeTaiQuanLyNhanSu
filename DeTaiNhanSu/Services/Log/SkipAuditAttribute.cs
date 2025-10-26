namespace DeTaiNhanSu.Services.Log
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class SkipAuditAttribute : Attribute { }
}
