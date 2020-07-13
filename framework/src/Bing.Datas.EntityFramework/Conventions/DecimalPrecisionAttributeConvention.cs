using System;
using System.Reflection;
using Bing.Datas.Attributes;
using Bing.Datas.EntityFramework.Extensions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Internal;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Bing.Datas.EntityFramework.Conventions
{
    /// <summary>
    /// Decimal精确度约定，用于ModelBuilder全局设置Decimal精确度属性
    /// </summary>
    public class DecimalPrecisionAttributeConvention : PropertyAttributeConvention<DecimalPrecisionAttribute>
    {
        /// <summary>
        /// 应用
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public override InternalPropertyBuilder Apply(InternalPropertyBuilder propertyBuilder, DecimalPrecisionAttribute attribute,
            MemberInfo clrMember)
        {
            if (propertyBuilder.Metadata.ClrType == typeof(decimal))
            {
                if (attribute.Precision < 1 || attribute.Precision > 38)
                    throw new InvalidOperationException("Precision must be between 1 and 38.");
                if (attribute.Scale > attribute.Precision)
                    throw new InvalidOperationException("Scale must be between 0 and the Precision value.");
                propertyBuilder.HasPrecision(ConfigurationSource.DataAnnotation, attribute.Precision, attribute.Scale);
            }

            return propertyBuilder;
        }
    }
}
