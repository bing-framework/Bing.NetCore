using System.ComponentModel.DataAnnotations;
using Bing.Validation;
using Shouldly;
using Xunit;

namespace Bing.Validation.Tests;

/// <summary>
/// <see cref="ValidationResultCollection"/> 单元测试
/// </summary>
public class ValidationResultCollectionTest
{
    // ═══════════════════════════════════════════════════════════
    // 构造函数 - 默认
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：默认构造后集合为空，IsValid 应为 true，Count = 0。
    /// </summary>
    [Fact]
    public void Default_ShouldBeEmptyAndValid()
    {
        // Arrange & Act
        var collection = new ValidationResultCollection();

        // Assert
        collection.Count.ShouldBe(0);
        collection.IsValid.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：静态 Success 实例应为空集合，IsValid = true。
    /// </summary>
    [Fact]
    public void Success_ShouldBeValidAndEmpty()
    {
        // Assert
        ValidationResultCollection.Success.IsValid.ShouldBeTrue();
        ValidationResultCollection.Success.Count.ShouldBe(0);
    }

    // ═══════════════════════════════════════════════════════════
    // 构造函数 - string
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：使用非空错误字符串构造时，Count = 1，IsValid = false。
    /// </summary>
    [Fact]
    public void Ctor_WithErrorString_ShouldContainOneResult()
    {
        // Arrange & Act
        var collection = new ValidationResultCollection("姓名不能为空");

        // Assert
        collection.Count.ShouldBe(1);
        collection.IsValid.ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：使用 null 或空白字符串构造时，集合应为空，IsValid = true。
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ctor_WithNullOrWhiteSpace_ShouldBeEmpty(string message)
    {
        // Arrange & Act
        var collection = new ValidationResultCollection(message);

        // Assert
        collection.Count.ShouldBe(0);
        collection.IsValid.ShouldBeTrue();
    }

    // ═══════════════════════════════════════════════════════════
    // 构造函数 - ValidationResult
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：使用 ValidationResult 构造时，Count = 1，IsValid = false。
    /// </summary>
    [Fact]
    public void Ctor_WithValidationResult_ShouldContainOneResult()
    {
        // Arrange
        var result = new ValidationResult("年龄超出范围", new[] { "Age" });

        // Act
        var collection = new ValidationResultCollection(result);

        // Assert
        collection.Count.ShouldBe(1);
        collection.IsValid.ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：使用 null ValidationResult 构造时应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void Ctor_WithNullValidationResult_ShouldThrow()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentNullException>(() => new ValidationResultCollection((ValidationResult)null));
    }

    // ═══════════════════════════════════════════════════════════
    // 构造函数 - IEnumerable<ValidationResult>
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：使用多条 ValidationResult 集合构造时，Count 应等于集合长度。
    /// </summary>
    [Fact]
    public void Ctor_WithMultipleResults_ShouldSetCountCorrectly()
    {
        // Arrange
        var results = new[]
        {
            new ValidationResult("错误1", new[] { "Field1" }),
            new ValidationResult("错误2", new[] { "Field2" }),
            new ValidationResult("错误3", new[] { "Field3" })
        };

        // Act
        var collection = new ValidationResultCollection(results);

        // Assert
        collection.Count.ShouldBe(3);
        collection.IsValid.ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：使用 null 集合构造时应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void Ctor_WithNullEnumerable_ShouldThrow()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new ValidationResultCollection((IEnumerable<ValidationResult>)null));
    }

    // ═══════════════════════════════════════════════════════════
    // Add / AddRange
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：Add(ValidationResult) 应使 Count 增加 1，IsValid 变为 false。
    /// </summary>
    [Fact]
    public void Add_ValidResult_ShouldIncreaseCount()
    {
        // Arrange
        var collection = new ValidationResultCollection();

        // Act
        collection.Add(new ValidationResult("错误消息"));

        // Assert
        collection.Count.ShouldBe(1);
        collection.IsValid.ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：Add(null) 应被忽略，不改变 Count。
    /// </summary>
    [Fact]
    public void Add_Null_ShouldBeIgnored()
    {
        // Arrange
        var collection = new ValidationResultCollection();

        // Act
        collection.Add(null);

        // Assert
        collection.Count.ShouldBe(0);
    }

    /// <summary>
    /// 测试目的：AddRange(null) 应被忽略，不抛异常，Count 保持不变。
    /// </summary>
    [Fact]
    public void AddRange_Null_ShouldBeIgnoredWithoutThrowing()
    {
        // Arrange
        var collection = new ValidationResultCollection();

        // Act & Assert
        Should.NotThrow(() => collection.AddRange(null));
        collection.Count.ShouldBe(0);
    }

    /// <summary>
    /// 测试目的：AddRange 多条结果后，Count 应等于累计添加数量。
    /// </summary>
    [Fact]
    public void AddRange_MultipleResults_ShouldAccumulateCount()
    {
        // Arrange
        var collection = new ValidationResultCollection();
        var results = new[] { new ValidationResult("E1"), new ValidationResult("E2") };

        // Act
        collection.AddRange(results);

        // Assert
        collection.Count.ShouldBe(2);
    }

    // ═══════════════════════════════════════════════════════════
    // IsValid / Count
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：初始为空时 IsValid = true；添加一条错误后变为 false（边界转换）。
    /// </summary>
    [Fact]
    public void IsValid_TransitionFromTrueToFalse_WhenFirstErrorAdded()
    {
        // Arrange
        var collection = new ValidationResultCollection();
        collection.IsValid.ShouldBeTrue();

        // Act
        collection.Add(new ValidationResult("首个错误"));

        // Assert
        collection.IsValid.ShouldBeFalse();
    }

    // ═══════════════════════════════════════════════════════════
    // ToMessage
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：空集合 ToMessage 应包含"未发现验证错误"提示。
    /// </summary>
    [Fact]
    public void ToMessage_WhenValid_ShouldContainNoErrorText()
    {
        // Arrange
        var collection = new ValidationResultCollection();

        // Act
        var msg = collection.ToMessage();

        // Assert
        msg.ShouldContain("未发现验证错误");
    }

    /// <summary>
    /// 测试目的：有 1 条错误时 ToMessage 应包含"发现1个验证错误"。
    /// </summary>
    [Fact]
    public void ToMessage_WithOneError_ShouldContainSingleErrorText()
    {
        // Arrange
        var collection = new ValidationResultCollection("错误");

        // Act
        var msg = collection.ToMessage();

        // Assert
        msg.ShouldContain("发现1个验证错误");
    }

    /// <summary>
    /// 测试目的：有多条错误时 ToMessage 应包含错误总数。
    /// </summary>
    [Fact]
    public void ToMessage_WithMultipleErrors_ShouldContainErrorCount()
    {
        // Arrange
        var collection = new ValidationResultCollection(new[]
        {
            new ValidationResult("E1"),
            new ValidationResult("E2"),
            new ValidationResult("E3")
        });

        // Act
        var msg = collection.ToMessage();

        // Assert
        msg.ShouldContain("发现3个验证错误");
    }

    // ═══════════════════════════════════════════════════════════
    // ToValidationMessages
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：有效集合（空）调用 ToValidationMessages 应返回空枚举。
    /// </summary>
    [Fact]
    public void ToValidationMessages_WhenValid_ShouldReturnEmpty()
    {
        // Arrange
        var collection = new ValidationResultCollection();

        // Act
        var messages = collection.ToValidationMessages().ToList();

        // Assert
        messages.Count.ShouldBe(0);
    }

    /// <summary>
    /// 测试目的：有错误时 ToValidationMessages 应返回对应数量的消息字符串。
    /// </summary>
    [Fact]
    public void ToValidationMessages_WithErrors_ShouldReturnAllMessages()
    {
        // Arrange
        var collection = new ValidationResultCollection(new[]
        {
            new ValidationResult("姓名不能为空", new[] { "Name" }),
            new ValidationResult("年龄超出范围", new[] { "Age" })
        });

        // Act
        var messages = collection.ToValidationMessages().ToList();

        // Assert
        messages.Count.ShouldBe(2);
        messages.ShouldContain(m => m.Contains("姓名不能为空"));
        messages.ShouldContain(m => m.Contains("年龄超出范围"));
    }

    // ═══════════════════════════════════════════════════════════
    // 枚举 / 迭代
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：ValidationResultCollection 应支持 foreach 迭代，遍历出所有错误结果。
    /// </summary>
    [Fact]
    public void Enumeration_ShouldYieldAllResults()
    {
        // Arrange
        var collection = new ValidationResultCollection(new[]
        {
            new ValidationResult("E1"),
            new ValidationResult("E2")
        });

        // Act
        var items = collection.ToList();

        // Assert
        items.Count.ShouldBe(2);
        items[0].ErrorMessage.ShouldBe("E1");
        items[1].ErrorMessage.ShouldBe("E2");
    }

    // ═══════════════════════════════════════════════════════════
    // 复制构造
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：使用 ValidationResultCollection 复制构造时，Count 和 ErrorCode 应与原始一致。
    /// </summary>
    [Fact]
    public void CopyCtor_ShouldPreserveCountAndErrorCode()
    {
        // Arrange
        var original = new ValidationResultCollection(new[]
        {
            new ValidationResult("错误A"),
            new ValidationResult("错误B")
        });
        original.ErrorCode = 2001;

        // Act
        var copy = new ValidationResultCollection(original);

        // Assert
        copy.Count.ShouldBe(2);
        copy.ErrorCode.ShouldBe(2001);
    }

    /// <summary>
    /// 测试目的：复制构造传 null 时应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void CopyCtor_WithNull_ShouldThrow()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new ValidationResultCollection((ValidationResultCollection)null));
    }
}
