using Bing.Aspects;

namespace Bing.Data;

/// <summary>
/// 数据库
/// </summary>
[IgnoreAspect]
public interface IDatabase : IDatabaseConnectionAccessor
{
}
