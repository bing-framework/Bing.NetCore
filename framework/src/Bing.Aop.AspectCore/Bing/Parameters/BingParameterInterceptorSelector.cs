using System.Collections.Concurrent;
using System.Reflection;
using AspectCore.DependencyInjection;
using AspectCore.DynamicProxy.Parameters;
using Bing.Aspects;
using Bing.Extensions;
using Bing.Validation;
using AspectCore.DynamicProxy;
using BingNotEmptyAttribute = Bing.Aspects.NotEmptyAttribute;
using BingNotNullAttribute = Bing.Aspects.NotNullAttribute;

namespace Bing.DependencyInjection;

/// <summary>
/// 保存当前服务集合的参数验证启用状态。
/// </summary>
internal sealed class BingParameterValidationRegistration
{
    /// <summary>
    /// 获取或设置是否已启用参数验证。
    /// </summary>
    public bool Enabled { get; set; }
}

/// <summary>
/// 将 Bing 中立参数标记适配为 AspectCore 参数拦截器。
/// </summary>
internal sealed class BingParameterInterceptorSelector : IParameterInterceptorSelector
{
    /// <summary>
    /// 创建参数拦截器时使用的属性注入器工厂。
    /// </summary>
    private readonly IPropertyInjectorFactory _propertyInjectorFactory;
    /// <summary>
    /// 调用方预先注册的参数选择器。
    /// </summary>
    private readonly IReadOnlyList<IParameterInterceptorSelector> _fallbackSelectors;
    /// <summary>
    /// 当前服务集合的参数验证注册状态。
    /// </summary>
    private readonly BingParameterValidationRegistration _parameterValidationRegistration;
    /// <summary>
    /// 按参数元数据缓存选出的拦截器。
    /// </summary>
    private readonly ConcurrentDictionary<ParameterInfo, IParameterInterceptor[]> _cache = new();

    /// <summary>
    /// 初始化一个 <see cref="BingParameterInterceptorSelector" /> 类型的实例。
    /// </summary>
    /// <param name="propertyInjectorFactory">属性注入器工厂。</param>
    /// <param name="fallbackSelectors">调用方预先注册的参数选择器。</param>
    /// <param name="parameterValidationRegistration">参数验证注册状态。</param>
    public BingParameterInterceptorSelector(
        IPropertyInjectorFactory propertyInjectorFactory,
        IEnumerable<IBingParameterInterceptorSelector> fallbackSelectors,
        BingParameterValidationRegistration parameterValidationRegistration = null)
    {
        _propertyInjectorFactory = propertyInjectorFactory ?? throw new ArgumentNullException(nameof(propertyInjectorFactory));
        _fallbackSelectors = fallbackSelectors?.Select(x => x.Selector).Where(x => x != null).ToArray()
            ?? Array.Empty<IParameterInterceptorSelector>();
        _parameterValidationRegistration = parameterValidationRegistration;
    }

    /// <inheritdoc />
    public IParameterInterceptor[] Select(ParameterInfo parameter)
    {
        if (parameter == null)
            throw new ArgumentNullException(nameof(parameter));

        return _cache.GetOrAdd(parameter, SelectCore);
    }

    /// <summary>
    /// 创建指定参数的拦截器集合。
    /// </summary>
    /// <param name="parameter">参数信息。</param>
    /// <returns>拦截器集合。</returns>
    private IParameterInterceptor[] SelectCore(ParameterInfo parameter)
    {
        var interceptors = new List<IParameterInterceptor>();
        if (_fallbackSelectors.Count > 0)
        {
            foreach (var selector in _fallbackSelectors)
                AddRange(interceptors, selector.Select(parameter));
        }
        else
        {
            AddRange(interceptors, parameter.GetCustomAttributes(inherit: true).OfType<IParameterInterceptor>());
        }

        if (_parameterValidationRegistration?.Enabled == true &&
            parameter.IsDefined(typeof(Bing.Aspects.NotNullAttribute), inherit: true))
            AddOnce(interceptors, new BingNotNullParameterInterceptor());
        if (_parameterValidationRegistration?.Enabled == true &&
            parameter.IsDefined(typeof(Bing.Aspects.NotEmptyAttribute), inherit: true))
            AddOnce(interceptors, new BingNotEmptyParameterInterceptor());
        if (_parameterValidationRegistration?.Enabled == true &&
            parameter.IsDefined(typeof(ValidAttribute), inherit: true))
            AddOnce(interceptors, new BingValidParameterInterceptor());

        foreach (var interceptor in interceptors)
            _propertyInjectorFactory.Create(interceptor.GetType()).Invoke(interceptor);
        return interceptors.ToArray();
    }

