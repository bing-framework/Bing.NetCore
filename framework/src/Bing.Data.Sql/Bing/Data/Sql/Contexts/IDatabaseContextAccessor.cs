namespace Bing.Data.Sql;

/// <summary>
/// 数据库上下文访问器
/// </summary>
public interface IDatabaseContextAccessor
{
    /// <summary>
    /// 获取或设置当前异步执行流的数据库上下文；设置为 <c>null</c> 会清除当前上下文。
    /// </summary>
    /// <value>当前异步执行流的数据库上下文；没有上下文时为 <c>null</c>。</value>
    DatabaseContext Current { get; set; }
}
