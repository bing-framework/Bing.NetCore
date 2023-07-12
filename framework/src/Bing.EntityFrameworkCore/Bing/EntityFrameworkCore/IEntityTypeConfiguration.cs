using Microsoft.EntityFrameworkCore;

namespace Bing.EntityFrameworkCore;

/// <summary>
/// 实体类型配置
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TDbContext">数据上下文类型</typeparam>
public interface IEntityTypeConfiguration<TEntity, TDbContext> : IEntityTypeConfiguration<TEntity> 
    where TEntity : class 
    where TDbContext : DbContext
{
}
