using Bing.Data.Enums;
using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql;

/// <summary>
/// SQL Provider 解析器。
/// </summary>
/// <remarks>
/// Provider Key 是唯一身份；<see cref="DatabaseType"/> 仅用于未指定 Key 时的官方兼容映射。
/// </remarks>
public interface ISqlProviderResolver
{
    /// <summary>
    /// 解析已注册的 SQL Provider。
    /// </summary>
    /// <param name="providerKey">Provider 唯一标识，匹配时忽略大小写和首尾空白。</param>
    /// <returns>已注册的 SQL Provider。</returns>
    ISqlProvider Resolve(string providerKey);

    /// <summary>
    /// 尝试根据 Provider Key 解析已注册的 SQL Provider。
    /// </summary>
    /// <param name="providerKey">Provider 唯一标识。</param>
    /// <param name="provider">解析成功时返回 SQL Provider。</param>
    /// <returns>解析成功时返回 <c>true</c>。</returns>
    bool TryResolve(string providerKey, out ISqlProvider provider);

    /// <summary>
    /// 解析已注册的 SQL Provider。
    /// </summary>
    /// <remarks>依次使用数据源 Provider Key、上下文 Provider Key、显式 Provider；均未指定时按数据库类型查找官方兼容映射。</remarks>
    /// <param name="context">当前数据库上下文。</param>
    /// <param name="provider">调用方显式指定的 SQL Provider。</param>
    /// <param name="databaseType">上下文未包含数据源时使用的兼容数据库类型。</param>
    /// <returns>已注册的 SQL Provider。</returns>
    ISqlProvider Resolve(DatabaseContext context, ISqlProvider provider = null, DatabaseType? databaseType = null);
}
