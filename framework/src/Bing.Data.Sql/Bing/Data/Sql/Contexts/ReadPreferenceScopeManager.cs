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
        var context = _databaseContextAccessor.Current ?? new DatabaseContext();
        context.ReadPreference = readPreference;
        return DatabaseContextScopeStack.Enter(_databaseContextAccessor, context);
    }
}