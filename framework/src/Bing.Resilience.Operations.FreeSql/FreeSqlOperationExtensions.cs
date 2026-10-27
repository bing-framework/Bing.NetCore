using Bing.Resilience.Operations.Relational;
using FreeSql;

namespace Bing.Resilience.Operations.FreeSql;

/// <summary>FreeSQL 专属事务桥接；不会隐式开启事务，也不会调用工作单元的 Commit。</summary>
public static class FreeSqlOperationExtensions
{
    /// <summary>复用宿主 IUnitOfWork 已建立的事务。业务 SQL 必须使用同一工作单元/WithTransaction。</summary>
    /// <remarks>DbContext.SaveChanges 的提交行为由宿主管理；不能先让它提交再登记 Journal。</remarks>
    public static IOperationTransaction EnlistFreeSql(this RelationalOperationStore store, IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(store); ArgumentNullException.ThrowIfNull(unitOfWork);
        var transaction = unitOfWork.GetOrBeginTransaction(false) ??
            throw new ResilienceConfigurationException("FreeSQL Journal 桥接需要已建立的宿主事务。");
        var connection = transaction.Connection ?? throw new ResilienceConfigurationException("FreeSQL 事务已完成。");
        return store.Enlist(connection, transaction);
    }
}
