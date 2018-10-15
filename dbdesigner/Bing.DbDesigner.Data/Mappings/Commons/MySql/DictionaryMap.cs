using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Bing.DbDesigner.Commons.Domain.Models;

namespace Bing.DbDesigner.Data.Mappings.Commons.MySql {
    /// <summary>
    /// 字典映射配置
    /// </summary>
    public class DictionaryMap : Bing.Datas.EntityFramework.MySql.AggregateRootMap<Dictionary> {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable( EntityTypeBuilder<Dictionary> builder ) {
            builder.ToTable( "Dictionary", "Commons" );
        }
        
        /// <summary>
        /// 映射属性
        /// </summary>
        protected override void MapProperties( EntityTypeBuilder<Dictionary> builder ) {
            //字典标识
            builder.Property(t => t.Id)
                .HasColumnName("DictionaryId");
            builder.HasQueryFilter( t => t.IsDeleted == false );
            builder.Property( t => t.Path ).HasColumnName( "Path" );
            builder.Property( t => t.Level ).HasColumnName( "Level" );
        }
    }
}
