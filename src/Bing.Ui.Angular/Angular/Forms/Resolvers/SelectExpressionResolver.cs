using System.Reflection;
using Bing.Ui.Angular.Forms.Configs;
using Bing.Ui.Extensions;
using Bing.Utils.Helpers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Bing.Ui.Angular.Forms.Resolvers
{
    /// <summary>
    /// 下拉列表表达式解析器
    /// </summary>
    public class SelectExpressionResolver
    {
        /// <summary>
        /// 属性表达式
        /// </summary>
        private readonly ModelExpression _expression;

        /// <summary>
        /// 配置
        /// </summary>
        private readonly SelectConfig _config;

        /// <summary>
        /// 成员
        /// </summary>
        private readonly MemberInfo _memberInfo;

        /// <summary>
        /// 初始化一个<see cref="SelectExpressionResolver"/>类型的实例
        /// </summary>
        /// <param name="expression">属性表达式</param>
        /// <param name="config">配置</param>
        private SelectExpressionResolver(ModelExpression expression, SelectConfig config)
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
        public static void Init(ModelExpression expression, SelectConfig config)
        {
            new SelectExpressionResolver(expression, config).Init();
        }

        /// <summary>
        /// 初始化
        /// </summary>
        private void Init()
        {
            Internal.Helper.Init(_config, _expression, _memberInfo);
            InitType();
        }

        /// <summary>
        /// 初始化类型
        /// </summary>
        private void InitType()
        {
            if (Reflection.IsBool(_memberInfo))
            {
                _config.AddBool();
            }
            else if(Reflection.IsEnum(_memberInfo))
            {
                _config.AddEnum(_expression.Metadata.ModelType);
            }
        }
    }
}
