using Bing.Admin.Commons.Domain.Models;
using FreeSql.Extensions.EfCoreFluentApi;

namespace Bing.Admin.Data.Mappings.Commons.MySql
{
    /// <summary>
    /// 文件 映射配置
    /// </summary>
    public class FileMap : Bing.FreeSQL.MySql.AggregateRootMap<File>
    {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable(EfCoreTableFluent<File> builder ) 
        {
            builder.ToTable( "`Commons.File`" );
        }
                
        /// <summary>
        /// 映射属性
        /// </summary>
        protected override void MapProperties(EfCoreTableFluent<File> builder ) 
        {
            // 文件编号
            builder.Property(t => t.Id)
                .HasColumnName("FileId");
        }
    }
}
