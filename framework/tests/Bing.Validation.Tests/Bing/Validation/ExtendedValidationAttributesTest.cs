using System.ComponentModel.DataAnnotations;
using Bing.Validation;
using Shouldly;
using Xunit;

namespace Bing.Validation.Tests;

// ── 测试用模型定义 ─────────────────────────────────────────────────────────

internal class HttpUrlModel
{
    [HttpUrlAddress]
    public string Url { get; set; }
}

internal class TelNoModel
{
    [TelNoOfChina]
    public string TelNo { get; set; }
}

internal class PlateModel
{
    [PlateNumberOfChina]
    public string PlateNo { get; set; }
}

internal class WechatModel
{
    [WechatNo]
    public string WechatId { get; set; }
}

/// <summary>
/// 扩展验证 Attribute 单元测试：
/// <see cref="HttpUrlAddressAttribute"/>、<see cref="TelNoOfChinaAttribute"/>、
/// <see cref="PlateNumberOfChinaAttribute"/>、<see cref="WechatNoAttribute"/>
/// </summary>
public class ExtendedValidationAttributesTest
{
    // ── 辅助方法 ─────────────────────────────────────────────────────────────

    private static bool IsValid(object model)
    {
        var ctx = new System.ComponentModel.DataAnnotations.ValidationContext(model);
        var results = new List<ValidationResult>();
        return Validator.TryValidateObject(model, ctx, results, validateAllProperties: true);
    }

