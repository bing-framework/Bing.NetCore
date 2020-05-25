using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Bing.Admin.Systems.Domain.Models;

namespace Bing.Admin.Data.Mappings.Systems.PgSql
{
    /// <summary>
    /// 用户角色 映射配置
    /// </summary>
    public class UserRoleMap : Bing.Datas.EntityFramework.PgSql.EntityMap<UserRole>
    {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable( EntityTypeBuilder<UserRole> builder ) 
        {
            builder.ToTable( "UserRole", "Systems" );
        }
                
        /// <summary>
        /// 映射属性
        /// </summary>
        protected override void MapProperties( EntityTypeBuilder<UserRole> builder ) 
        {
            // 复合标识
            builder.HasKey(t => new { t.UserId, t.RoleId });
        }
    }
}
