using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Bing.Auditing
{
    /// <summary>
    /// 审计契约解析器
    /// </summary>
    public class AuditingContractResolver : CamelCasePropertyNamesContractResolver
    {
        /// <summary>
        /// 忽略类型列表
        /// </summary>
        private readonly List<Type> _ignoredTypes;

        /// <summary>
        /// 初始化一个<see cref="AuditingContractResolver"/>类型的实例
        /// </summary>
        /// <param name="ignoredTypes"></param>
        public AuditingContractResolver(List<Type> ignoredTypes) => _ignoredTypes = ignoredTypes;

        /// <summary>
        /// 创建属性
        /// </summary>
        /// <param name="member">成员信息</param>
        /// <param name="memberSerialization">成员序列化器</param>
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);
            if (member.IsDefined(typeof(DisableAuditingAttribute)) || member.IsDefined(typeof(JsonIgnoreAttribute)))
                property.ShouldSerialize = instance => false;
            foreach (var ignoredType in _ignoredTypes)
            {
                if (ignoredType.GetTypeInfo().IsAssignableFrom(property.PropertyType))
                {
                    property.ShouldSerialize = instance => false;
                    break;
                }
            }
            return property;
        }
    }
}
