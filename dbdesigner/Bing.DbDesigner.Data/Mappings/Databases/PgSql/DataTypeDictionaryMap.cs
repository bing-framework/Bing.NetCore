using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Bing.DbDesigner.Databases.Domain.Models;

namespace Bing.DbDesigner.Data.Mappings.Databases.PgSql {
    /// <summary>
    /// 数据类型字典映射配置
    /// </summary>
    public class DataTypeDictionaryMap : Bing.Datas.EntityFramework.PgSql.AggregateRootMap<DataTypeDictionary> {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable( EntityTypeBuilder<DataTypeDictionary> builder ) {
            builder.ToTable( "DataTypeDictionary", "Databases" );
        }
        
        /// <summary>
        /// 映射属性
        /// </summary>
        protected override void MapProperties( EntityTypeBuilder<DataTypeDictionary> builder ) {
            //数据类型字典标识
            builder.Property(t => t.Id)
                .HasColumnName("DataTypeDictionaryId");
            builder.HasQueryFilter( t => t.IsDeleted == false );
        }
    }
}
