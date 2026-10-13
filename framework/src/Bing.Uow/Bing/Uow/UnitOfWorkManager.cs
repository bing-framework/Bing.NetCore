namespace Bing.Uow;

/// <summary>
/// 工作单元管理器
/// </summary>
public class UnitOfWorkManager : IUnitOfWorkManager
{
    /// <summary>
    /// 工作单元集合
    /// </summary>
    private readonly HashSet<IUnitOfWork> _unitOfWorks;

    /// <summary>
    /// 初始化一个 <see cref="UnitOfWorkManager"/> 类型的实例。
    /// </summary>
    public UnitOfWorkManager() => _unitOfWorks = new HashSet<IUnitOfWork>();

    /// <inheritdoc />
    public void Commit()
    {
        foreach (var unitOfWork in _unitOfWorks)
            unitOfWork.Commit();
    }

    /// <inheritdoc />
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        foreach (var unitOfWork in _unitOfWorks)
            await unitOfWork.CommitAsync(cancellationToken);
    }

    /// <inheritdoc />
    public void Register(IUnitOfWork unitOfWork)
    {
        if (unitOfWork == null)
            throw new ArgumentNullException(nameof(unitOfWork));
        if (_unitOfWorks.Contains(unitOfWork) == false)
            _unitOfWorks.Add(unitOfWork);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<IUnitOfWork> GetUnitOfWorks() => _unitOfWorks;
}
