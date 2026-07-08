using System.ComponentModel.DataAnnotations;
using Bing.AspNetCore.Mvc.Validation;
using Bing.Validation;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Moq;
using Shouldly;
using Xunit;

namespace Bing.AspNetCore.Mvc.Validation;

/// <summary>
/// <see cref="ModelStateValidator"/> 单元测试
/// </summary>
public class ModelStateValidatorTest
{
    private readonly ModelStateValidator _validator = new();

    // ═══════════════════════════════════════════════════════════
    // AddErrors
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：ModelState 有效时，AddErrors 不应向 validationResult 添加任何错误，
    /// 确保有效请求不会被意外标记为失败。
    /// </summary>
    [Fact]
    public void AddErrors_WhenModelStateIsValid_ShouldAddNoErrors()
    {
        // Arrange
        var modelState = new ModelStateDictionary();
        var result = new ValidationResultCollection();

        // Act
        _validator.AddErrors(result, modelState);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Count.ShouldBe(0);
    }

    /// <summary>
    /// 测试目的：ModelState 包含一个错误时，AddErrors 应将该错误添加到 validationResult，
    /// 确保字段级验证错误可以被正确收集。
    /// </summary>
    [Fact]
    public void AddErrors_WhenModelStateHasOneError_ShouldAddOneError()
    {
        // Arrange
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Name", "Name is required");
        var result = new ValidationResultCollection();

        // Act
        _validator.AddErrors(result, modelState);

        // Assert
        result.Count.ShouldBe(1);
        result.IsValid.ShouldBeFalse();
        result.First().ErrorMessage.ShouldBe("Name is required");
        result.First().MemberNames.ShouldContain("Name");
    }

    /// <summary>
    /// 测试目的：ModelState 包含多个字段错误时，AddErrors 应全部收集，
    /// 确保批量验证场景下所有错误不丢失。
    /// </summary>
    [Fact]
    public void AddErrors_WhenModelStateHasMultipleErrors_ShouldAddAll()
    {
        // Arrange
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Name", "Name required");
        modelState.AddModelError("Email", "Email invalid");
        var result = new ValidationResultCollection();

        // Act
        _validator.AddErrors(result, modelState);

        // Assert
        result.Count.ShouldBe(2);
    }

    /// <summary>
    /// 测试目的：同一字段有多个错误时，AddErrors 应逐条收集，
    /// 确保单字段多验证规则场景下所有错误均被记录。
    /// </summary>
    [Fact]
    public void AddErrors_WhenSameFieldHasMultipleErrors_ShouldAddEachError()
    {
        // Arrange
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Name", "Name is required");
        modelState.AddModelError("Name", "Name must be at least 2 characters");
        var result = new ValidationResultCollection();

        // Act
        _validator.AddErrors(result, modelState);

        // Assert
        result.Count.ShouldBe(2);
        result.All(r => r.MemberNames.Contains("Name")).ShouldBeTrue();
    }

    // ═══════════════════════════════════════════════════════════
    // Validate
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：ModelState 有效时，Validate 应不抛异常，
    /// 确保正常请求不受验证器影响。
    /// </summary>
    [Fact]
    public void Validate_WhenModelStateIsValid_ShouldNotThrow()
    {
        // Arrange
        var modelState = new ModelStateDictionary();

        // Act & Assert
        Should.NotThrow(() => _validator.Validate(modelState));
    }

    /// <summary>
    /// 测试目的：ModelState 包含错误时，Validate 当前实现不抛异常（早返回），
    /// 确保调用方不会因 Validate 本身崩溃。
    /// </summary>
    [Fact]
    public void Validate_WhenModelStateHasErrors_ShouldNotThrow()
    {
        // Arrange
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Field", "Error message");

        // Act & Assert（当前实现在 IsValid 为 false 时直接 return，不抛异常）
        Should.NotThrow(() => _validator.Validate(modelState));
    }
}
