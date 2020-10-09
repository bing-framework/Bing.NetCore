using Bing.Domain.Entities;
using FreeSql.Extensions.EfCoreFluentApi;

namespace Bing.FreeSQL.MySql
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
        protected override void MapVersion(EfCoreTableFluent<TEntity> builder) => builder.Property(x => x.Version).IsRowVersion();
    }
}
