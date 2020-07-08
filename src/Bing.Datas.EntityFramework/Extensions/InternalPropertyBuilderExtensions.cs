using System;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Bing.Datas.EntityFramework.Extensions
{
    /// <summary>
    /// 内部属性生成器扩展
    /// </summary>
    public static partial class InternalPropertyBuilderExtensions
    {
        /// <summary>
        /// 设置精度
        /// </summary>
        /// <param name="propertyBuilder">内部属性生成器</param>
        /// <param name="configurationSource">配置源</param>
        /// <param name="precision">精度</param>
        /// <param name="scale">保留小数位</param>
        public static InternalPropertyBuilder HasPrecision(this InternalPropertyBuilder propertyBuilder,
            ConfigurationSource configurationSource, int precision, int scale)
        {
            propertyBuilder.Relational(configurationSource).HasColumnType($"decimal({precision},{scale})");
            return propertyBuilder;
        }

        /// <summary>
        /// 设置最大长度
        /// </summary>
        /// <param name="propertyBuilder">内部属性生成器</param>
        /// <param name="configurationSource">配置源</param>
        /// <param name="hasMaxLength">是否最大长度</param>
        public static InternalPropertyBuilder HasMaxLength(this InternalPropertyBuilder propertyBuilder,
            ConfigurationSource configurationSource, bool? hasMaxLength)
        {
            if (hasMaxLength.HasValue && hasMaxLength.Value == true)
            {
                Debug.WriteLine($"HasMaxLength(true) remove MaxLength Annotaion.entity:{propertyBuilder.Metadata.DeclaringType.Name};property:{propertyBuilder.Metadata.Name};{configurationSource}");
                propertyBuilder.HasAnnotation(CoreAnnotationNames.MaxLengthAnnotation, null, configurationSource);
            }

            return propertyBuilder;
        }
    }
}
