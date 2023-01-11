using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace Bing.Data.Filters;

/// <summary>
/// 逻辑删除过滤器
/// </summary>
public class SoftDeleteFilter : FilterBase<ISoftDelete>
{
    /// <summary>
    /// 获取过滤表达式
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    public override Expression<Func<TEntity, bool>> GetExpression<TEntity>() where TEntity : class
    {
        return entity => !EF.Property<bool>(entity, "IsDeleted");
    }
}
