using Bing.Admin.Systems.Domain.Models;
using FreeSql.Extensions.EfCoreFluentApi;

namespace Bing.Admin.Data.Mappings.Systems.MySql
{
    /// <summary>
    /// 权限 映射配置
    /// </summary>
    public class PermissionMap : Bing.FreeSQL.MySql.AggregateRootMap<Permission>
    {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable( EfCoreTableFluent<Permission> builder ) 
        {
            builder.ToTable( "`Systems.Permission`" );
        }
                
        /// <summary>
        /// 映射属性
        /// </summary>
        protected override void MapProperties( EfCoreTableFluent<Permission> builder ) 
        {
            // 权限标识
            builder.Property(t => t.Id)
                .HasColumnName("PermissionId");
        }
    }
}