    /// <summary>
    /// 添加拦截器并避免同一实例重复出现。
    /// </summary>
    /// <param name="target">目标集合。</param>
    /// <param name="source">待添加的拦截器。</param>
    private static void AddRange(ICollection<IParameterInterceptor> target, IEnumerable<IParameterInterceptor> source)
    {
        if (source == null)
            return;
        foreach (var interceptor in source)
            AddOnce(target, interceptor);
    }

    /// <summary>
    /// 添加尚未存在的拦截器类型。
    /// </summary>
    /// <param name="target">目标集合。</param>
    /// <param name="interceptor">待添加的拦截器。</param>
    private static void AddOnce(ICollection<IParameterInterceptor> target, IParameterInterceptor interceptor)
    {
        if (interceptor == null || target.Any(x => x.GetType() == interceptor.GetType()))
            return;
        target.Add(interceptor);
    }
}

/// <summary>
/// 保存调用方原有参数选择器的适配记录。
/// </summary>
internal interface IBingParameterInterceptorSelector
{
    /// <summary>
    /// 获取原有参数选择器。
    /// </summary>
    IParameterInterceptorSelector Selector { get; }
}

/// <summary>
/// 调用方原有参数选择器的服务描述适配器。
/// </summary>
internal sealed class BingParameterInterceptorSelectorAdapter : IBingParameterInterceptorSelector
{
    /// <summary>
    /// 调用方原有的参数选择器。
    /// </summary>
    private readonly IParameterInterceptorSelector _selector;

    /// <summary>
    /// 初始化一个 <see cref="BingParameterInterceptorSelectorAdapter" /> 类型的实例。
    /// </summary>
    /// <param name="selector">原有参数选择器。</param>
    public BingParameterInterceptorSelectorAdapter(IParameterInterceptorSelector selector)
    {
        _selector = selector ?? throw new ArgumentNullException(nameof(selector));
    }

    /// <inheritdoc />
    public IParameterInterceptorSelector Selector => _selector;
}

/// <summary>
/// 检查参数不得为 <see langword="null" />。
/// </summary>
internal sealed class BingNotNullParameterInterceptor : IParameterInterceptor
{
    /// <inheritdoc />
    public Task Invoke(ParameterAspectContext context, ParameterAspectDelegate next)
    {
        if (context.Parameter.Value == null)
            throw new ArgumentNullException(context.Parameter.Name);
        return next(context);
    }
}

/// <summary>
/// 检查参数不得为空文本。
/// </summary>
internal sealed class BingNotEmptyParameterInterceptor : IParameterInterceptor
{
    /// <inheritdoc />
    public Task Invoke(ParameterAspectContext context, ParameterAspectDelegate next)
    {
        if (string.IsNullOrWhiteSpace(context.Parameter.Value.SafeString()))
            throw new ArgumentNullException(context.Parameter.Name);
        return next(context);
    }
}

/// <summary>
/// 调用验证模型的参数拦截器。
/// </summary>
internal sealed class BingValidParameterInterceptor : IParameterInterceptor
{
    /// <inheritdoc />
    public async Task Invoke(ParameterAspectContext context, ParameterAspectDelegate next)
    {
        Validate(context.Parameter);
        await next(context).ConfigureAwait(false);
    }

    /// <summary>
    /// 验证单个参数或参数集合。
    /// </summary>
    /// <param name="parameter">参数信息。</param>
    private static void Validate(Parameter parameter)
    {
        if (Bing.Reflection.Reflections.IsGenericCollection(parameter.RawType))
        {
            if (parameter.Value is IEnumerable<IVerifyModel> validations)
            {
                foreach (var validation in validations)
                    validation.Validate();
            }
            return;
        }

        if (parameter.Value is IVerifyModel validationModel)
            validationModel.Validate();
    }
}

/// <summary>
/// 验证仅声明在实现方法参数上的中立标记。
/// </summary>
internal sealed class BingImplementationParameterValidationInterceptor : IInterceptor
{
    /// <inheritdoc />
    public bool AllowMultiple => false;

