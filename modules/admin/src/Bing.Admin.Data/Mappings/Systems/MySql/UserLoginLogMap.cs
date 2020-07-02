using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Bing.Admin.Systems.Domain.Models;

namespace Bing.Admin.Data.Mappings.Systems.MySql
{
    /// <summary>
    /// 用户登录日志 映射配置
    /// </summary>
    public class UserLoginLogMap : Bing.Datas.EntityFramework.MySql.AggregateRootMap<UserLoginLog>
    {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable( EntityTypeBuilder<UserLoginLog> builder ) 
        {
            builder.ToTable( "Systems.UserLoginLog" );
        }
                
        /// <summary>
        /// 映射属性
        /// </summary>
        protected override void MapProperties( EntityTypeBuilder<UserLoginLog> builder ) 
        {
            // 用户登录日志编号
            builder.Property(t => t.Id)
                .HasColumnName("UserLoginLogId");
        }
    }
}