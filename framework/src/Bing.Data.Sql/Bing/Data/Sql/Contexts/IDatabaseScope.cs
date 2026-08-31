namespace Bing.Data.Sql;

/// <summary>
/// 表示临时覆盖数据库上下文的作用域。
/// </summary>
/// <remarks>
/// 该作用域不拥有数据库连接或事务。嵌套作用域必须按创建的反向顺序释放，释放后会恢复其父上下文。
/// </remarks>
public interface IDatabaseScope : IDisposable
{
}