    /// <inheritdoc />
    public bool Inherited { get; set; } = true;

    /// <inheritdoc />
    public int Order { get; set; }

    /// <inheritdoc />
    public async Task Invoke(AspectContext context, AspectDelegate next)
    {
        Validate(context);
        await next(context).ConfigureAwait(false);
    }

    /// <summary>
    /// 判断实现方法是否包含接口方法未声明的验证标记。
    /// </summary>
    /// <param name="serviceMethod">服务方法。</param>
    /// <param name="implementationMethod">实现方法。</param>
    /// <returns>存在实现方法独有标记时返回 <see langword="true" />。</returns>
    public static bool HasImplementationMarkers(MethodInfo serviceMethod, MethodInfo implementationMethod)
    {
        var implementationParameters = implementationMethod?.GetParameters();
        if (implementationParameters == null)
            return false;

        var serviceParameters = serviceMethod?.GetParameters();
        for (var index = 0; index < implementationParameters.Length; index++)
        {
            var serviceParameter = serviceParameters != null && index < serviceParameters.Length
                ? serviceParameters[index]
                : null;
            if (HasImplementationMarker(implementationParameters[index], serviceParameter, typeof(BingNotNullAttribute)) ||
                HasImplementationMarker(implementationParameters[index], serviceParameter, typeof(BingNotEmptyAttribute)) ||
                HasImplementationMarker(implementationParameters[index], serviceParameter, typeof(ValidAttribute)))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 执行实现方法参数验证。
    /// </summary>
    /// <param name="context">切面上下文。</param>
    private static void Validate(AspectContext context)
    {
        var implementationParameters = context.ImplementationMethod?.GetParameters();
        if (implementationParameters == null)
            return;

        var serviceParameters = context.ServiceMethod?.GetParameters();
        for (var index = 0; index < implementationParameters.Length && index < context.Parameters.Length; index++)
        {
            var implementationParameter = implementationParameters[index];
            var serviceParameter = serviceParameters != null && index < serviceParameters.Length
                ? serviceParameters[index]
                : null;
            var value = context.Parameters[index];
            if (HasImplementationMarker(implementationParameter, serviceParameter, typeof(BingNotNullAttribute)) && value == null)
                throw new ArgumentNullException(implementationParameter.Name);
            if (HasImplementationMarker(implementationParameter, serviceParameter, typeof(BingNotEmptyAttribute)) &&
                string.IsNullOrWhiteSpace(value.SafeString()))
                throw new ArgumentNullException(implementationParameter.Name);
            if (HasImplementationMarker(implementationParameter, serviceParameter, typeof(ValidAttribute)))
                ValidateModel(implementationParameter, value);
        }
    }

    /// <summary>
    /// 判断实现参数是否包含服务参数未声明的指定标记。
    /// </summary>
    /// <param name="implementationParameter">实现方法参数。</param>
    /// <param name="serviceParameter">服务方法参数。</param>
    /// <param name="markerType">标记类型。</param>
    /// <returns>实现参数具有独有标记时返回 <see langword="true" />。</returns>
    private static bool HasImplementationMarker(
        ParameterInfo implementationParameter,
        ParameterInfo serviceParameter,
        Type markerType)
    {
        return implementationParameter?.IsDefined(markerType, inherit: true) == true &&
            serviceParameter?.IsDefined(markerType, inherit: true) != true;
    }

    /// <summary>
    /// 验证对象参数或其泛型集合元素。
    /// </summary>
    /// <param name="parameter">参数信息。</param>
    /// <param name="value">参数值。</param>
    private static void ValidateModel(ParameterInfo parameter, object value)
    {
        if (Bing.Reflection.Reflections.IsGenericCollection(parameter.ParameterType))
        {
            if (value is IEnumerable<IVerifyModel> validations)
                foreach (var validation in validations)
                    validation.Validate();
            return;
        }

        if (value is IVerifyModel validationModel)
            validationModel.Validate();
    }
}

/// <summary>
/// 标记已启用 Bing 参数验证。
/// </summary>
internal sealed class BingParameterValidationEnabledHandler : IAspectValidationHandler
{
    /// <inheritdoc />
    public int Order => int.MinValue;

    /// <inheritdoc />
    public bool Invoke(AspectValidationContext context, AspectValidationDelegate next)
    {
        return next(context);
    }
}
