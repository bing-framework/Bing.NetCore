using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Bing.DbDesigner.Databases.Domain.Models;

namespace Bing.DbDesigner.Data.Mappings.Databases.SqlServer {
    /// <summary>
    /// 数据库映射配置
    /// </summary>
    public class DatabaseMap : Bing.Datas.EntityFramework.SqlServer.AggregateRootMap<Database> {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable( EntityTypeBuilder<Database> builder ) {
            builder.ToTable( "Database", "Databases" );
        }
        
        /// <summary>
        /// 映射属性
        /// </summary>
        protected override void MapProperties( EntityTypeBuilder<Database> builder ) {
            //数据库标识
            builder.Property(t => t.Id)
                .HasColumnName("DatabaseId");
            builder.HasQueryFilter( t => t.IsDeleted == false );
        }
    }
}
