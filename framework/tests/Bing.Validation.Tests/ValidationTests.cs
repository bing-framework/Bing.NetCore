using System.ComponentModel.DataAnnotations;
using Bing.Validation;
using Shouldly;
using Xunit;

namespace Bing.Validation.Tests;

// ─── 测试用模型 ────────────────────────────────────────────────────

internal class ValidModel
{
    [Required(ErrorMessage = "姓名不能为空")]
    [StringLength(20, ErrorMessage = "姓名不超过20个字符")]
    public string Name { get; set; } = "测试";

    [Range(0, 150, ErrorMessage = "年龄必须在 0~150 之间")]
    public int Age { get; set; } = 25;
}

internal class InvalidModel
{
    [Required(ErrorMessage = "姓名不能为空")]
    public string Name { get; set; } // null → 验证失败

    [Range(1, 10, ErrorMessage = "数值必须在 1~10 之间")]
    public int Value { get; set; } = 100; // 超出范围
}

internal class MultiErrorModel
{
    [Required(ErrorMessage = "字段A不能为空")]
    public string FieldA { get; set; }

    [Required(ErrorMessage = "字段B不能为空")]
    public string FieldB { get; set; }
}

/// <summary>
/// <see cref="ValidationResultCollection"/> 单元测试
/// </summary>
public class ValidationResultCollectionTest
{
    // ── IsValid ────────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：空集合应报告有效（无错误）。
    /// </summary>
    [Fact]
    public void IsValid_WhenEmpty_ShouldBeTrue()
    {
        // Arrange & Act
        var col = new ValidationResultCollection();

        // Assert
        col.IsValid.ShouldBeTrue();
        col.Count.ShouldBe(0);
    }

    /// <summary>
    /// 测试目的：添加 ValidationResult 后应报告无效。
    /// </summary>
    [Fact]
    public void IsValid_AfterAddError_ShouldBeFalse()
    {
        // Arrange
        var col = new ValidationResultCollection();

        // Act
        col.Add(new ValidationResult("字段错误"));

        // Assert
        col.IsValid.ShouldBeFalse();
        col.Count.ShouldBe(1);
    }

    // ── 构造函数 ──────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：从字符串构造时，集合应包含对应的单条错误消息。
    /// </summary>
    [Fact]
    public void Constructor_WithString_ShouldContainSingleError()
    {
        // Arrange & Act
        var col = new ValidationResultCollection("初始错误消息");

        // Assert
        col.IsValid.ShouldBeFalse();
        col.Count.ShouldBe(1);
        col.First().ErrorMessage.ShouldBe("初始错误消息");
    }

    /// <summary>
    /// 测试目的：从空字符串构造时，集合应为空（有效）。
    /// </summary>
    [Fact]
    public void Constructor_WithEmptyString_ShouldBeValid()
    {
        // Arrange & Act
        var col = new ValidationResultCollection(string.Empty);

        // Assert
        col.IsValid.ShouldBeTrue();
    }

    // ── AddRange ──────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：AddRange 应一次性加入多条错误，Count 正确反映总数。
    /// </summary>
    [Fact]
    public void AddRange_ShouldAddAllErrors()
    {
        // Arrange
        var col = new ValidationResultCollection();
        var errors = new[]
        {
            new ValidationResult("错误1"),
            new ValidationResult("错误2"),
            new ValidationResult("错误3"),
        };

        // Act
        col.AddRange(errors);

        // Assert
        col.Count.ShouldBe(3);
        col.IsValid.ShouldBeFalse();
    }

    // ── GetErrors ─────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：有效时 GetErrors 应返回空序列，不为 null。
    /// </summary>
    [Fact]
    public void GetErrors_WhenValid_ShouldReturnEmpty()
    {
        // Arrange
        var col = new ValidationResultCollection();

        // Act
        var errors = col.GetErrors();

        // Assert
        errors.ShouldNotBeNull();
        errors.ShouldBeEmpty();
    }

    /// <summary>
    /// 测试目的：无效时 GetErrors 应返回所有错误的字符串描述。
    /// </summary>
    [Fact]
    public void GetErrors_WhenInvalid_ShouldReturnErrorDescriptions()
    {
        // Arrange
        var col = new ValidationResultCollection();
        col.Add(new ValidationResult("字段A错误", new[] { "FieldA" }));

        // Act
        var errors = col.GetErrors().ToList();

        // Assert
        errors.ShouldNotBeEmpty();
        errors[0].ShouldContain("FieldA");
    }

