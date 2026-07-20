using Bing.Data.Enums;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// SQL 对象名称能力提供器。
/// </summary>
public interface ISqlObjectNameCapabilityProvider
{
    /// <summary>
    /// 获取数据库类型支持的对象名称能力。
    /// </summary>
    /// <param name="databaseType">数据库类型。</param>
    /// <returns>对象名称能力。</returns>
    SqlObjectNameCapabilities GetCapabilities(DatabaseType? databaseType);
}