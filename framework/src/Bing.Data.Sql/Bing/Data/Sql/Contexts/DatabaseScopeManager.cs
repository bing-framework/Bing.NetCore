using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql.Configs;

namespace Bing.Data.Sql;

/// <summary>
/// 数据库上下文作用域管理器
/// </summary>
public sealed class DatabaseScopeManager : IDatabaseScopeManager
{
    /// <summary>
    /// 数据库上下文访问器
    /// </summary>
    private readonly IDatabaseContextAccessor _databaseContextAccessor;

    /// <summary>
    /// Sql 元数据配置
    /// </summary>
    private readonly SqlMetadataOptions _options;

    /// <summary>
    /// 初始化一个<see cref="DatabaseScopeManager"/>类型的实例
    /// </summary>
    /// <param name="databaseContextAccessor">数据库上下文访问器</param>
    /// <param name="options">Sql 元数据配置</param>
    public DatabaseScopeManager(IDatabaseContextAccessor databaseContextAccessor, SqlMetadataOptions options = null)
    {
        _databaseContextAccessor = databaseContextAccessor ?? throw new ArgumentNullException(nameof(databaseContextAccessor));
        _options = options ?? new SqlMetadataOptions();
    }

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
    public IDatabaseScope Use(string dbKey, DatabaseType databaseType, DatabaseRole role = DatabaseRole.Default,
        string tenantId = null, bool readOnly = false, string mappingVersion = null)
    {
        var parent = _databaseContextAccessor.Current;
        var defaultContext = _options.DefaultDatabaseContext ?? new DatabaseContext
        {
            DbKey = ConnectionStringCollection.DefaultConnectionStringName,
            DatabaseType = DatabaseType.SqlServer
        };
        _databaseContextAccessor.Current = new DatabaseContext
        {
            DbKey = string.IsNullOrWhiteSpace(dbKey) ? defaultContext.DbKey : dbKey,
            DatabaseType = databaseType,
            Role = role,
            TenantId = tenantId ?? parent?.TenantId,
            ReadOnly = readOnly,
            MappingVersion = mappingVersion ?? parent?.MappingVersion ?? defaultContext.MappingVersion
        };
        return new DatabaseScope(_databaseContextAccessor, parent);
    }

    /// <summary>
    /// 数据库上下文作用域
    /// </summary>
    private sealed class DatabaseScope : IDatabaseScope
    {
        /// <summary>
        /// 数据库上下文访问器
        /// </summary>
        private readonly IDatabaseContextAccessor _databaseContextAccessor;

        /// <summary>
        /// 父级数据库上下文
        /// </summary>
        private readonly DatabaseContext _parent;

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 初始化一个<see cref="DatabaseScope"/>类型的实例
        /// </summary>
        /// <param name="databaseContextAccessor">数据库上下文访问器</param>
        /// <param name="parent">父级数据库上下文</param>
        public DatabaseScope(IDatabaseContextAccessor databaseContextAccessor, DatabaseContext parent)
        {
            _databaseContextAccessor = databaseContextAccessor;
            _parent = parent;
        }

        /// <summary>
        /// 释放作用域并恢复上级数据库上下文
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            _databaseContextAccessor.Current = _parent;
            _disposed = true;
        }
    }
}
