using System;
using System.Collections.Generic;
using System.Linq;
using Bing.Extensions.Swashbuckle.Extensions;
using Microsoft.AspNetCore.Authorization;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Bing.Extensions.Swashbuckle.Filters.Operations
{
    /// <summary>
    /// 添加安全请求 操作过滤器
    /// </summary>
    public class AddSecurityRequirementsOperationFilter : IOperationFilter
    {
        /// <summary>
        /// 过滤器
        /// </summary>
        private readonly AddSecurityRequirementsOperationFilter<AuthorizeAttribute> _filter;

        /// <summary>
        /// 初始化一个<see cref="AddSecurityRequirementsOperationFilter"/>类型的实例
        /// </summary>
        /// <param name="includeUnauthorizedAndForbiddenResponses">是否包含未授权或被禁止的响应，如果为true,则将为每个操作添加401、403响应</param>
        public AddSecurityRequirementsOperationFilter(bool includeUnauthorizedAndForbiddenResponses = true)
        {
            IEnumerable<string> PolicySelector(IEnumerable<AuthorizeAttribute> authAttributes) =>
                authAttributes.Where(x => !string.IsNullOrWhiteSpace(x.Policy)).Select(x => x.Policy);
            _filter = new AddSecurityRequirementsOperationFilter<AuthorizeAttribute>(PolicySelector,
                includeUnauthorizedAndForbiddenResponses);
        }

        /// <summary>
        /// 重写操作处理
        /// </summary>
        /// <param name="operation">当前操作</param>
        /// <param name="context">操作过滤器上下文</param>
        public void Apply(Operation operation, OperationFilterContext context)
        {
            _filter.Apply(operation, context);
        }
    }

    /// <summary>
    /// 添加安全请求 操作过滤器
    /// </summary>
    /// <typeparam name="TAttribute">特性类型</typeparam>
    public class AddSecurityRequirementsOperationFilter<TAttribute> :IOperationFilter where TAttribute : Attribute
    {
        /// <summary>
        /// 是否包含未授权或被禁止的响应
        /// </summary>
        private readonly bool _includeUnauthorizedAndForbiddenResponses;

        /// <summary>
        /// 授权策略选择器，从授权特性中选择授权策略
        /// </summary>
        private readonly Func<IEnumerable<TAttribute>, IEnumerable<string>> _policySelector;

        /// <summary>
        /// 初始化一个<see cref="AddSecurityRequirementsOperationFilter{TAttribute}"/>类型的实例
        /// </summary>
        /// <param name="policySelector">授权策略选择器，从授权特性中选择授权策略。范例：(t => t.Policy)</param>
        /// <param name="includeUnauthorizedAndForbiddenResponses">是否包含未授权或被禁止的响应，如果为true,则将为每个操作添加401、403响应</param>
        public AddSecurityRequirementsOperationFilter(Func<IEnumerable<TAttribute>, IEnumerable<string>> policySelector,
            bool includeUnauthorizedAndForbiddenResponses = true)
        {
            this._policySelector = policySelector;
            this._includeUnauthorizedAndForbiddenResponses = includeUnauthorizedAndForbiddenResponses;
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
            var actionAttributes = context.GetControllerAndActionAttributes<TAttribute>().ToList();
            if (!actionAttributes.Any())
            {
                return;
            }

            if (_includeUnauthorizedAndForbiddenResponses)
            {
                operation.Responses.Add("401", new Response() {Description = "Unauthorized"});
                operation.Responses.Add("403", new Response() { Description = "Forbidden" });
            }

            var policies = _policySelector(actionAttributes) ?? Enumerable.Empty<string>();
            operation.Security = new List<IDictionary<string, IEnumerable<string>>>()
            {
                new Dictionary<string, IEnumerable<string>>
                {
                    {"oauth2", policies}
                }
            };
        }
    }
}
