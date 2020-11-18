using Bing.Admin.Data.Pos.Systems;
using FreeSql.Extensions.EfCoreFluentApi;

namespace Bing.Admin.Data.Mappings.Systems.MySql
{
    /// <summary>
    /// 资源 映射配置
    /// </summary>
    public class ResourcePoMap : Bing.FreeSQL.MySql.AggregateRootMap<ResourcePo>
    {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable( EfCoreTableFluent<ResourcePo> builder ) 
        {
            builder.ToTable( "`Systems.Resource`" );
        }
                
        /// <summary>
        /// 映射属性
        /// </summary>
        protected override void MapProperties( EfCoreTableFluent<ResourcePo> builder ) 
        {
            // 资源标识
            builder.Property(t => t.Id)
                .HasColumnName("ResourceId");
            builder.Property( t => t.Path ).HasColumnName( "Path" );
            builder.Property( t => t.Level ).HasColumnName( "Level" );
        }
    }
}
