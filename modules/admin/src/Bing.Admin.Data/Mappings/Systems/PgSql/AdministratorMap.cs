using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Bing.Admin.Systems.Domain.Models;

namespace Bing.Admin.Data.Mappings.Systems.PgSql
{
    /// <summary>
    /// 管理员 映射配置
    /// </summary>
    public class AdministratorMap : Bing.Datas.EntityFramework.PgSql.AggregateRootMap<Administrator>
    {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable( EntityTypeBuilder<Administrator> builder ) 
        {
            builder.ToTable( "Administrator", "Systems" );
        }
                
        /// <summary>
        /// 映射属性
        /// </summary>
        protected override void MapProperties( EntityTypeBuilder<Administrator> builder ) 
        {
            // 管理员编号
            builder.Property(t => t.Id)
                .HasColumnName("AdministratorId");
            builder.HasQueryFilter( t => t.IsDeleted == false );
        }
    }
}