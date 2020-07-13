using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Bing.Domains.Entities;
using Microsoft.EntityFrameworkCore;
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
        public static ModelBuilder SetSimpleUnderscoreTableNameConvention(this ModelBuilder modelBuilder)
        {
            var underscoreRegex = new Regex(@"((?<=.)[A-Z][a-zA-Z]*)|((?<=[a-zA-Z])\d+)");
            foreach (var entity in modelBuilder.Model.GetEntityTypes())
                entity.Relational().TableName = underscoreRegex.Replace(entity.DisplayName(), @"$1$2").ToLower();
            return modelBuilder;
        }

        /// <summary>
        /// 设置关闭所有主外键关系的级联删除
        /// </summary>
        /// <param name="modelBuilder">实体生成器</param>
        public static ModelBuilder SetOneToManyCascadeDeleteConvention(this ModelBuilder modelBuilder)
        {
            foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(x => x.GetForeignKeys()))
                relationship.DeleteBehavior = DeleteBehavior.Restrict;
            return modelBuilder;
        }

        /// <summary>
        /// 全局逻辑删除查询过滤器
        /// </summary>
        /// <param name="modelBuilder">实体生成器</param>
        public static ModelBuilder HasGlobalDeleteQueryFilter(this ModelBuilder modelBuilder)
        {
            modelBuilder.Model.GetEntityTypes().Where(entityType => typeof(IDelete).IsAssignableFrom(entityType.ClrType))
                .ToList().ForEach(x =>
                {
                    modelBuilder.Entity(x.ClrType).Property<bool>("IsDeleted");
                    var parameter = Expression.Parameter(x.ClrType, "e");
                    var body = Expression.Equal(
                        Expression.Call(typeof(EF), nameof(EF.Property), new[] { typeof(bool) }, parameter,
                            Expression.Constant("IsDeleted")), Expression.Constant(false));
                    modelBuilder.Entity(x.ClrType).HasQueryFilter(Expression.Lambda(body, parameter));
                });
            return modelBuilder;
        }
    }
}
