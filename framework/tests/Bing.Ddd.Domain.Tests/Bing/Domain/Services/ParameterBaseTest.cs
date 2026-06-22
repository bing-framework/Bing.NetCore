using System.ComponentModel.DataAnnotations;
using Bing.Domain.Services;
using Bing.Exceptions;
using Shouldly;
using Xunit;

namespace Bing.Domain.Services;

// =========================================================================
//  ParameterBase Tests
// =========================================================================

/// <summary>
/// 测试目的：验证 ParameterBase.Validate() 在通过/失败两种情况下的行为。
/// </summary>
public class ParameterBaseTest
{
    // ----- 具体实现 -----

    /// <summary>合法参数样例：Name 必填，已满足</summary>
    private class ValidParam : ParameterBase
    {
        [Required(ErrorMessage = "Name 不能为空")]
        public string Name { get; set; } = "Alice";
    }

    /// <summary>非法参数样例：Name 必填，但为空</summary>
    private class InvalidParam : ParameterBase
    {
        [Required(ErrorMessage = "Name 不能为空")]
        public string Name { get; set; }
    }

    /// <summary>无约束参数样例：无 DataAnnotation，始终合法</summary>
    private class UnconstrainedParam : ParameterBase
    {
        public string Remark { get; set; }
    }

    // ----- 测试 -----

    /// <summary>
    /// 测试目的：参数满足所有 DataAnnotation 约束时，Validate 应返回成功结果，不抛异常。
    /// </summary>
    [Fact]
    public void Validate_ValidParam_ShouldReturnSuccess()
    {
        // Arrange
        var param = new ValidParam();

        // Act & Assert
        var result = Should.NotThrow(() => param.Validate());
        result.IsValid.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：参数违反 [Required] 约束时，Validate 应抛出 Warning 异常，包含约束消息。
    /// </summary>
    [Fact]
    public void Validate_InvalidParam_ShouldThrowWarning()
    {
        // Arrange
        var param = new InvalidParam(); // Name == null

        // Act & Assert
        var ex = Should.Throw<Warning>(() => param.Validate());
        ex.Message.ShouldContain("Name 不能为空");
    }

    /// <summary>
    /// 测试目的：无任何约束的参数，Validate 应返回成功，不抛异常。
    /// </summary>
    [Fact]
    public void Validate_NoConstraints_ShouldReturnSuccess()
    {
        // Arrange
        var param = new UnconstrainedParam { Remark = "test" };

        // Act & Assert
        var result = Should.NotThrow(() => param.Validate());
        result.IsValid.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：可重写 Validate 方法，派生类自定义验证逻辑可正常工作。
    /// </summary>
    [Fact]
    public void Validate_OverriddenValidate_ShouldWorkCorrectly()
    {
        // Arrange: 重写 Validate 直接返回 Success
        var param = new CustomValidatedParam { Name = null };

        // Act & Assert — 不抛异常（自定义实现跳过校验）
        var result = Should.NotThrow(() => param.Validate());
        result.IsValid.ShouldBeTrue();
    }

    /// <summary>自定义验证参数：直接绕过 DataAnnotation</summary>
    private class CustomValidatedParam : ParameterBase
    {
        [Required]
        public string Name { get; set; }

        public override Bing.Validation.IValidationResult Validate()
            => Bing.Validation.ValidationResultCollection.Success;
    }
}
