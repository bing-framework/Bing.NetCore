using System.Data;
using System.Data.Common;

namespace Bing.Uow;

/// <summary>
/// 在框架提交事务前延迟 FreeSQL 保存阶段的内部提交。
/// </summary>
/// <remarks>只在一次保存中使用，不取得底层工作单元的释放所有权。</remarks>
internal sealed class DeferredCommitUnitOfWork : FreeSql.IUnitOfWork
{
    /// <summary>
    /// 实际工作单元。
    /// </summary>
    private readonly FreeSql.IUnitOfWork _inner;

    /// <summary>
    /// 初始化一个 <see cref="DeferredCommitUnitOfWork" /> 类型的实例。
    /// </summary>
    /// <param name="inner">实际工作单元。</param>
    internal DeferredCommitUnitOfWork(FreeSql.IUnitOfWork inner) =>
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    /// <inheritdoc />
    public IFreeSql Orm => _inner.Orm;

    /// <inheritdoc />
    public IsolationLevel? IsolationLevel
    {
        get => _inner.IsolationLevel;
        set => _inner.IsolationLevel = value;
    }

    /// <inheritdoc />
    public FreeSql.DbContext.EntityChangeReport EntityChangeReport => _inner.EntityChangeReport;

    /// <inheritdoc />
    public Dictionary<string, object> States => _inner.States;

    /// <inheritdoc />
    public DbTransaction GetOrBeginTransaction(bool isCreate = true) => _inner.GetOrBeginTransaction(isCreate);

    /// <inheritdoc />
    public void Commit()
    {
        // SaveChangesSuccess 的内部提交由外层事务流程统一完成。
    }

    /// <inheritdoc />
    public void Rollback() => _inner.Rollback();

    /// <inheritdoc />
    public void Dispose()
    {
        // 实际工作单元由原资源所有者释放。
    }
}
