using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace Bing.Data.Filters;

/// <summary>
/// 逻辑删除过滤器
/// </summary>
public class SoftDeleteFilter : FilterBase<ISoftDelete>
{
    /// <inheritdoc />
    public override Expression<Func<TEntity, bool>> GetExpression<TEntity>() where TEntity : class
    {
        return entity => !EF.Property<bool>(entity, "IsDeleted");
    }
}
