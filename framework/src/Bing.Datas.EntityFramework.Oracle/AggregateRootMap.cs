using Bing.Datas.EntityFramework.Core;
using Bing.Domain.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bing.Datas.EntityFramework.Oracle;

/// <summary>
/// 聚合根映射配置
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
public abstract class AggregateRootMap<TEntity> : MapBase<TEntity>, IMap where TEntity : class, IVersion
{
    /// <summary>
    /// 映射乐观离线锁
    /// </summary>
    protected override void MapVersion(EntityTypeBuilder<TEntity> builder) => builder.Property(t => t.Version).IsConcurrencyToken();
}