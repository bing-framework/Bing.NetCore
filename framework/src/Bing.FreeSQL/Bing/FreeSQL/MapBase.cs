using FreeSql;
using FreeSql.Extensions.EfCoreFluentApi;

namespace Bing.FreeSQL
{
    /// <summary>
    /// 映射配置
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    public abstract class MapBase<TEntity> : IMap where TEntity : class
    {
        /// <summary>
        /// 模型生成器
        /// </summary>
        protected ICodeFirst ModelBuilder { get; private set; }

        /// <summary>
        /// 映射配置
        /// </summary>
        /// <param name="modelBuilder">模型生成器</param>
        public void Map(ICodeFirst modelBuilder)
        {
            ModelBuilder = modelBuilder;
            modelBuilder.Entity<TEntity>(builder =>
            {
                MapTable(builder);
            });
        }

        /// <summary>
        /// 映射表
        /// </summary>
        /// <param name="builder">实体模型生成器</param>
        protected abstract void MapTable(EfCoreTableFluent<TEntity> builder);

        /// <summary>
        /// 映射乐观离线锁
        /// </summary>
        /// <param name="builder">实体类型生成器</param>
        protected virtual void MapVersion(EfCoreTableFluent<TEntity> builder)
        {
        }

        /// <summary>
        /// 映射属性
        /// </summary>
        /// <param name="builder">实体类型生成器</param>
        protected virtual void MapProperties(EfCoreTableFluent<TEntity> builder)
        {
        }

        /// <summary>
        /// 映射导航属性
        /// </summary>
        /// <param name="builder">实体类型生成器</param>
        protected virtual void MapAssociations(EfCoreTableFluent<TEntity> builder)
        {
        }
    }
}
