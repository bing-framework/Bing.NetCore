using Bing.Admin.Commons.Domain.Models;
using FreeSql.Extensions.EfCoreFluentApi;

namespace Bing.Admin.Data.Mappings.Commons.MySql
{
    /// <summary>
    /// 用户信息 映射配置
    /// </summary>
    public class UserInfoMap : Bing.FreeSQL.MySql.AggregateRootMap<UserInfo>
    {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable(EfCoreTableFluent<UserInfo> builder ) 
        {
            builder.ToTable( "`Commons.UserInfo`" );
        }
                
        /// <summary>
        /// 映射属性
        /// </summary>
        protected override void MapProperties(EfCoreTableFluent<UserInfo> builder ) 
        {
            // 用户编号
            builder.Property(t => t.Id)
                .HasColumnName("UserId");
        }
    }
}
