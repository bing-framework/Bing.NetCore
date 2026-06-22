using System.ComponentModel.DataAnnotations;
using Bing.Validation;
using Shouldly;
using Xunit;

namespace Bing.Validation.Tests;

/// <summary>
/// <see cref="ValidationResultCollection"/> 高级构造函数与方法的单元测试
/// </summary>
public class ValidationResultCollectionAdvancedTest
{
    // ── Static Success ─────────────────────────────────────────────

    /// <summary>
    /// 测试目的：静态 Success 属性应为有效的空集合（IsValid=true，Count=0）。
    /// </summary>
    [Fact]
    public void Success_ShouldBeValidAndEmpty()
    {
        // Arrange & Act
        var success = ValidationResultCollection.Success;

        // Assert
        success.IsValid.ShouldBeTrue();
        success.Count.ShouldBe(0);
    }

    // ── Constructor(ValidationResult) ──────────────────────────────

    /// <summary>
    /// 测试目的：通过 ValidationResult 构造时，集合应包含该条目且 IsValid=false。
    /// </summary>
    [Fact]
    public void Constructor_WithValidationResult_ShouldContainOneError()
    {
        // Arrange
        var result = new ValidationResult("字段错误", new[] { "Field1" });

        // Act
        var col = new ValidationResultCollection(result);

        // Assert
        col.IsValid.ShouldBeFalse();
        col.Count.ShouldBe(1);
        col.First().ErrorMessage.ShouldBe("字段错误");
    }

