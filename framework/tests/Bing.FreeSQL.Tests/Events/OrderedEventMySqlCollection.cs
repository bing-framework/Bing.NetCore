using Xunit;

namespace Bing.FreeSQL.Tests.Events;

/// <summary>
/// 串行运行共享有序事件表的 MySQL 集成测试。
/// </summary>
[CollectionDefinition("MySqlOrderedEvents", DisableParallelization = true)]
public sealed class OrderedEventMySqlCollection
{
}
