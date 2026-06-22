using Bing.Aop.AspectCore.Samples;
using Bing.Validation;
using Shouldly;
using Xunit;

namespace Bing.Aop.AspectCore;

/// <summary>
/// <see cref="ValidAttribute"/> 行为测试。
/// 依赖 Startup.cs 中配置的 AspectCore DI 管道（EnableParameterAspect）。
/// </summary>
public class ValidAttributeTest
{
    private readonly IValidTestService _service;

    /// <summary>
    /// 通过 Xunit.DependencyInjection 注入经 AOP 代理的服务
    /// </summary>
    public ValidAttributeTest(IValidTestService service)
    {
        _service = service;
    }

    /// <summary>
    /// 测试目的：当参数实现 IVerifyModel 且 Validate() 内部抛出异常时，
    /// AOP 管道应将异常向上传播，不吞掉。
    /// </summary>
    [Fact]
    public void ProcessObject_WhenParameterValidateThrows_ShouldPropagateException()
    {
        // Arrange
        var model = new ThrowingValidModel();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => _service.ProcessObject(model));
    }

    /// <summary>
    /// 测试目的：当参数不实现 IVerifyModel 时（如普通字符串），
    /// AOP 管道应直接透传，不抛异常。
    /// </summary>
    [Fact]
    public void ProcessObject_WhenParameterIsNotIVerifyModel_ShouldNotThrow()
    {
        // Act & Assert
        Should.NotThrow(() => _service.ProcessObject("just a plain string"));
    }

    /// <summary>
    /// 测试目的：当参数实现 IVerifyModel 且验证通过时，
    /// AOP 管道应调用 Validate()，TrackingValidModel 的 WasValidated 应为 true。
    /// </summary>
    [Fact]
    public void ProcessObject_WhenParameterIsValidIVerifyModel_ShouldCallValidate()
    {
        // Arrange
        var model = new TrackingValidModel();

        // Act
        _service.ProcessObject(model);

        // Assert
        model.WasValidated.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：ValidAttribute 应继承自 ParameterInterceptorBase，
    /// 确保类型层次结构正确，AOP 框架能识别并调用。
    /// </summary>
    [Fact]
    public void ValidAttribute_ShouldInheritFromParameterInterceptorBase()
    {
        // Assert
        typeof(ValidAttribute).BaseType.ShouldBe(typeof(Bing.Aspects.ParameterInterceptorBase));
    }
}
