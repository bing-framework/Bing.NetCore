using System;
using System.Collections.Generic;
using System.Text;
using Bing.DbDesigner.Domains.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bing.DbDesigner.Data.Mappings.SqlServer
{
    /// <summary>
    /// 项目映射配置
    /// </summary>
    public class ProjectMap:Bing.Datas.EntityFramework.SqlServer.AggregateRootMap<Project>
    {
        /// <summary>
        /// 映射表
        /// </summary>
        protected override void MapTable(EntityTypeBuilder<Project> builder)
        {
            builder.ToTable("Project");
        }

        /// <summary>
        /// 映射属性
        /// </summary>
        /// <param name="builder"></param>
        protected override void MapProperties(EntityTypeBuilder<Project> builder)
        {
            builder.Property(t => t.Id).HasColumnName("Id");
            builder.HasQueryFilter(t => t.IsDeleted == false);
        }
    }
}
