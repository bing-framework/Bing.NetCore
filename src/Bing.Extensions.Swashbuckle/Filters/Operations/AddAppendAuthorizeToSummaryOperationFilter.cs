using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.Extensions.Swashbuckle.Extensions;
using Microsoft.AspNetCore.Authorization;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Bing.Extensions.Swashbuckle.Filters.Operations
{
    /// <summary>
    /// 添加 追加授权信息到注释 操作过滤器
    /// </summary>
    public class AddAppendAuthorizeToSummaryOperationFilter : IOperationFilter
    {
        /// <summary>
        /// 过滤器
        /// </summary>
        private readonly AddAppendAuthorizeToSummaryOperationFilter<AuthorizeAttribute> _filter;

        /// <summary>
        /// 初始化一个<see cref="AddAppendAuthorizeToSummaryOperationFilter"/>类型的实例
        /// </summary>
        public AddAppendAuthorizeToSummaryOperationFilter()
        {
            var policySelector = new PolicySelectorWithLabel<AuthorizeAttribute>()
            {
                Label = "policies",
                Selector = authAttributes =>
                    authAttributes.Where(x => string.IsNullOrWhiteSpace(x.Policy)).Select(x => x.Policy)
            };

            var rolesSelector=new PolicySelectorWithLabel<AuthorizeAttribute>()
            {
                Label = "roles",
                Selector = authAttributes =>
                    authAttributes.Where(x => string.IsNullOrWhiteSpace(x.Roles)).Select(x => x.Roles)
            };
            _filter = new AddAppendAuthorizeToSummaryOperationFilter<AuthorizeAttribute>(
                new[] {policySelector, rolesSelector}.AsEnumerable());
        }

        /// <summary>
        /// 重写操作处理
        /// </summary>
        /// <param name="operation">当前操作</param>
        /// <param name="context">操作过滤器上下文</param>
        public void Apply(Operation operation, OperationFilterContext context)
        {
            _filter.Apply(operation,context);
        }
    }

    /// <summary>
    /// 添加 追加授权信息到注释 操作过滤器
    /// </summary>
    /// <typeparam name="TAttribute">特性类型</typeparam>
    public class AddAppendAuthorizeToSummaryOperationFilter<TAttribute> : IOperationFilter where TAttribute : Attribute
    {
        /// <summary>
        /// 授权选择器标签列表
        /// </summary>
        private readonly IEnumerable<PolicySelectorWithLabel<TAttribute>> _policySelectors;

        /// <summary>
        /// 初始化一个<see cref="AddAppendAuthorizeToSummaryOperationFilter{TAttribute}"/>类型的实例
        /// </summary>
        /// <param name="policySelectors">授权选择器标签列表</param>
        public AddAppendAuthorizeToSummaryOperationFilter(
            IEnumerable<PolicySelectorWithLabel<TAttribute>> policySelectors)
        {
            _policySelectors = policySelectors;
        }

        /// <summary>
        /// 重写操作处理
        /// </summary>
        /// <param name="operation">当前操作</param>
        /// <param name="context">操作过滤器上下文</param>
        public void Apply(Operation operation, OperationFilterContext context)
        {
            if (context.GetControllerAndActionAttributes<AllowAnonymousAttribute>().Any())
            {
                return;
            }

            var authorizeAttributes = context.GetControllerAndActionAttributes<TAttribute>().ToList();
            if (authorizeAttributes.Any())
            {
                var authorizationDescription=new StringBuilder(" (Auth");
                foreach (var policySelector in _policySelectors)
                {
                    AppendPolicies(authorizeAttributes, authorizationDescription, policySelector);
                }

                operation.Summary += authorizationDescription.ToString().TrimEnd(';') + ")";
            }
        }

        /// <summary>
        /// 追加授权信息
        /// </summary>
        /// <param name="authorizeAttributes">授权特性列表</param>
        /// <param name="authorizationDescription">授权说明</param>
        /// <param name="policySelector">授权选择器</param>
        private void AppendPolicies(IEnumerable<TAttribute> authorizeAttributes, StringBuilder authorizationDescription,
            PolicySelectorWithLabel<TAttribute> policySelector)
        {
            var policies = policySelector.Selector(authorizeAttributes).OrderBy(policy => policy).ToList();
            if (policies.Any())
            {
                authorizationDescription.Append($" {policySelector.Label}: {string.Join(", ", policies)};");
            }
        }
    }
}