    // ════════════════════════════════════════════════════════════════
    // [HttpUrlAddress]
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：合法的 http/https URL 应通过 [HttpUrlAddress] 验证。
    /// </summary>
    [Theory]
    [InlineData("http://www.example.com")]
    [InlineData("https://example.com/path/to/page")]
    [InlineData("http://sub.domain.org/resource?key=value&other=123")]
    public void HttpUrlAddress_ValidUrl_ShouldPass(string url)
    {
        // Arrange
        var model = new HttpUrlModel { Url = url };

        // Act & Assert
        IsValid(model).ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：非 URL 格式字符串应使 [HttpUrlAddress] 验证失败。
    /// </summary>
    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.com")]
    [InlineData("www.example.com")]
    [InlineData("just some text")]
    public void HttpUrlAddress_InvalidUrl_ShouldFail(string url)
    {
        // Arrange
        var model = new HttpUrlModel { Url = url };

        // Act & Assert
        IsValid(model).ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：null 或空字符串应通过 [HttpUrlAddress] 验证（允许空值）。
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void HttpUrlAddress_NullOrEmpty_ShouldPass(string url)
    {
        // Arrange
        var model = new HttpUrlModel { Url = url };

        // Act & Assert
        IsValid(model).ShouldBeTrue();
    }

    // ════════════════════════════════════════════════════════════════
    // [TelNoOfChina]
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：符合中国固定电话号码格式（区号-号码）的字符串应通过验证。
    /// </summary>
    [Theory]
    [InlineData("010-12345678")]
    [InlineData("021-1234567")]
    [InlineData("0755-1234567")]
    [InlineData("01012345678")]
    public void TelNoOfChina_ValidTelNo_ShouldPass(string telNo)
    {
        // Arrange
        var model = new TelNoModel { TelNo = telNo };

        // Act & Assert
        IsValid(model).ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：不符合固话格式的字符串应使 [TelNoOfChina] 验证失败。
    /// </summary>
    [Theory]
    [InlineData("13800138000")]  // 手机号，非固话
    [InlineData("abcd-1234567")]
    [InlineData("12")]
    public void TelNoOfChina_InvalidTelNo_ShouldFail(string telNo)
    {
        // Arrange
        var model = new TelNoModel { TelNo = telNo };

        // Act & Assert
        IsValid(model).ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：null 或空字符串应通过 [TelNoOfChina] 验证（允许空值）。
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TelNoOfChina_NullOrEmpty_ShouldPass(string telNo)
    {
        // Arrange
        var model = new TelNoModel { TelNo = telNo };

        // Act & Assert
        IsValid(model).ShouldBeTrue();
    }

    // ════════════════════════════════════════════════════════════════
    // [PlateNumberOfChina]
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：符合中国车牌号格式的字符串应通过 [PlateNumberOfChina] 验证。
    /// </summary>
    [Theory]
    [InlineData("京A12345")]
    [InlineData("粤B88888")]
    [InlineData("沪C12AB6")]
    public void PlateNumberOfChina_ValidPlate_ShouldPass(string plateNo)
    {
        // Arrange
        var model = new PlateModel { PlateNo = plateNo };

        // Act & Assert
        IsValid(model).ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：不符合车牌格式（太短、纯数字等）应使验证失败。
    /// </summary>
    [Theory]
    [InlineData("123456")]
    [InlineData("AB12345")]
    [InlineData("1234")]
    public void PlateNumberOfChina_InvalidPlate_ShouldFail(string plateNo)
    {
        // Arrange
        var model = new PlateModel { PlateNo = plateNo };

        // Act & Assert
        IsValid(model).ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：null 或空字符串应通过 [PlateNumberOfChina] 验证（允许空值）。
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void PlateNumberOfChina_NullOrEmpty_ShouldPass(string plateNo)
    {
        // Arrange
        var model = new PlateModel { PlateNo = plateNo };

        // Act & Assert
        IsValid(model).ShouldBeTrue();
    }

    // ════════════════════════════════════════════════════════════════
    // [WechatNo]
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：满足微信号规则（字母开头，6~20位字母/数字/下划线/横线）的字符串应通过验证。
    /// </summary>
    [Theory]
    [InlineData("aBcDe12345")]
    [InlineData("wx_user-001")]
    [InlineData("hello_world_2024")]
    public void WechatNo_ValidWechat_ShouldPass(string wechat)
    {
        // Arrange
        var model = new WechatModel { WechatId = wechat };

        // Act & Assert
        IsValid(model).ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：不符合微信号格式（数字开头、太短）的字符串应验证失败。
    /// </summary>
    [Theory]
    [InlineData("1abc")]          // 数字开头
    [InlineData("ab")]            // 太短（不足6位）
    [InlineData("@invalid!")]     // 非法字符
    public void WechatNo_InvalidWechat_ShouldFail(string wechat)
    {
        // Arrange
        var model = new WechatModel { WechatId = wechat };

        // Act & Assert
        IsValid(model).ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：null 或空字符串应通过 [WechatNo] 验证（允许空值）。
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void WechatNo_NullOrEmpty_ShouldPass(string wechat)
    {
        // Arrange
        var model = new WechatModel { WechatId = wechat };

        // Act & Assert
        IsValid(model).ShouldBeTrue();
    }

    // ════════════════════════════════════════════════════════════════
    // ValidatePattern 常量验证
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：ValidatePattern 中各正则常量不为 null/空，确保静态初始化正常。
    /// </summary>
    [Fact]
    public void ValidatePattern_AllPatterns_ShouldNotBeNullOrEmpty()
    {
        // Assert
        Bing.Validations.Validators.ValidatePattern.MobilePhonePattern.ShouldNotBeNullOrWhiteSpace();
        Bing.Validations.Validators.ValidatePattern.IdCardPattern.ShouldNotBeNullOrWhiteSpace();
        Bing.Validations.Validators.ValidatePattern.ChinesePattern.ShouldNotBeNullOrWhiteSpace();
        Bing.Validations.Validators.ValidatePattern.UrlPattern.ShouldNotBeNullOrWhiteSpace();
        Bing.Validations.Validators.ValidatePattern.LetterPattern.ShouldNotBeNullOrWhiteSpace();
        Bing.Validations.Validators.ValidatePattern.PlateNumberOfChinaPatter.ShouldNotBeNullOrWhiteSpace();
        Bing.Validations.Validators.ValidatePattern.TelNoOfChinaPatter.ShouldNotBeNullOrWhiteSpace();
        Bing.Validations.Validators.ValidatePattern.WechatNoPatter.ShouldNotBeNullOrWhiteSpace();
        Bing.Validations.Validators.ValidatePattern.QQPatter.ShouldNotBeNullOrWhiteSpace();
        Bing.Validations.Validators.ValidatePattern.PostalCodeOfChinaPatter.ShouldNotBeNullOrWhiteSpace();
    }
}
