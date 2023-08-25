using Bing.Datas.EntityFramework.Core;
using Bing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bing.Datas.EntityFramework.SqlServer;

/// <summary>
/// 聚合根映射配置
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
public abstract class AggregateRootMap<TEntity> : MapBase<TEntity>, IMap where TEntity : class
{
    /// <summary>
    /// 映射乐观离线锁
    /// </summary>
    /// <param name="builder">实体类型生成器</param>
    protected override void MapVersion(EntityTypeBuilder<TEntity> builder)
    {
        if (typeof(IVersion).IsAssignableFrom(typeof(TEntity)) == false)
            return;
        builder.Property(nameof(IVersion.Version))
            .HasColumnName(nameof(IVersion.Version))
            .IsRowVersion();
    }
}
