using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Bing.Admin.Systems.Domain.Models;

namespace Bing.Admin.Data.Mappings.Systems.PgSql
{
    /// <summary>
    /// 角色 映射配置
    /// </summary>
    public class RoleMap : Bing.Datas.EntityFramework.PgSql.AggregateRootMap<Role>
    {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable( EntityTypeBuilder<Role> builder ) 
        {
            builder.ToTable( "Role", "Systems" );
        }
                
        /// <summary>
        /// 映射属性
        /// </summary>
        protected override void MapProperties( EntityTypeBuilder<Role> builder ) 
        {
            // 角色标识
            builder.Property(t => t.Id)
                .HasColumnName("RoleId");
            builder.HasQueryFilter( t => t.IsDeleted == false );
            builder.Property( t => t.Path ).HasColumnName( "Path" );
            builder.Property( t => t.Level ).HasColumnName( "Level" );
        }
    }
}