using System;
using System.Linq;
using System.Reflection;
using Bing.Ui.Attributes;
using Bing.Utils.Extensions;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Bing.Ui.Internal
{
    /// <summary>
    /// 辅助操作
    /// </summary>
    internal static class Helper
    {
        /// <summary>
        /// 获取模型绑定
        /// </summary>
        /// <param name="expression">属性表达式</param>
        /// <param name="member">成员</param>
        /// <returns></returns>
        public static string GetModel(ModelExpression expression, MemberInfo member)
        {
            Type modelType = expression.Metadata.ContainerType;
            var propertyName = expression.Name;
            return GetModel(GetModelName(modelType), GetPropertyName(member, propertyName));
        }

        /// <summary>
        /// 获取模型绑定
        /// </summary>
        /// <param name="modelName">模型名称</param>
        /// <param name="propertyName">属性名</param>
        /// <returns></returns>
        private static string GetModel(string modelName, string propertyName)
        {
            modelName = Bing.Utils.Helpers.Str.FirstLower(modelName);
            propertyName = GetFirstLowerCasePropertyName(propertyName);
            if (string.IsNullOrWhiteSpace(modelName) || string.IsNullOrWhiteSpace(propertyName))
            {
                return string.Empty;
            }

            return $"{modelName}&&{modelName}.{propertyName}";
        }

        /// <summary>
        /// 获取首字母小写的属性名
        /// </summary>
        /// <param name="propertyName">属性名</param>
        /// <returns></returns>
        private static string GetFirstLowerCasePropertyName(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                return propertyName;
            }

            return propertyName.Split('.').Select(Bing.Utils.Helpers.Str.FirstLower).ToList().Join("", ".");
        }

        /// <summary>
        /// 获取模型名称
        /// </summary>
        /// <param name="modelType">模型类型</param>
        /// <returns></returns>
        private static string GetModelName(Type modelType)
        {
            if (modelType.GetCustomAttribute<ModelAttribute>() is ModelAttribute attribute)
            {
                return attribute.Ignore ? string.Empty : attribute.Model;
            }

            return modelType.Name;
        }

        /// <summary>
        /// 获取属性名称
        /// </summary>
        /// <param name="member">成员</param>
        /// <param name="propertyName">属性名</param>
        /// <returns></returns>
        private static string GetPropertyName(MemberInfo member, string propertyName)
        {
            var attribute = member.GetCustomAttribute<ModelAttribute>();
            return attribute == null ? propertyName : attribute.Ignore ? string.Empty : attribute.Model;
        }
    }
}
