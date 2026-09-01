using Bing.Aspects;

namespace Bing.Data;

/// <summary>
/// 提供数据库连接访问能力。
/// </summary>
[IgnoreAspect]
public interface IDatabase : IDatabaseConnectionAccessor
{
}
