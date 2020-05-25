using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Bing.Admin.Commons.Domain.Models;

namespace Bing.Admin.Data.Mappings.Commons.PgSql
{
    /// <summary>
    /// 用户信息 映射配置
    /// </summary>
    public class UserInfoMap : Bing.Datas.EntityFramework.PgSql.AggregateRootMap<UserInfo>
    {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable( EntityTypeBuilder<UserInfo> builder ) 
        {
            builder.ToTable( "UserInfo", "Commons" );
        }
                
        /// <summary>
        /// 映射属性
        /// </summary>
        protected override void MapProperties( EntityTypeBuilder<UserInfo> builder ) 
        {
            // 用户编号
            builder.Property(t => t.Id)
                .HasColumnName("UserId");
            builder.HasQueryFilter( t => t.IsDeleted == false );
        }
    }
}