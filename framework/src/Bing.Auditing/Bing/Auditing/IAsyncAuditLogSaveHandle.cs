namespace Bing.Auditing;

/// <summary>
/// 支持等待审计保存完成的异步作用域。
/// </summary>
public interface IAsyncAuditLogSaveHandle : IAuditLogSaveHandle, IAsyncDisposable
{
}
