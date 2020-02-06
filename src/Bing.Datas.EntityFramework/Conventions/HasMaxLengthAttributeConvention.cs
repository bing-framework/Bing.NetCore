using System.Reflection;
using Bing.Datas.Attributes;
using Bing.Datas.EntityFramework.Extensions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Internal;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Bing.Datas.EntityFramework.Conventions
{
    /// <summary>
    /// 最大长度约定，用于ModelBuilder全局设置最大长度
    /// </summary>
    public class HasMaxLengthAttributeConvention : PropertyAttributeConvention<HasMaxLengthAttribute>
    {
        /// <summary>
        /// 应用
        /// </summary>
        public override InternalPropertyBuilder Apply(InternalPropertyBuilder propertyBuilder, HasMaxLengthAttribute attribute,
            MemberInfo clrMember)
        {
            propertyBuilder.HasMaxLength(ConfigurationSource.DataAnnotation, attribute.HasMaxLength);
            return propertyBuilder;
        }
    }
}
