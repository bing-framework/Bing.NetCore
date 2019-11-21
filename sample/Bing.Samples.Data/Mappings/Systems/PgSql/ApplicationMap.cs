using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Bing.Samples.Domain.Models;

namespace Bing.Samples.Data.Mappings.Systems.PgSql {
    /// <summary>
    /// 应用程序映射配置
    /// </summary>
    public class ApplicationMap : Bing.Datas.EntityFramework.PgSql.AggregateRootMap<Application> {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable( EntityTypeBuilder<Application> builder ) {
            builder.ToTable( "Application", "Systems" );
        }

        /// <summary>
        /// 映射属性
        /// </summary>
        protected override void MapProperties( EntityTypeBuilder<Application> builder ) {
            //应用程序编号
            builder.Property( t => t.Id )
                .HasColumnName( "ApplicationId" );
            builder.HasQueryFilter( t => t.IsDeleted == false );
        }
    }
}
