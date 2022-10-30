using System;

namespace Bing.Auditing;

/// <summary>
/// 禁用审计 特性
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property)]
public class DisableAuditingAttribute : Attribute
{
}