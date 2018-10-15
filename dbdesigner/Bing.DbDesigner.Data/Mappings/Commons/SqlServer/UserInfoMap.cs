using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Bing.DbDesigner.Commons.Domain.Models;

namespace Bing.DbDesigner.Data.Mappings.Commons.SqlServer {
    /// <summary>
    /// 用户信息映射配置
    /// </summary>
    public class UserInfoMap : Bing.Datas.EntityFramework.SqlServer.AggregateRootMap<UserInfo> {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable( EntityTypeBuilder<UserInfo> builder ) {
            builder.ToTable( "UserInfo", "Commons" );
        }
        
        /// <summary>
        /// 映射属性
        /// </summary>
        protected override void MapProperties( EntityTypeBuilder<UserInfo> builder ) {
            //用户标识
            builder.Property(t => t.Id)
                .HasColumnName("UserId");
            builder.HasQueryFilter( t => t.IsDeleted == false );
        }
    }
}
