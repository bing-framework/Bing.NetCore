using Bing.Data.Enums;

namespace Bing.Data.Sql;

/// <summary>
/// 数据库上下文作用域管理器
/// </summary>
public interface IDatabaseScopeManager
{
    /// <summary>
    /// 使用指定数据库上下文
    /// </summary>
    /// <param name="dbKey">数据库标识</param>
    /// <param name="databaseType">数据库类型</param>
    /// <param name="role">数据库角色</param>
    /// <param name="tenantId">租户标识</param>
    /// <param name="readOnly">是否只读</param>
    /// <param name="mappingVersion">映射版本</param>
    /// <returns>数据库上下文作用域</returns>
    IDatabaseScope Use(string dbKey, DatabaseType databaseType, DatabaseRole role = DatabaseRole.Default,
        string tenantId = null, bool readOnly = false, string mappingVersion = null);
}
