namespace Bing.Data.Sql;

/// <summary>
/// 读取偏好作用域管理器。
/// </summary>
public sealed class ReadPreferenceScopeManager : IReadPreferenceScopeManager
{
    /// <summary>
    /// 数据库上下文访问器。
    /// </summary>
    private readonly IDatabaseContextAccessor _databaseContextAccessor;

    /// <summary>
    /// 初始化一个<see cref="ReadPreferenceScopeManager"/>类型的实例。
    /// </summary>
    /// <param name="databaseContextAccessor">数据库上下文访问器。</param>
    public ReadPreferenceScopeManager(IDatabaseContextAccessor databaseContextAccessor)
    {
        _databaseContextAccessor = databaseContextAccessor ??
            throw new ArgumentNullException(nameof(databaseContextAccessor));
    }

    /// <inheritdoc />
    public IDatabaseScope Use(SqlReadPreference readPreference)
    {
        var parent = _databaseContextAccessor.Current;
        var context = _databaseContextAccessor.Current ?? new DatabaseContext();
        context.ReadPreference = readPreference;
        _databaseContextAccessor.Current = context;
        return new ReadPreferenceScope(_databaseContextAccessor, parent);
    }

    /// <summary>
    /// 读取偏好作用域。
    /// </summary>
    private sealed class ReadPreferenceScope : IDatabaseScope
    {
        /// <summary>
        /// 数据库上下文访问器。
        /// </summary>
        private readonly IDatabaseContextAccessor _databaseContextAccessor;

        /// <summary>
        /// 父级数据库上下文。
        /// </summary>
        private readonly DatabaseContext _parent;

        /// <summary>
        /// 是否已释放。
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 初始化一个<see cref="ReadPreferenceScope"/>类型的实例。
        /// </summary>
        /// <param name="databaseContextAccessor">数据库上下文访问器。</param>
        /// <param name="parent">父级数据库上下文。</param>
        public ReadPreferenceScope(IDatabaseContextAccessor databaseContextAccessor, DatabaseContext parent)
        {
            _databaseContextAccessor = databaseContextAccessor;
            _parent = parent;
        }

        /// <summary>
        /// 释放作用域并恢复父级读取偏好。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _databaseContextAccessor.Current = _parent;
        }
    }
}