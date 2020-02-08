using System;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Internal;

namespace Bing.Datas.EntityFramework.Conventions
{
    /// <summary>
    /// Bing 核心约束集合生成器
    /// </summary>
    public class BingCoreConventionSetBuilder : CoreConventionSetBuilder
    {
        /// <summary>
        /// 初始化一个<see cref="BingCoreConventionSetBuilder"/>类型的实例
        /// </summary>
        /// <param name="dependencies">依赖</param>
        public BingCoreConventionSetBuilder(CoreConventionSetBuilderDependencies dependencies) : base(dependencies)
        {
            Console.WriteLine("BingRelationModeCustomizer ctor");
        }

        /// <summary>
        /// 创建约束集合
        /// </summary>
        /// <returns></returns>
        public override ConventionSet CreateConventionSet()
        {
            Console.WriteLine("BingCoreConventionSetBuilder.CreateConventionSet");

            var conventionSet = base.CreateConventionSet();
            conventionSet.PropertyAddedConventions.Add(new DecimalPrecisionAttributeConvention());
            conventionSet.PropertyAddedConventions.Add(new HasMaxLengthAttributeConvention());

            return conventionSet;
        }
    }
}
