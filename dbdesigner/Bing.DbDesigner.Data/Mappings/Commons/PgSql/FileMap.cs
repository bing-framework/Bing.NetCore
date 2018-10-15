using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Bing.DbDesigner.Commons.Domain.Models;

namespace Bing.DbDesigner.Data.Mappings.Commons.PgSql {
    /// <summary>
    /// 文件映射配置
    /// </summary>
    public class FileMap : Bing.Datas.EntityFramework.PgSql.AggregateRootMap<File> {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable( EntityTypeBuilder<File> builder ) {
            builder.ToTable( "File", "Commons" );
        }
        
        /// <summary>
        /// 映射属性
        /// </summary>
        protected override void MapProperties( EntityTypeBuilder<File> builder ) {
            //文件标识
            builder.Property(t => t.Id)
                .HasColumnName("FileId");
        }
    }
}
