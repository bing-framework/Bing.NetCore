using System.Linq;
using Bing.Uow;
using Microsoft.EntityFrameworkCore;

namespace Bing.Datas.EntityFramework.Extensions;

/// <summary>
/// EF工作单元 扩展
/// </summary>
public static partial class UnitOfWorkExtensions
{
    /// <summary>
    /// 清空缓存
    /// </summary>
    /// <param name="unitOfWork">工作单元</param>
    public static void ClearCache(this IUnitOfWork unitOfWork)
    {
        var dbContext = unitOfWork as DbContext;
        dbContext?.ChangeTracker.Entries().ToList().ForEach(entry => entry.State = EntityState.Detached);
    }
}