    // ── ToString ──────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：有效时 ToString 应返回空字符串（或者不含错误描述）。
    /// </summary>
    [Fact]
    public void ToString_WhenValid_ShouldReturnEmptyOrSpecialString()
    {
        // Arrange
        var col = new ValidationResultCollection();

        // Act
        var str = col.ToString();

        // Assert — 有效时不应包含错误描述内容
        str.ShouldNotContain("验证错误");
    }

    /// <summary>
    /// 测试目的：含多个错误时 ToString 应包含"验证错误"关键字（中文提示文本）。
    /// </summary>
    [Fact]
    public void ToString_WhenMultipleErrors_ShouldContainErrorCountKeyword()
    {
        // Arrange
        var col = new ValidationResultCollection();
        col.Add(new ValidationResult("错误A"));
        col.Add(new ValidationResult("错误B"));

        // Act
        var str = col.ToString();

        // Assert
        str.ShouldContain("验证错误");
    }
}

/// <summary>
/// <see cref="DataAnnotationValidation"/> 单元测试
/// </summary>
public class DataAnnotationValidationTest
{
    // ── 有效模型 ───────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：满足所有 DataAnnotation 约束的模型应验证通过。
    /// </summary>
    [Fact]
    public void Validate_WithValidModel_ShouldReturnValidResult()
    {
        // Arrange
        var model = new ValidModel { Name = "张三", Age = 30 };

        // Act
        var result = DataAnnotationValidation.Validate(model);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    // ── 无效模型 ───────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：违反 [Required] 和 [Range] 约束的模型应返回相应错误信息。
    /// </summary>
    [Fact]
    public void Validate_WithInvalidModel_ShouldReturnErrors()
    {
        // Arrange
        var model = new InvalidModel { Name = null, Value = 100 };

        // Act
        var result = DataAnnotationValidation.Validate(model);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    /// <summary>
    /// 测试目的：多字段违规时，所有错误都应被收集（非 fail-fast）。
    /// </summary>
    [Fact]
    public void Validate_WithMultipleViolations_ShouldCollectAllErrors()
    {
        // Arrange
        var model = new MultiErrorModel { FieldA = null, FieldB = null };

        // Act
        var result = DataAnnotationValidation.Validate(model);

        // Assert
        result.Count.ShouldBe(2);
    }

    // ── null 边界 ─────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：传入 null 时应抛出 ArgumentNullException，而非 NullReferenceException。
    /// </summary>
    [Fact]
    public void Validate_WhenTargetIsNull_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => DataAnnotationValidation.Validate(null));
    }
}

/// <summary>
/// <see cref="ThrowHandler"/> 和 <see cref="NothingHandler"/> 单元测试
/// </summary>
public class ValidationHandlerTest
{
    // ── ThrowHandler ───────────────────────────────────────────────

    /// <summary>
    /// 测试目的：ThrowHandler 在验证失败时应抛出 Warning 异常。
    /// </summary>
    [Fact]
    public void ThrowHandler_WhenInvalid_ShouldThrowWarning()
    {
        // Arrange
        var handler = new ThrowHandler();
        var result = new ValidationResultCollection("验证失败：字段X不合法");

        // Act & Assert
        Should.Throw<Bing.Exceptions.Warning>(() => handler.Handle(result));
    }

    /// <summary>
    /// 测试目的：ThrowHandler 在验证通过时不应抛出任何异常。
    /// </summary>
    [Fact]
    public void ThrowHandler_WhenValid_ShouldNotThrow()
    {
        // Arrange
        var handler = new ThrowHandler();
        var result = new ValidationResultCollection();

        // Act & Assert
        Should.NotThrow(() => handler.Handle(result));
    }

    // ── NothingHandler ─────────────────────────────────────────────

    /// <summary>
    /// 测试目的：NothingHandler 无论验证结果如何都不应抛出任何异常（空操作）。
    /// </summary>
    [Fact]
    public void NothingHandler_WhenInvalid_ShouldNotThrow()
    {
        // Arrange
        var handler = new NothingHandler();
        var result = new ValidationResultCollection("验证失败");

        // Act & Assert
        Should.NotThrow(() => handler.Handle(result));
    }

    /// <summary>
    /// 测试目的：NothingHandler 对有效结果也不抛异常（确认接口完整性）。
    /// </summary>
    [Fact]
    public void NothingHandler_WhenValid_ShouldNotThrow()
    {
        // Arrange
        var handler = new NothingHandler();
        var result = new ValidationResultCollection();

        // Act & Assert
        Should.NotThrow(() => handler.Handle(result));
    }
}
