using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Bing.DbDesigner.Commons.Domain.Models;

namespace Bing.DbDesigner.Data.Mappings.Commons.MySql {
    /// <summary>
    /// 系统配置映射配置
    /// </summary>
    public class ConfigurationMap : Bing.Datas.EntityFramework.MySql.AggregateRootMap<DbDesigner.Commons.Domain.Models.Configuration> {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable( EntityTypeBuilder<DbDesigner.Commons.Domain.Models.Configuration> builder ) {
            builder.ToTable( "Configuration", "Commons" );
        }
        
        /// <summary>
        /// 映射属性
        /// </summary>
        protected override void MapProperties( EntityTypeBuilder<DbDesigner.Commons.Domain.Models.Configuration> builder ) {
            //系统配置标识
            builder.Property(t => t.Id)
                .HasColumnName("ConfigurationId");
            builder.HasQueryFilter( t => t.IsDeleted == false );
        }
    }
}
