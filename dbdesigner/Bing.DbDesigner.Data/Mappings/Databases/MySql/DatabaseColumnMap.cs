using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Bing.DbDesigner.Databases.Domain.Models;

namespace Bing.DbDesigner.Data.Mappings.Databases.MySql {
    /// <summary>
    /// 数据列映射配置
    /// </summary>
    public class DatabaseColumnMap : Bing.Datas.EntityFramework.MySql.AggregateRootMap<DatabaseColumn> {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable( EntityTypeBuilder<DatabaseColumn> builder ) {
            builder.ToTable( "DatabaseColumn", "Databases" );
        }
        
        /// <summary>
        /// 映射属性
        /// </summary>
        protected override void MapProperties( EntityTypeBuilder<DatabaseColumn> builder ) {
            //数据列标识
            builder.Property(t => t.Id)
                .HasColumnName("DatabaseColumnId");
            builder.HasQueryFilter( t => t.IsDeleted == false );
        }
    }
}
