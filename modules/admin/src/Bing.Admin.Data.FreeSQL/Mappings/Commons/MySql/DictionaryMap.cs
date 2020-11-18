using Bing.Admin.Commons.Domain.Models;
using FreeSql.Extensions.EfCoreFluentApi;

namespace Bing.Admin.Data.Mappings.Commons.MySql
{
    /// <summary>
    /// 字典 映射配置
    /// </summary>
    public class DictionaryMap : Bing.FreeSQL.MySql.AggregateRootMap<Dictionary>
    {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable(EfCoreTableFluent<Dictionary> builder ) 
        {
            builder.ToTable( "`Commons.Dictionary`" );
        }
                
        /// <summary>
        /// 映射属性
        /// </summary>
        protected override void MapProperties(EfCoreTableFluent<Dictionary> builder ) 
        {
            // 字典编号
            builder.Property(t => t.Id)
                .HasColumnName("DictionaryId");
            builder.Property( t => t.Path ).HasColumnName( "Path" );
            builder.Property( t => t.Level ).HasColumnName( "Level" );
        }
    }
}
