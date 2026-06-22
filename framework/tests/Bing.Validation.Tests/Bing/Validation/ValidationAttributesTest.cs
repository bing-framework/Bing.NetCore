using System.ComponentModel.DataAnnotations;
using Bing.Validation;
using Shouldly;
using Xunit;

namespace Bing.Validation.Tests;

// ── 测试用模型定义（仅 string 参数属性，确保属性语法合法）──────────────────

internal class LetterModel
{
    [Letter]
    public string Code { get; set; }
}

internal class ChineseModel
{
    [Chinese]
    public string Name { get; set; }
}

internal class IdCardModel
{
    [IdCard]
    public string CardNo { get; set; }
}

internal class PostalModel
{
    [PostalCodeOfChina]
    public string Postal { get; set; }
}

internal class QQModel
{
    [QQ]
    public string QQNo { get; set; }
}

internal class MultiAttributeModel
{
    [Letter]
    public string Code { get; set; }

    [Chinese]
    public string Name { get; set; }
}

/// <summary>
/// 自定义 DataAnnotation 验证属性的单元测试
/// </summary>
public class ValidationAttributesTest
{
    // ── [Letter] ──────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：纯英文字母值应通过 [Letter] 验证。
    /// </summary>
    [Theory]
    [InlineData("abc")]
    [InlineData("ABC")]
    [InlineData("Hello")]
    public void LetterAttribute_ValidLetter_ShouldPass(string value)
    {
        // Arrange
        var model = new LetterModel { Code = value };

        // Act
        var result = DataAnnotationValidation.Validate(model);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：包含非字母字符（数字、空格、中文）的值应使 [Letter] 验证失败。
    /// </summary>
    [Theory]
    [InlineData("abc123")]
    [InlineData("Hello World")]
    [InlineData("测试")]
    public void LetterAttribute_InvalidLetter_ShouldFail(string value)
    {
        // Arrange
        var model = new LetterModel { Code = value };

        // Act
        var result = DataAnnotationValidation.Validate(model);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：null 或空字符串值应通过 [Letter] 验证（允许空值，非必填）。
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void LetterAttribute_NullOrEmpty_ShouldPass(string value)
    {
        // Arrange
        var model = new LetterModel { Code = value };

        // Act
        var result = DataAnnotationValidation.Validate(model);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    // ── [Chinese] ─────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：纯中文字符串应通过 [Chinese] 验证。
    /// </summary>
    [Theory]
    [InlineData("张三")]
    [InlineData("你好世界")]
    public void ChineseAttribute_ValidChinese_ShouldPass(string value)
    {
        // Arrange
        var model = new ChineseModel { Name = value };

        // Act
        var result = DataAnnotationValidation.Validate(model);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：含英文字母或数字的字符串应使 [Chinese] 验证失败。
    /// </summary>
    [Theory]
    [InlineData("abc")]
    [InlineData("张三abc")]
    [InlineData("123")]
    public void ChineseAttribute_InvalidChinese_ShouldFail(string value)
    {
        // Arrange
        var model = new ChineseModel { Name = value };

        // Act
        var result = DataAnnotationValidation.Validate(model);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：null 或空字符串应通过 [Chinese] 验证（允许空值）。
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ChineseAttribute_NullOrEmpty_ShouldPass(string value)
    {
        // Arrange
        var model = new ChineseModel { Name = value };

        // Act
        var result = DataAnnotationValidation.Validate(model);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    // ── [Money] (直接实例化测试，decimal 不支持属性语法中的编译时常量) ─────

    /// <summary>
    /// 测试目的：在 (Min, Max] 范围内的金额值应通过 MoneyAttribute 验证。
    /// </summary>
    [Theory]
    [InlineData("1")]
    [InlineData("500")]
    [InlineData("1000")]
    public void MoneyAttribute_ValidAmount_ShouldPass(string rawValue)
    {
        // Arrange
        var attr = new MoneyAttribute(0m, 1000m);
        var value = decimal.Parse(rawValue);
        var ctx = new ValidationContext(new object()) { MemberName = "Amount" };

        // Act
        var result = attr.GetValidationResult(value, ctx);

        // Assert — null 表示验证通过
        result.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：等于 Min（不满足 > Min）或超出 Max 的值应验证失败。
    /// </summary>
    [Theory]
    [InlineData("0")]     // 等于 Min，不满足 > 0
    [InlineData("1001")]  // 超过 Max
    [InlineData("-10")]   // 负数，低于 Min
    public void MoneyAttribute_InvalidAmount_ShouldFail(string rawValue)
    {
        // Arrange
        var attr = new MoneyAttribute(0m, 1000m);
        var value = decimal.Parse(rawValue);
        var ctx = new ValidationContext(new object()) { MemberName = "Amount" };

        // Act
        var result = attr.GetValidationResult(value, ctx);

        // Assert — 非 null 表示验证失败
        result.ShouldNotBeNull();
    }

    /// <summary>
    /// 测试目的：null 值（空值场景）应通过 MoneyAttribute 验证，不强制要求填写。
    /// </summary>
    [Fact]
    public void MoneyAttribute_Null_ShouldPass()
    {
        // Arrange
        var attr = new MoneyAttribute(0m, 1000m);
        var ctx = new ValidationContext(new object()) { MemberName = "Amount" };

        // Act
        var result = attr.GetValidationResult(null, ctx);

        // Assert
        result.ShouldBeNull();
    }

    // ── [IdCard] ──────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：符合身份证格式（15 位或 18 位含 X/x）的字符串应通过 [IdCard] 验证。
    /// </summary>
    [Theory]
    [InlineData("11010519491231002X")]   // 18 位含 X
    [InlineData("110105194912310020")]   // 18 位纯数字
    [InlineData("110105491231002")]      // 15 位
    public void IdCardAttribute_ValidIdCard_ShouldPass(string value)
    {
        // Arrange
        var model = new IdCardModel { CardNo = value };

        // Act
        var result = DataAnnotationValidation.Validate(model);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：不符合身份证格式的字符串应验证失败。
    /// </summary>
    [Theory]
    [InlineData("1234567")]             // 位数太少
    [InlineData("abcdefghijklmnopqr")] // 全为字母
    [InlineData("1101051949123100XY")] // 结尾两个字符无效
    public void IdCardAttribute_InvalidIdCard_ShouldFail(string value)
    {
        // Arrange
        var model = new IdCardModel { CardNo = value };

        // Act
        var result = DataAnnotationValidation.Validate(model);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：null 或空字符串应通过 [IdCard] 验证（允许空值）。
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IdCardAttribute_NullOrEmpty_ShouldPass(string value)
    {
        // Arrange
        var model = new IdCardModel { CardNo = value };

        // Act
        var result = DataAnnotationValidation.Validate(model);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    // ── [PostalCodeOfChina] ────────────────────────────────────────

    /// <summary>
    /// 测试目的：6 位纯数字邮政编码应通过 [PostalCodeOfChina] 验证。
    /// </summary>
    [Theory]
    [InlineData("100000")]
    [InlineData("200001")]
    [InlineData("310000")]
    public void PostalCodeAttribute_Valid_ShouldPass(string value)
    {
        // Arrange
        var model = new PostalModel { Postal = value };

        // Act
        var result = DataAnnotationValidation.Validate(model);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：位数不足、含字母或超出 6 位的编码应验证失败。
    /// </summary>
    [Theory]
    [InlineData("12345")]     // 只有 5 位
    [InlineData("1234567")]   // 7 位超出
    [InlineData("abcdef")]    // 含字母
    public void PostalCodeAttribute_Invalid_ShouldFail(string value)
    {
        // Arrange
        var model = new PostalModel { Postal = value };

        // Act
        var result = DataAnnotationValidation.Validate(model);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    // ── [QQ] ──────────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：满足 QQ 号规则（非 0 开头、5~11 位数字）应通过 [QQ] 验证。
    /// </summary>
    [Theory]
    [InlineData("10001")]
    [InlineData("123456789")]
    [InlineData("99999999999")] // 11 位（最大长度边界）
    public void QQAttribute_Valid_ShouldPass(string value)
    {
        // Arrange
        var model = new QQModel { QQNo = value };

        // Act
        var result = DataAnnotationValidation.Validate(model);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：以 0 开头、位数不足或含非数字字符的值应验证失败。
    /// </summary>
    [Theory]
    [InlineData("0001")]      // 以 0 开头
    [InlineData("1234")]      // 只有 4 位，不足 5 位
    [InlineData("abc123")]    // 含字母
    public void QQAttribute_Invalid_ShouldFail(string value)
    {
        // Arrange
        var model = new QQModel { QQNo = value };

        // Act
        var result = DataAnnotationValidation.Validate(model);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：null 或空字符串应通过 [QQ] 验证（允许空值）。
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void QQAttribute_NullOrEmpty_ShouldPass(string value)
    {
        // Arrange
        var model = new QQModel { QQNo = value };

        // Act
        var result = DataAnnotationValidation.Validate(model);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    // ── 多属性模型 ────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：同一模型上多个属性均违规时，所有错误应被汇总收集（非 fail-fast）。
    /// </summary>
    [Fact]
    public void MultipleAttributes_WhenBothInvalid_ShouldCollectAllErrors()
    {
        // Arrange — Code 含数字违反 [Letter]，Name 含英文违反 [Chinese]
        var model = new MultiAttributeModel { Code = "abc123", Name = "English" };

        // Act
        var result = DataAnnotationValidation.Validate(model);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Count.ShouldBe(2);
    }

    /// <summary>
    /// 测试目的：同一模型上多个属性均合法时，验证应通过。
    /// </summary>
    [Fact]
    public void MultipleAttributes_WhenBothValid_ShouldPass()
    {
        // Arrange
        var model = new MultiAttributeModel { Code = "HelloWorld", Name = "张三" };

        // Act
        var result = DataAnnotationValidation.Validate(model);

        // Assert
        result.IsValid.ShouldBeTrue();
    }
}
