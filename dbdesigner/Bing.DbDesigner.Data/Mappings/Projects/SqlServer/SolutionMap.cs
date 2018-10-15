using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Bing.DbDesigner.Projects.Domain.Models;

namespace Bing.DbDesigner.Data.Mappings.Projects.SqlServer {
    /// <summary>
    /// 解决方案映射配置
    /// </summary>
    public class SolutionMap : Bing.Datas.EntityFramework.SqlServer.AggregateRootMap<Solution> {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable( EntityTypeBuilder<Solution> builder ) {
            builder.ToTable( "Solution", "Projects" );
        }
        
        /// <summary>
        /// 映射属性
        /// </summary>
        protected override void MapProperties( EntityTypeBuilder<Solution> builder ) {
            //解决方案标识
            builder.Property(t => t.Id)
                .HasColumnName("SolutionId");
            builder.HasQueryFilter( t => t.IsDeleted == false );
        }
    }
}
