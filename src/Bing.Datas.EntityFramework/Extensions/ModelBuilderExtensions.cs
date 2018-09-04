using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Bing.Datas.EntityFramework.Extensions
{
    /// <summary>
    /// 实体生成器(<see cref="ModelBuilder"/>) 扩展
    /// </summary>
    public static partial class ModelBuilderExtensions
    {
        /// <summary>
        /// 设置简单下划线表名约定
        /// </summary>
        /// <param name="modelBuilder">实体生成器</param>
        /// <returns></returns>
        public static ModelBuilder SetSimpleUnderscoreTableNameConvention(this ModelBuilder modelBuilder)
        {
            Regex underscoreRegex=new Regex(@"((?<=.)[A-Z][a-zA-Z]*)|((?<=[a-zA-Z])\d+)");
            foreach (IMutableEntityType entity in modelBuilder.Model.GetEntityTypes())
            {
                entity.Relational().TableName = underscoreRegex.Replace(entity.DisplayName(), @"$1$2").ToLower();
            }
            return modelBuilder;
        }

        /// <summary>
        /// 设置关闭所有主外键关系的级联删除
        /// </summary>
        /// <param name="modelBuilder">实体生成器</param>
        /// <returns></returns>
        public static ModelBuilder SetOneToManyCascadeDeleteConvention(this ModelBuilder modelBuilder)
        {
            foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(x=>x.GetForeignKeys()))
            {
                relationship.DeleteBehavior = DeleteBehavior.Restrict;
            }
            return modelBuilder;
        }
    }
}
