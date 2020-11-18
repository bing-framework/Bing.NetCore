using Bing.Admin.Systems.Domain.Models;
using FreeSql.Extensions.EfCoreFluentApi;

namespace Bing.Admin.Data.Mappings.Systems.MySql
{
    /// <summary>
    /// 角色 映射配置
    /// </summary>
    public class RoleMap : Bing.FreeSQL.MySql.AggregateRootMap<Role>
    {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable( EfCoreTableFluent<Role> builder ) 
        {
            builder.ToTable( "`Systems.Role`" );
        }
                
        /// <summary>
        /// 映射属性
        /// </summary>
        protected override void MapProperties( EfCoreTableFluent<Role> builder ) 
        {
            // 角色标识
            builder.Property(t => t.Id)
                .HasColumnName("RoleId");
            builder.Property( t => t.Path ).HasColumnName( "Path" );
            builder.Property( t => t.Level ).HasColumnName( "Level" );
        }
    }
}
