using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Bing.DbDesigner.Systems.Domain.Models;

namespace Bing.DbDesigner.Data.Mappings.Systems.PgSql {
    /// <summary>
    /// 应用程序角色映射配置
    /// </summary>
    public class ApplicationRoleMap : Bing.Datas.EntityFramework.PgSql.AggregateRootMap<ApplicationRole> {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable( EntityTypeBuilder<ApplicationRole> builder ) {
            builder.ToTable( "ApplicationRole", "Systems" );
        }
        
        /// <summary>
        /// 映射属性
        /// </summary>
        protected override void MapProperties( EntityTypeBuilder<ApplicationRole> builder ) {
            //应用程序标识
            builder.Property(t => t.Id)
                .HasColumnName("ApplicationId");
            //角色标识
            builder.Property(t => t.Id)
                .HasColumnName("RoleId");
        }
    }
}
