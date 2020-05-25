using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Bing.Admin.Systems.Domain.Models;

namespace Bing.Admin.Data.Mappings.Systems.PgSql
{
    /// <summary>
    /// 权限 映射配置
    /// </summary>
    public class PermissionMap : Bing.Datas.EntityFramework.PgSql.AggregateRootMap<Permission>
    {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable( EntityTypeBuilder<Permission> builder ) 
        {
            builder.ToTable( "Permission", "Systems" );
        }
                
        /// <summary>
        /// 映射属性
        /// </summary>
        protected override void MapProperties( EntityTypeBuilder<Permission> builder ) 
        {
            // 权限标识
            builder.Property(t => t.Id)
                .HasColumnName("PermissionId");
            builder.HasQueryFilter( t => t.IsDeleted == false );
        }
    }
}