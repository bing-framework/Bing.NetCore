using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Bing.DbDesigner.Databases.Domain.Models;

namespace Bing.DbDesigner.Data.Mappings.Databases.MySql {
    /// <summary>
    /// 数据模式映射配置
    /// </summary>
    public class DatabaseSchemaMap : Bing.Datas.EntityFramework.MySql.AggregateRootMap<DatabaseSchema> {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable( EntityTypeBuilder<DatabaseSchema> builder ) {
            builder.ToTable( "DatabaseSchema", "Databases" );
        }
        
        /// <summary>
        /// 映射属性
        /// </summary>
        protected override void MapProperties( EntityTypeBuilder<DatabaseSchema> builder ) {
            //数据模式标识
            builder.Property(t => t.Id)
                .HasColumnName("DatabaseSchemaId");
            builder.HasQueryFilter( t => t.IsDeleted == false );
        }
    }
}
