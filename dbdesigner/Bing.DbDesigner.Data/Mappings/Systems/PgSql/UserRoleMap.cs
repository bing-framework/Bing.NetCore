using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Bing.DbDesigner.Systems.Domain.Models;

namespace Bing.DbDesigner.Data.Mappings.Systems.PgSql {
    /// <summary>
    /// 用户角色映射配置
    /// </summary>
    public class UserRoleMap : Bing.Datas.EntityFramework.PgSql.AggregateRootMap<UserRole> {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable( EntityTypeBuilder<UserRole> builder ) {
            builder.ToTable( "UserRole", "Systems" );
        }
        
        /// <summary>
        /// 映射属性
        /// </summary>
        protected override void MapProperties( EntityTypeBuilder<UserRole> builder ) {
            //角色标识
            builder.Property(t => t.Id)
                .HasColumnName("RoleId");
            //用户标识
            builder.Property(t => t.Id)
                .HasColumnName("UserId");
        }
    }
}
