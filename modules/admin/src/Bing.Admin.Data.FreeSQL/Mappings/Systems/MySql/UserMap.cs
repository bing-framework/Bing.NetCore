using Bing.Admin.Systems.Domain.Models;
using FreeSql.Extensions.EfCoreFluentApi;

namespace Bing.Admin.Data.Mappings.Systems.MySql
{
    /// <summary>
    /// 用户 映射配置
    /// </summary>
    public class UserMap : Bing.FreeSQL.MySql.AggregateRootMap<User>
    {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable( EfCoreTableFluent<User> builder ) 
        {
            builder.ToTable( "`Systems.User`" );
        }
                
        /// <summary>
        /// 映射属性
        /// </summary>
        protected override void MapProperties( EfCoreTableFluent<User> builder ) 
        {
            // 用户标识
            builder.Property(t => t.Id)
                .HasColumnName("UserId");
        }
    }
}
