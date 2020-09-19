using Bing.Datas.EntityFramework.Core;
using Bing.Domain.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bing.Datas.EntityFramework.Sqlite
{
    /// <summary>
    /// 聚合根映射配置
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    public abstract class AggregateRootMap<TEntity> : MapBase<TEntity>, IMap where TEntity : class, IVersion
    {
        /// <summary>
        /// 映射乐观离线锁
        /// </summary>
        /// <param name="builder">实体类型生成器</param>
        protected override void MapVersion(EntityTypeBuilder<TEntity> builder) => builder.Property(t => t.Version).IsRowVersion();
    }
}