    /// <summary>
    /// 测试目的：传入 null 的 ValidationResult 应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void Constructor_WithNullValidationResult_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new ValidationResultCollection((ValidationResult)null));
    }

    // ── Constructor(ValidationResult, strategyName) ────────────────

    /// <summary>
    /// 测试目的：带策略名构造时，结果仍应被正确存入集合，IsValid=false。
    /// </summary>
    [Fact]
    public void Constructor_WithValidationResultAndStrategyName_ShouldContainOneError()
    {
        // Arrange
        var result = new ValidationResult("策略错误");

        // Act
        var col = new ValidationResultCollection(result, "myStrategy");

        // Assert
        col.IsValid.ShouldBeFalse();
        col.Count.ShouldBe(1);
    }

    // ── Constructor(IEnumerable<ValidationResult>) ─────────────────

    /// <summary>
    /// 测试目的：通过多条 ValidationResult 集合构造时，Count 应正确反映。
    /// </summary>
    [Fact]
    public void Constructor_WithEnumerableResults_ShouldContainAllErrors()
    {
        // Arrange
        var results = new[]
        {
            new ValidationResult("错误A"),
            new ValidationResult("错误B"),
        };

        // Act
        var col = new ValidationResultCollection(results);

        // Assert
        col.Count.ShouldBe(2);
        col.IsValid.ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：传入 null 的枚举集合应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void Constructor_WithNullEnumerable_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new ValidationResultCollection((IEnumerable<ValidationResult>)null));
    }

    // ── Copy Constructor(ValidationResultCollection) ───────────────

    /// <summary>
    /// 测试目的：拷贝构造时，应复制 ErrorCode、Flag 与所有错误条目。
    /// </summary>
    [Fact]
    public void Constructor_CopyFromOther_ShouldCopyErrorCodeFlagAndResults()
    {
        // Arrange
        var source = new ValidationResultCollection("原始错误");
        source.ErrorCode = 9999;
        source.Flag = "MY_FLAG";

        // Act
        var copy = new ValidationResultCollection(source);

        // Assert
        copy.Count.ShouldBe(1);
        copy.ErrorCode.ShouldBe(9999L);
        copy.Flag.ShouldBe("MY_FLAG");
    }

    /// <summary>
    /// 测试目的：传入 null 的源集合应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void Constructor_CopyFromNull_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new ValidationResultCollection((ValidationResultCollection)null));
    }

    // ── ErrorCode / Flag defaults ──────────────────────────────────

    /// <summary>
    /// 测试目的：默认构造后，ErrorCode 应为 1001，Flag 应为 "__EMPTY_FLG"。
    /// </summary>
    [Fact]
    public void DefaultConstructor_ShouldHaveDefaultErrorCodeAndFlag()
    {
        // Arrange & Act
        var col = new ValidationResultCollection();

        // Assert
        col.ErrorCode.ShouldBe(1001L);
        col.Flag.ShouldBe("__EMPTY_FLG");
    }

    /// <summary>
    /// 测试目的：ErrorCode 和 Flag 属性可被外部设置并正确存储。
    /// </summary>
    [Fact]
    public void ErrorCodeAndFlag_CanBeSetExternally()
    {
        // Arrange
        var col = new ValidationResultCollection();

        // Act
        col.ErrorCode = 5000L;
        col.Flag = "CUSTOM_FLAG";

        // Assert
        col.ErrorCode.ShouldBe(5000L);
        col.Flag.ShouldBe("CUSTOM_FLAG");
    }

    // ── ToMessage ─────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：有效（空）集合的 ToMessage 应包含"未发现验证错误"。
    /// </summary>
    [Fact]
    public void ToMessage_WhenValid_ShouldContainNoErrorText()
    {
        // Arrange
        var col = new ValidationResultCollection();

        // Act
        var msg = col.ToMessage();

        // Assert
        msg.ShouldContain("未发现验证错误");
    }

    /// <summary>
    /// 测试目的：1 条错误时 ToMessage 应包含"发现1个验证错误"。
    /// </summary>
    [Fact]
    public void ToMessage_WhenOneError_ShouldContainOneErrorText()
    {
        // Arrange
        var col = new ValidationResultCollection("单条错误");

        // Act
        var msg = col.ToMessage();

        // Assert
        msg.ShouldContain("发现1个验证错误");
    }

    /// <summary>
    /// 测试目的：多条错误时 ToMessage 应包含正确的错误数量。
    /// </summary>
    [Fact]
    public void ToMessage_WhenMultipleErrors_ShouldContainCorrectCount()
    {
        // Arrange
        var col = new ValidationResultCollection();
        col.Add(new ValidationResult("错误1"));
        col.Add(new ValidationResult("错误2"));

        // Act
        var msg = col.ToMessage();

        // Assert
        msg.ShouldContain("发现2个验证错误");
    }

    // ── ToValidationMessages ───────────────────────────────────────

    /// <summary>
    /// 测试目的：有效集合的 ToValidationMessages 应返回空序列（非 null）。
    /// </summary>
    [Fact]
    public void ToValidationMessages_WhenValid_ShouldReturnEmpty()
    {
        // Arrange
        var col = new ValidationResultCollection();

        // Act
        var msgs = col.ToValidationMessages().ToList();

        // Assert
        msgs.ShouldNotBeNull();
        msgs.ShouldBeEmpty();
    }

    /// <summary>
    /// 测试目的：无效集合的 ToValidationMessages 每条消息应包含成员名与错误描述。
    /// </summary>
    [Fact]
    public void ToValidationMessages_WhenInvalid_ShouldContainMemberNameAndMessage()
    {
        // Arrange
        var col = new ValidationResultCollection();
        col.Add(new ValidationResult("姓名格式有误", new[] { "Name" }));

        // Act
        var msgs = col.ToValidationMessages().ToList();

        // Assert
        msgs.Count.ShouldBe(1);
        msgs[0].ShouldContain("Name");
        msgs[0].ShouldContain("姓名格式有误");
    }

    // ── Add(null) / AddRange(null) no-op ──────────────────────────

    /// <summary>
    /// 测试目的：Add(null) 应为空操作，Count 保持不变，不抛异常。
    /// </summary>
    [Fact]
    public void Add_Null_ShouldBeNoOp()
    {
        // Arrange
        var col = new ValidationResultCollection();

        // Act
        Should.NotThrow(() => col.Add(null));

        // Assert
        col.Count.ShouldBe(0);
        col.IsValid.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：AddRange(null) 应为空操作，不抛异常，Count 保持不变。
    /// </summary>
    [Fact]
    public void AddRange_Null_ShouldBeNoOp()
    {
        // Arrange
        var col = new ValidationResultCollection();

        // Act
        Should.NotThrow(() => col.AddRange(null));

        // Assert
        col.Count.ShouldBe(0);
    }

    // ── IEnumerable ────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：集合应支持 foreach 遍历并返回所有 ValidationResult。
    /// </summary>
    [Fact]
    public void GetEnumerator_ShouldIterateAllResults()
    {
        // Arrange
        var col = new ValidationResultCollection();
        col.Add(new ValidationResult("E1"));
        col.Add(new ValidationResult("E2"));

        // Act
        var list = col.ToList();

        // Assert
        list.Count.ShouldBe(2);
        list[0].ErrorMessage.ShouldBe("E1");
        list[1].ErrorMessage.ShouldBe("E2");
    }
}
