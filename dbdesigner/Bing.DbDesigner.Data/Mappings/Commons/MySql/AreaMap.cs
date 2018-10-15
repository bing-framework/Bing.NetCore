using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Bing.DbDesigner.Commons.Domain.Models;

namespace Bing.DbDesigner.Data.Mappings.Commons.MySql {
    /// <summary>
    /// 地区映射配置
    /// </summary>
    public class AreaMap : Bing.Datas.EntityFramework.MySql.AggregateRootMap<Area> {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable( EntityTypeBuilder<Area> builder ) {
            builder.ToTable( "Area", "Commons" );
        }
        
        /// <summary>
        /// 映射属性
        /// </summary>
        protected override void MapProperties( EntityTypeBuilder<Area> builder ) {
            //区域标识
            builder.Property(t => t.Id)
                .HasColumnName("AreaId");
            builder.HasQueryFilter( t => t.IsDeleted == false );
            builder.Property( t => t.Path ).HasColumnName( "Path" );
            builder.Property( t => t.Level ).HasColumnName( "Level" );
        }
    }
}
