using Bing.Admin.Systems.Domain.Models;
using FreeSql.Extensions.EfCoreFluentApi;

namespace Bing.Admin.Data.Mappings.Systems.MySql
{
    /// <summary>
    /// 管理员 映射配置
    /// </summary>
    public class AdministratorMap : Bing.FreeSQL.MySql.AggregateRootMap<Administrator>
    {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable( EfCoreTableFluent<Administrator> builder ) 
        {
            builder.ToTable( "`Systems.Administrator`" );
        }
                
        /// <summary>
        /// 映射属性
        /// </summary>
        protected override void MapProperties( EfCoreTableFluent<Administrator> builder ) 
        {
            // 管理员编号
            builder.Property(t => t.Id)
                .HasColumnName("AdministratorId");
        }
    }
}
