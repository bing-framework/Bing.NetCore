using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Bing.Admin.Commons.Domain.Models;

namespace Bing.Admin.Data.Mappings.Commons.MySql
{
    /// <summary>
    /// 文件 映射配置
    /// </summary>
    public class FileMap : Bing.Datas.EntityFramework.MySql.AggregateRootMap<File>
    {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable( EntityTypeBuilder<File> builder ) 
        {
            builder.ToTable( "Commons.File" );
        }
                
        /// <summary>
        /// 映射属性
        /// </summary>
        protected override void MapProperties( EntityTypeBuilder<File> builder ) 
        {
            // 文件编号
            builder.Property(t => t.Id)
                .HasColumnName("FileId");
        }
    }
}