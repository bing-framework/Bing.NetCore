using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Bing.DbDesigner.Databases.Domain.Models;

namespace Bing.DbDesigner.Data.Mappings.Databases.MySql {
    /// <summary>
    /// 数据表映射配置
    /// </summary>
    public class DatabaseTableMap : Bing.Datas.EntityFramework.MySql.AggregateRootMap<DatabaseTable> {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable( EntityTypeBuilder<DatabaseTable> builder ) {
            builder.ToTable( "DatabaseTable", "Databases" );
        }
        
        /// <summary>
        /// 映射属性
        /// </summary>
        protected override void MapProperties( EntityTypeBuilder<DatabaseTable> builder ) {
            //数据表标识
            builder.Property(t => t.Id)
                .HasColumnName("DatabaseTableId");
            builder.HasQueryFilter( t => t.IsDeleted == false );
        }
    }
}
