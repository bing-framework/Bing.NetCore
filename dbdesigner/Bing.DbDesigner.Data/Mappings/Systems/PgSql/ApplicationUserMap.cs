using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Bing.DbDesigner.Systems.Domain.Models;

namespace Bing.DbDesigner.Data.Mappings.Systems.PgSql {
    /// <summary>
    /// 应用程序用户映射配置
    /// </summary>
    public class ApplicationUserMap : Bing.Datas.EntityFramework.PgSql.AggregateRootMap<ApplicationUser> {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable( EntityTypeBuilder<ApplicationUser> builder ) {
            builder.ToTable( "ApplicationUser", "Systems" );
        }
        
        /// <summary>
        /// 映射属性
        /// </summary>
        protected override void MapProperties( EntityTypeBuilder<ApplicationUser> builder ) {
            //应用程序标识
            builder.Property(t => t.Id)
                .HasColumnName("ApplicationId");
            //用户标识
            builder.Property(t => t.Id)
                .HasColumnName("UserId");
        }
    }
}
