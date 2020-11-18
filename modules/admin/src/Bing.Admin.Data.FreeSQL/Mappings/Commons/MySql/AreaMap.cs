using Bing.Admin.Commons.Domain.Models;
using FreeSql.Extensions.EfCoreFluentApi;

namespace Bing.Admin.Data.Mappings.Commons.MySql
{
    /// <summary>
    /// 地区 映射配置
    /// </summary>
    public class AreaMap : Bing.FreeSQL.MySql.AggregateRootMap<Area>
    {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable(EfCoreTableFluent<Area> builder ) 
        {
            builder.ToTable( "`Commons.Area`" );
        }
                
        /// <summary>
        /// 映射属性
        /// </summary>
        protected override void MapProperties(EfCoreTableFluent<Area> builder ) 
        {
            // 地区编号
            builder.Property(t => t.Id)
                .HasColumnName("AreaId");
            builder.Property( t => t.Path ).HasColumnName( "Path" );
            builder.Property( t => t.Level ).HasColumnName( "Level" );
        }
    }
}
