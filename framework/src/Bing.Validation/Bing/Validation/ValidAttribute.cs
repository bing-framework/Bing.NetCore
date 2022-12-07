﻿using AspectCore.DynamicProxy.Parameters;
using Bing.Aspects;

namespace Bing.Validation;

/// <summary>
/// 验证拦截器
/// </summary>
public class ValidAttribute : ParameterInterceptorBase
{
    /// <summary>
    /// 执行
    /// </summary>
    public override async Task Invoke(ParameterAspectContext context, ParameterAspectDelegate next)
    {
        Validate(context.Parameter);
        await next(context);
    }

    /// <summary>
    /// 验证
    /// </summary>
    /// <param name="parameter">参数</param>
    private void Validate(Parameter parameter)
    {
        if (Reflection.Reflections.IsGenericCollection(parameter.RawType))
        {
            ValidateCollection(parameter);
            return;
        }
        var validation = parameter.Value as IVerifyModel;
        validation?.Validate();
    }

    /// <summary>
    /// 验证集合
    /// </summary>
    /// <param name="parameter">参数</param>
    private void ValidateCollection(Parameter parameter)
    {
        if (!(parameter.Value is IEnumerable<IVerifyModel> validations))
            return;
        foreach (var validation in validations)
            validation.Validate();
    }
}
