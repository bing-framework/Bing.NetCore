using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Bing.DbDesigner.Projects.Domain.Models;

namespace Bing.DbDesigner.Data.Mappings.Projects.PgSql {
    /// <summary>
    /// 用户项目映射配置
    /// </summary>
    public class UserProjectMap : Bing.Datas.EntityFramework.PgSql.AggregateRootMap<UserProject> {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable( EntityTypeBuilder<UserProject> builder ) {
            builder.ToTable( "UserProject", "Projects" );
        }
        
        /// <summary>
        /// 映射属性
        /// </summary>
        protected override void MapProperties( EntityTypeBuilder<UserProject> builder ) {
            //用户项目标识
            builder.Property(t => t.Id)
                .HasColumnName("UserProjectId");
        }
    }
}
