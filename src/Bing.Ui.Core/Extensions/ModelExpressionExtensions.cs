using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Bing.Ui.Extensions
{
    /// <summary>
    /// 模型表达式扩展
    /// </summary>
    public static partial class ModelExpressionExtensions
    {
        /// <summary>
        /// 获取成员
        /// </summary>
        /// <param name="expression">模型表达式</param>
        public static MemberInfo GetMemberInfo(this ModelExpression expression)
        {
            var members = expression.Metadata.ContainerType.GetMember(expression.Metadata.PropertyName);
            return members.Length == 0 ? null : members[0];
        }
    }
}
