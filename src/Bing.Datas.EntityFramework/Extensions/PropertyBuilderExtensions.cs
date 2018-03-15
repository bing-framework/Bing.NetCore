using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Bing.Datas.EntityFramework.Extensions
{
    /// <summary>
    /// 属性生成器扩展
    /// </summary>
    public static class PropertyBuilderExtensions
    {
        /// <summary>
        /// 设置精度
        /// </summary>
        /// <typeparam name="TProperty">属性类型</typeparam>
        /// <param name="propertyBuilder">属性生成器</param>
        /// <param name="precision">精度</param>
        /// <param name="scale">保留小数位</param>
        /// <returns></returns>
        public static PropertyBuilder<TProperty> HasPrecision<TProperty>(
            this PropertyBuilder<TProperty> propertyBuilder, int precision = 18, int scale = 4)
        {
            ((IInfrastructure<InternalPropertyBuilder>) propertyBuilder).Instance.HasPrecision(
                ConfigurationSource.Explicit, precision, scale);
            return propertyBuilder;
        }

        /// <summary>
        /// 设置最大长度
        /// </summary>
        /// <typeparam name="TProperty">属性类型</typeparam>
        /// <param name="propertyBuilder">属性生成器</param>
        /// <param name="hasMaxLength">是否最大长度</param>
        /// <returns></returns>
        public static PropertyBuilder<TProperty> HasMaxLength<TProperty>(
            this PropertyBuilder<TProperty> propertyBuilder, bool? hasMaxLength)
        {
            (propertyBuilder as IInfrastructure<InternalPropertyBuilder>).Instance.HasMaxLength(
                ConfigurationSource.Explicit, hasMaxLength);
            return propertyBuilder;
        }
    }
}
