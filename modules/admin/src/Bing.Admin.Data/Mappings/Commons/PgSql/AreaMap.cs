using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Bing.Admin.Commons.Domain.Models;

namespace Bing.Admin.Data.Mappings.Commons.PgSql
{
    /// <summary>
    /// 地区 映射配置
    /// </summary>
    public class AreaMap : Bing.Datas.EntityFramework.PgSql.AggregateRootMap<Area>
    {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable( EntityTypeBuilder<Area> builder ) 
        {
            builder.ToTable( "Area", "Commons" );
        }
                
        /// <summary>
        /// 映射属性
        /// </summary>
        protected override void MapProperties( EntityTypeBuilder<Area> builder ) 
        {
            // 地区编号
            builder.Property(t => t.Id)
                .HasColumnName("AreaId");
            builder.HasQueryFilter( t => t.IsDeleted == false );
            builder.Property( t => t.Path ).HasColumnName( "Path" );
            builder.Property( t => t.Level ).HasColumnName( "Level" );
        }
    }
}