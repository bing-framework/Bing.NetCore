using System.Reflection;
using Bing.Ui.Configs;
using Bing.Ui.Extensions;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Bing.Ui.Angular.Forms.Resolvers
{
    /// <summary>
    /// 表达式解析器
    /// </summary>
    public class ExpressionResolver
    {
        /// <summary>
        /// 属性表达式
        /// </summary>
        private readonly ModelExpression _expression;

        /// <summary>
        /// 配置
        /// </summary>
        private readonly IConfig _config;

        /// <summary>
        /// 成员
        /// </summary>
        private readonly MemberInfo _memberInfo;

        /// <summary>
        /// 初始化一个<see cref="ExpressionResolver"/>类型的实例
        /// </summary>
        /// <param name="expression">属性表达式</param>
        /// <param name="config">配置</param>
        private ExpressionResolver(ModelExpression expression, IConfig config)
        {
            if (expression == null || config == null)
            {
                return;
            }

            _expression = expression;
            _config = config;
            _memberInfo = expression.GetMemberInfo();
        }

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="expression">属性表达式</param>
        /// <param name="config">配置</param>
        public static void Init(ModelExpression expression, IConfig config)
        {
            new ExpressionResolver(expression,config).Init();
        }

        /// <summary>
        /// 初始化
        /// </summary>
        private void Init()
        {
            Internal.Helper.Init(_config, _expression, _memberInfo);
        }
    }
}
