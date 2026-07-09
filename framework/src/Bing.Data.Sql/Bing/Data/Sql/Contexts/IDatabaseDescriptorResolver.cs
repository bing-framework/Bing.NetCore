namespace Bing.Data.Sql;

/// <summary>
/// 数据库描述解析器
/// </summary>
public interface IDatabaseDescriptorResolver
{
    /// <summary>
    /// 解析数据库描述信息
    /// </summary>
    /// <param name="context">数据库上下文</param>
    /// <returns>数据库描述信息</returns>
    DatabaseDescriptor Resolve(DatabaseContext context);
}