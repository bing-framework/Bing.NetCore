using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Bing.DbDesigner.Projects.Domain.Models;

namespace Bing.DbDesigner.Data.Mappings.Projects.SqlServer {
    /// <summary>
    /// 用户解决方案映射配置
    /// </summary>
    public class UserSolutionMap : Bing.Datas.EntityFramework.SqlServer.AggregateRootMap<UserSolution> {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable( EntityTypeBuilder<UserSolution> builder ) {
            builder.ToTable( "UserSolution", "Projects" );
        }
        
        /// <summary>
        /// 映射属性
        /// </summary>
        protected override void MapProperties( EntityTypeBuilder<UserSolution> builder ) {
            //用户解决方案标识
            builder.Property(t => t.Id)
                .HasColumnName("UserSolutionId");
        }
    }
}
