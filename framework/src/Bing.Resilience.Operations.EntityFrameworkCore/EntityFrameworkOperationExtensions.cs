using Bing.Resilience.Operations.Relational;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Bing.Resilience.Operations.EntityFrameworkCore;

/// <summary>将 Journal 加入 DbContext 当前的显式关系型事务；连接和提交权仍属于宿主。</summary>
public static class EntityFrameworkOperationExtensions
{
    /// <summary>必须先 BeginTransaction/UseTransaction；SaveChanges 与 Journal 入队应在同一事务内完成。</summary>
    /// <remarks>启用执行策略重试时，应由宿主对整个事务重试并使用相同业务幂等键，不能只重试 Journal。</remarks>
    public static IOperationTransaction EnlistEntityFramework(this RelationalOperationStore store, DbContext context)
    {
        ArgumentNullException.ThrowIfNull(store); ArgumentNullException.ThrowIfNull(context);
        var transaction = context.Database.CurrentTransaction ??
            throw new ResilienceConfigurationException("EF Core Journal 桥接需要当前显式事务。");
        return store.Enlist(context.Database.GetDbConnection(), transaction.GetDbTransaction());
    }
}
