using Bing.Datas.Configs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bing.Datas.EntityFramework.Core
{
    /// <summary>
    /// 映射配置
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    public abstract class MapBase<TEntity>:IMap where TEntity:class
    {
        /// <summary>
        /// 模型生成器
        /// </summary>
        protected ModelBuilder ModelBuilder { get; private set; }

        /// <summary>
        /// 映射配置
        /// </summary>
        /// <param name="modelBuilder">模型生成器</param>
        public void Map(ModelBuilder modelBuilder)
        {
            ModelBuilder = modelBuilder;
            var builder = modelBuilder.Entity<TEntity>();
            MapTable(builder);
            if (DataConfig.EnabledValidateVersion)
            {
                MapVersion(builder);
            }
            MapProperties(builder);
            MapAssociations(builder);
        }

        /// <summary>
        /// 映射表
        /// </summary>
        /// <param name="builder">实体类型生成器</param>
        protected abstract void MapTable(EntityTypeBuilder<TEntity> builder);

        /// <summary>
        /// 映射乐观离线锁
        /// </summary>
        /// <param name="builder">实体类型生成器</param>
        protected virtual void MapVersion(EntityTypeBuilder<TEntity> builder) { }

        /// <summary>
        /// 映射属性
        /// </summary>
        /// <param name="builder">实体类型生成器</param>
        protected virtual void MapProperties(EntityTypeBuilder<TEntity> builder) { }

        /// <summary>
        /// 映射导航属性
        /// </summary>
        /// <param name="builder">实体类型生成器</param>
        protected virtual void MapAssociations(EntityTypeBuilder<TEntity> builder) { }
    }
}
