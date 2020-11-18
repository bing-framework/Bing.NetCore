using Bing.Admin.Data.Pos.Systems;
using FreeSql.Extensions.EfCoreFluentApi;

namespace Bing.Admin.Data.Mappings.Systems.MySql
{
    /// <summary>
    /// 应用程序 映射配置
    /// </summary>
    public class ApplicationPoMap : Bing.FreeSQL.MySql.AggregateRootMap<ApplicationPo>
    {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable( EfCoreTableFluent<ApplicationPo> builder ) 
        {
            builder.ToTable( "`Systems.Application`" );
        }
                
        /// <summary>
        /// 映射属性
        /// </summary>
        protected override void MapProperties( EfCoreTableFluent<ApplicationPo> builder ) 
        {
            // 应用程序标识
            builder.Property(t => t.Id)
                .HasColumnName("ApplicationId");
        }
    }
}
