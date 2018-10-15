using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Bing.DbDesigner.Projects.Domain.Models;

namespace Bing.DbDesigner.Data.Mappings.Projects.PgSql {
    /// <summary>
    /// 项目映射配置
    /// </summary>
    public class ProjectMap : Bing.Datas.EntityFramework.PgSql.AggregateRootMap<Project> {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable( EntityTypeBuilder<Project> builder ) {
            builder.ToTable( "Project", "Projects" );
        }
        
        /// <summary>
        /// 映射属性
        /// </summary>
        protected override void MapProperties( EntityTypeBuilder<Project> builder ) {
            //项目标识
            builder.Property(t => t.Id)
                .HasColumnName("ProjectId");
            builder.HasQueryFilter( t => t.IsDeleted == false );
        }
    }
}
