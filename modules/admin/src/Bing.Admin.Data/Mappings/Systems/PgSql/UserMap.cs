using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Bing.Admin.Systems.Domain.Models;

namespace Bing.Admin.Data.Mappings.Systems.PgSql
{
    /// <summary>
    /// 用户 映射配置
    /// </summary>
    public class UserMap : Bing.Datas.EntityFramework.PgSql.AggregateRootMap<User>
    {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable( EntityTypeBuilder<User> builder ) 
        {
            builder.ToTable( "User", "Systems" );
        }
                
        /// <summary>
        /// 映射属性
        /// </summary>
        protected override void MapProperties( EntityTypeBuilder<User> builder ) 
        {
            // 用户标识
            builder.Property(t => t.Id)
                .HasColumnName("UserId");
            builder.HasQueryFilter( t => t.IsDeleted == false );
        }
    }
}