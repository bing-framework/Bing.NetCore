using Bing.Admin.Data.Pos.Systems;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bing.Admin.Data.Mappings.Systems.PgSql
{
    /// <summary>
    /// 应用程序持久化对象 映射配置
    /// </summary>
    public class ApplicationPoMap : Bing.Datas.EntityFramework.PgSql.AggregateRootMap<ApplicationPo>
    {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable( EntityTypeBuilder<ApplicationPo> builder ) 
        {
            builder.ToTable( "Application", "Systems" );
        }
                
        /// <summary>
        /// 映射属性
        /// </summary>
        protected override void MapProperties( EntityTypeBuilder<ApplicationPo> builder ) 
        {
            // 应用程序标识
            builder.Property(t => t.Id)
                .HasColumnName("ApplicationId");
            builder.HasQueryFilter( t => t.IsDeleted == false );
        }
    }
}
