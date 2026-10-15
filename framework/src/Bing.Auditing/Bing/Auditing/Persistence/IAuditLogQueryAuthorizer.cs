using System;
using System.Threading;
using System.Threading.Tasks;

namespace Bing.Auditing.Persistence;

/// <summary>
/// 校验审计日志查询请求的调用权限。
/// </summary>
/// <remarks>授权器应校验查询租户、角色和用途，不应只依据查询对象中的字符串判断可信身份。</remarks>
public interface IAuditLogQueryAuthorizer
{
    /// <summary>
    /// 授权一次审计日志查询。
    /// </summary>
    /// <param name="query">待授权的查询条件。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示授权检查完成的异步操作。</returns>
    Task AuthorizeAsync(AuditLogQuery query, CancellationToken cancellationToken = default);
}
