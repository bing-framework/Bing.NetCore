using Bing.Domain.Entities;

namespace Bing.Domain.Entities;

/// <summary>
/// 描述上下文 测试
/// </summary>
public class DescriptionContextTest
{
    private readonly DescriptionContext _context;

    /// <summary>
    /// 初始化
    /// </summary>
    public DescriptionContextTest()
    {
        _context = new DescriptionContext();
    }

    #region Output - 空上下文

    /// <summary>
    /// 测试目的：未添加任何描述时，Output 应返回空字符串。
    /// </summary>
    [Fact]
    public void Output_Empty_ShouldReturnEmptyString()
    {
        // Act
        var result = _context.Output();

        // Assert
        result.ShouldBe(string.Empty);
    }

    #endregion

    #region Add(string)

    /// <summary>
    /// 测试目的：Add(null) 不应添加任何内容，Output 仍为空。
    /// </summary>
    [Fact]
    public void Add_NullString_ShouldIgnore()
    {
        // Act
        _context.Add((string)null);

        // Assert
        _context.Output().ShouldBe(string.Empty);
    }

    /// <summary>
    /// 测试目的：Add(空白字符串) 不应添加任何内容，Output 仍为空。
    /// </summary>
    [Fact]
    public void Add_WhitespaceString_ShouldIgnore()
    {
        // Act
        _context.Add("   ");

        // Assert
        _context.Output().ShouldBe(string.Empty);
    }

    /// <summary>
    /// 测试目的：Add(有效描述) 应将描述追加到输出中。
    /// </summary>
    [Fact]
    public void Add_ValidString_ShouldAppendToOutput()
    {
        // Act
        _context.Add("Hello");

        // Assert
        _context.Output().ShouldBe("Hello");
    }

    /// <summary>
    /// 测试目的：多次 Add(string) 应将所有描述顺序拼接。
    /// </summary>
    [Fact]
    public void Add_MultipleStrings_ShouldConcatenateInOrder()
    {
        // Act
        _context.Add("Foo");
        _context.Add("Bar");

        // Assert
        _context.Output().ShouldBe("FooBar");
    }

    #endregion

    #region Add<TValue>(string name, TValue value)

    /// <summary>
    /// 测试目的：name 为 null 时应忽略，不影响输出。
    /// </summary>
    [Fact]
    public void Add_NameNull_ShouldIgnore()
    {
        // Act
        _context.Add<string>(null, "value");

        // Assert
        _context.Output().ShouldBe(string.Empty);
    }

    /// <summary>
    /// 测试目的：name 为空白时应忽略，不影响输出。
    /// </summary>
    [Fact]
    public void Add_NameWhitespace_ShouldIgnore()
    {
        // Act
        _context.Add<string>("   ", "value");

        // Assert
        _context.Output().ShouldBe(string.Empty);
    }

    /// <summary>
    /// 测试目的：value 为 null 时应忽略，不影响输出。
    /// </summary>
    [Fact]
    public void Add_ValueNull_ShouldIgnore()
    {
        // Act
        _context.Add<string>("Name", null);

        // Assert
        _context.Output().ShouldBe(string.Empty);
    }

    /// <summary>
    /// 测试目的：value 为 int 默认值（0）时应忽略，不影响输出。
    /// </summary>
    [Fact]
    public void Add_IntDefaultValue_ShouldIgnore()
    {
        // Act
        _context.Add<int>("Age", 0);

        // Assert
        _context.Output().ShouldBe(string.Empty);
    }

    /// <summary>
    /// 测试目的：name 和 value 均有效时，应以 "name:value," 格式追加，末尾逗号在 Output 时被剪除。
    /// </summary>
    [Fact]
    public void Add_ValidNameAndValue_ShouldFormatAsNameColonValue()
    {
        // Act
        _context.Add("Name", "Alice");

        // Assert
        _context.Output().ShouldBe("Name:Alice");
    }

    /// <summary>
    /// 测试目的：name 含前后空格时，格式化结果中 name 应被 Trim。
    /// </summary>
    [Fact]
    public void Add_NameWithWhitespacePadding_ShouldTrimName()
    {
        // Act
        _context.Add("  Name  ", "Alice");

        // Assert
        _context.Output().ShouldBe("Name:Alice");
    }

    /// <summary>
    /// 测试目的：连续添加多个键值对，Output 应包含所有键值，末尾逗号被裁去。
    /// </summary>
    [Fact]
    public void Add_MultipleNameValues_ShouldJoinWithComma()
    {
        // Act
        _context.Add("Name", "Alice");
        _context.Add("Age", 30);

        // Assert
        var output = _context.Output();
        output.ShouldContain("Name:Alice");
        output.ShouldContain("Age:30");
        output.ShouldEndWith("30");   // 末尾没有多余逗号
    }

    #endregion

    #region FlushCache

    /// <summary>
    /// 测试目的：FlushCache 后 Output 应返回空字符串。
    /// </summary>
    [Fact]
    public void FlushCache_AfterAdd_ShouldClearOutput()
    {
        // Arrange
        _context.Add("Name", "Alice");

        // Act
        _context.FlushCache();

        // Assert
        _context.Output().ShouldBe(string.Empty);
    }

    /// <summary>
    /// 测试目的：FlushCache 后可以重新添加内容，Output 仅含新内容。
    /// </summary>
    [Fact]
    public void FlushCache_ThenAdd_ShouldOnlyContainNewContent()
    {
        // Arrange
        _context.Add("OldKey", "OldValue");
        _context.FlushCache();

        // Act
        _context.Add("NewKey", "NewValue");

        // Assert
        var output = _context.Output();
        output.ShouldNotContain("OldKey");
        output.ShouldContain("NewKey:NewValue");
    }

    #endregion

    #region ToString

    /// <summary>
    /// 测试目的：ToString 应与 Output 返回相同的字符串。
    /// </summary>
    [Fact]
    public void ToString_ShouldReturnSameAsOutput()
    {
        // Arrange
        _context.Add("X", "Y");

        // Act & Assert
        _context.ToString().ShouldBe(_context.Output());
    }

    /// <summary>
    /// 测试目的：空上下文时 ToString 应返回空字符串。
    /// </summary>
    [Fact]
    public void ToString_Empty_ShouldReturnEmptyString()
    {
        // Act & Assert
        _context.ToString().ShouldBe(string.Empty);
    }

    #endregion

    #region Output - 末尾逗号裁剪

    /// <summary>
    /// 测试目的：单个键值对添加后，Output 末尾不应有逗号。
    /// </summary>
    [Fact]
    public void Output_SingleNameValue_ShouldNotEndWithComma()
    {
        // Act
        _context.Add("Key", "Value");

        // Assert
        _context.Output().ShouldEndWith("Value");
        _context.Output().ShouldNotEndWith(",");
    }

    #endregion
}
