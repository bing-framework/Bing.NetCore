namespace Bing.Auditing;

/// <summary>
/// 禁用审计 特性（已迁移至 Bing.Auditing.Contracts）
/// </summary>
[Obsolete("DisableAuditingAttribute has been moved to Bing.Auditing.Contracts. " +
          "Reference Bing.Auditing.Contracts and use Bing.Auditing.DisableAuditingAttribute from there.", false)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property)]
public class DisableAuditingAttribute : Attribute
{
}
